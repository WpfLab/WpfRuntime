# WpfDemo 仓库 WPF 实现

## 目标/边界

`Demo/WpfDemo` 是仓库 WPF 的开发与运行探针宿主。它负责在构建 WPF 实现后：

- 让 C# 与 XAML 使用实现项目自动生成的参考程序集。
- 将仓库 WPF 托管实现、主题程序集和 x64 native 资产部署为 app-local 文件。
- 通过 `.deps.json` 与 `.runtimeconfig.json` 让应用脱离 `Microsoft.WindowsDesktop.App.WPF` 共享框架运行。
- 在界面或 `--verify-repo-wpf` 模式中验证关键托管程序集和 native 模块的加载来源。

当前消费管线只支持 `Debug|x64`。`x86`、`arm64` 和 Publish 均未实现。解决方案的 `Any CPU` 只负责映射到 WpfDemo 项目的 `x64`，不表示 WpfDemo 已具备 AnyCPU 运行时闭包。

本文只描述 WpfDemo 的当前实现契约。仓库整体状态以 [00-overview.md](00-overview.md) 为准，后续工作以 [01-phase-plan.md](01-phase-plan.md) 为准。

## 项目与共享文件

WpfDemo 项目侧文件：

- `Demo/WpfDemo/WpfDemo.csproj`：声明 WPF 应用、x64 平台、构建依赖，并在 SDK targets 后导入仓库 `Microsoft.WinFX.targets`。
- `Demo/WpfDemo/Directory.Build.props`：先继承仓库根 props，再导入 `eng/WpfDemo/RepoWpfConsumer.props`。
- `Demo/WpfDemo/Directory.Build.targets`：先继承仓库根 targets，再导入 `eng/WpfDemo/RepoWpfConsumer.targets`。
- `Demo/WpfDemo/Diagnostics/WpfRuntimeProbe.cs`：实现托管程序集、C++/CLI 程序集和 native 模块的 app-local 来源断言。
- `Demo/WpfDemo/App.xaml.cs` 与 `MainWindow.xaml.cs`：接入自动探针、验证报告和退出码。

共享构建文件：

- `eng/WpfDemo/RepoWpfConsumer.props`：导入共享版本与资产清单，设置 repo-WPF 消费开关，声明 runtime 包。
- `eng/WpfDemo/RepoWpfConsumer.targets`：替换编译引用、逐项目收集 runtime、登记 deps、复制资源与 native 文件，并执行输出校验。
- `eng/WpfRuntimeDependencies.props`：由 Builder 与 WpfDemo 共用的目标框架、WindowsDesktop runtime 版本、NuGet 包、编译程序集、runtime 程序集、主题项目和 native 文件清单。
- 根 `Directory.Build.targets`：为 `InternalMarkupCompilation=true` 的项目导入仓库标记编译 targets，并处理 `NoInternalTypeHelper=true` 的公共生成逻辑。
- `Microsoft.Dotnet.Wpf.slnx`：声明 WpfDemo 及其解决方案平台映射。

共享版本与资产清单已经实现，不应在 WpfDemo 项目中另建同名版本常量或复制清单。`eng/WpfRuntimeDependencies.props` 会按需导入 `eng/Versions.props`，其中 WindowsDesktop x64 runtime 版本当前为 `8.0.6`。

## 构建依赖而非运行引用

WpfDemo 对 `PresentationFramework.csproj` 以及七个外部主题项目使用：

- `Targets="Restore;Build"`
- `ReferenceOutputAssembly="false"`

这些 `ProjectReference` 只用于建立还原、构建和增量构建顺序，不向 WpfDemo 注入项目输出作为普通编译或运行引用。这样可避免实现程序集、自动 ref、手工 ref、SDK inbox 程序集和 cycle-breaker 同名程序集同时进入 XAML 或 C# 引用图。

`PresentationFramework` 是托管主链的顶层构建依赖；主题程序集由运行时按主题名称加载，因此七个主题项目另行显式加入构建依赖。运行资产由共享清单逐项收集，不依赖 `ProjectReference` 的传递复制行为。

## 自动 ref 引用

WpfDemo 移除隐式 `Microsoft.WindowsDesktop.App.WPF` FrameworkReference，并在 `ResolveReferences` 后清理 SDK 或项目解析得到的同名 WPF 引用，再注入实现项目生成的自动 ref：

```text
artifacts/obj/<Assembly>/x64/Debug/net8.0/ref/<Assembly>.dll
```

当前编译清单为：

- `WindowsBase`
- `System.Xaml`
- `System.Windows.Input.Manipulations`
- `UIAutomationTypes`
- `UIAutomationProvider`
- `PresentationCore`
- `PresentationFramework`

替换同时覆盖 `ReferencePath`、`ReferencePathWithRefAssemblies` 和 `ReferenceDependencyPaths`。自动 ref 随实现项目公开 API 一起生成，因此 WpfDemo 不以手工 `*-ref.csproj` 作为新增 API 试验的主要编译契约。

仓库 `PresentationBuildTasks/Microsoft.WinFX.targets` 在 SDK targets 后导入，C# 与 XAML 因而共享同一组自动 ref。缺少任一 ref 输出时，非设计时构建会报告具体缺失路径。

## 每项目 runtime 收集

正式实现不再把 `PresentationFramework` 输出目录整包复制为运行时闭包。该做法只属于早期原型，会把传递副本误当成项目主输出，并可能掩盖版本或来源错误。

当前实现遍历 `RepoWpfRuntimeAssembly`，从每个程序集所属项目的输出目录收集其主 DLL：

```text
artifacts/bin/<Assembly>/x64/Debug/net8.0/<Assembly>.dll
```

`DirectWriteForwarder.dll` 使用其 C++/CLI 项目输出布局单独定位。文化资源程序集当前通过 `<ProjectOutput>\*\<Assembly>.resources.dll` 收集，复制目标依赖 `%(RecursiveDir)`。该模式没有使用递归通配符产生稳定的 `RecursiveDir` 元数据，因此尚未证明会保留文化子目录，并存在不同文化的同名卫星程序集被扁平复制或覆盖的风险。

共享 runtime 清单包含核心 WPF、ReachFramework、PresentationUI、Ribbon、WindowsFormsIntegration、UIAutomation、七个主题程序集和 `DirectWriteForwarder` 等条目；其中 `WpfDemoRequired=true` 的条目缺失时立即失败，其他条目仅在项目主输出存在时纳入。

`System.Printing` 不属于当前 WpfDemo runtime 清单。打印实现的迁移和构建状态是仓库其他工作项，不能据此把 `System.Printing.dll` 加入 WpfDemo 当前运行闭包。

## deps/runtimeconfig

收集到的实现程序集会加入：

- `ReferenceDependencyPaths`，并设置 `IncludeRuntimeDependency=true`。
- `ReferenceCopyLocalPaths`，用于复制到 WpfDemo 输出目录。

这两步分别保证运行时程序集写入 `WpfDemo.deps.json` 和部署到应用目录。只复制 DLL 而不登记 deps 不能形成可靠的 app-local 运行契约。

构建后校验包括：

- `WpfDemo.runtimeconfig.json` 与 `WpfDemo.deps.json` 必须存在。
- `runtimeconfig` 不得包含 `Microsoft.WindowsDesktop.App`。
- `deps` 必须登记 `WindowsBase.dll`、`PresentationCore.dll`、`PresentationFramework.dll` 和 `DirectWriteForwarder.dll`。
- 输出目录必须存在关键托管程序集、`ijwhost.dll` 和关键 native WPF 文件。

`ReferenceOutputAssembly=false` 会切断上游项目包依赖的自然传递，因此 WpfDemo 从共享 `RepoWpfRuntimePackage` 清单显式引用运行时 NuGet 包。Builder 与 WpfDemo 使用同一版本来源。

## 主题 BAML 与 native/ijwhost 部署

七个主题项目为：

- `PresentationFramework.Aero`
- `PresentationFramework.Aero2`
- `PresentationFramework.AeroLite`
- `PresentationFramework.Classic`
- `PresentationFramework.Fluent`
- `PresentationFramework.Luna`
- `PresentationFramework.Royale`

它们既是显式构建依赖，也是逐项目 runtime 收集条目。输出校验使用 PE 元数据读取每个主题 DLL，确认存在 `<AssemblyName>.g.resources`，并确认对应文件名已登记到 `WpfDemo.deps.json`。仅检查 DLL 存在不足以证明主题 BAML 已生成。

x64 native 文件来自 `Microsoft.WindowsDesktop.App.Runtime.win-x64` 的共享版本，当前清单为：

- `D3DCompiler_47_cor3.dll`
- `PenImc_cor3.dll`
- `PresentationNative_cor3.dll`
- `vcruntime140_cor3.dll`
- `wpfgfx_cor3.dll`

`DirectWriteForwarder.dll` 从其项目主输出收集，`ijwhost.dll` 从同一 C++/CLI 构建输出复制并在 WpfDemo 目录中统一使用官方文件名。构建会在源文件缺失时失败，避免以机器共享框架文件补齐闭包。

`PresentationCore` 的模块初始化当前会调用 `DWriteLoader.LoadDWrite()`。代码中已发现 `UnloadDWrite()` 定义，但尚未确认存在对应调用，因此不能声称 DWrite 在进程退出时的释放已经验证。

## 运行探针

`WpfRuntimeProbe` 已实现，验证内容包括：

- 进程架构必须为 x64。
- `WindowsBase`、`PresentationCore` 和 `PresentationFramework` 的实际位置必须等于 WpfDemo 输出目录中的预期文件。
- 三个核心程序集的目标框架必须为 `.NETCoreApp,Version=v8.0`，程序集版本必须为 `8.0.0.0`。
- `DirectWriteForwarder.dll` 必须作为 app-local C++/CLI 程序集加载。
- `PenImc_cor3.dll`、`PresentationNative_cor3.dll` 和 `wpfgfx_cor3.dll` 必须从应用目录加载，并能在当前进程模块中找到。

普通启动会在窗口内容呈现后、Dispatcher 到达 `ApplicationIdle` 时执行探针并显示结果。传入 `--verify-repo-wpf` 时，应用会写出 `WpfDemo.repo-wpf-validation.txt`，成功返回 0，验证失败返回非零退出码。

该探针验证的是被检查模块的架构、版本和实际加载位置，不替代完整解决方案构建、所有 WPF 功能或 Visual Studio 调试链路验证。

## 平台映射

WpfDemo 项目声明 `Platforms=x64` 和 `PlatformTarget=x64`，消费 targets 同时拒绝非 `Debug` 配置和非 `x64` 平台。

`Microsoft.Dotnet.Wpf.slnx` 当前映射：

- 解决方案 `Any CPU` -> WpfDemo 项目 `x64`
- 解决方案 `x64` -> WpfDemo 项目 `x64`

WpfDemo 没有 `x86` 或 `arm64` 映射。解决方案 `Any CPU` 映射成功只表示 WpfDemo 选择 x64 项目配置；完整 `Debug|Any CPU` 构建仍需按 [00-overview.md](00-overview.md) 的边界单独验证。

## 验证状态

- WpfDemo x64 消费、共享版本与资产清单、自动 ref、逐项目 runtime 收集、deps/runtimeconfig 校验、主题 BAML 校验、native/ijwhost 部署和运行探针均已实现；文化资源的子目录保持仍需修正和验证。
- 现存 `WpfDemo.repo-wpf-validation.txt` 报告证明 x64 探针曾成功，并记录核心托管程序集及四个受检 native/C++/CLI 模块来自 WpfDemo 输出目录。
- 该报告不能外推为当前工作区完整 `Microsoft.Dotnet.Wpf.slnx` 构建成功，也不能替代重新执行后的结果。
- 当前完整解决方案构建状态只在 [00-overview.md](00-overview.md) 维护。
- Visual Studio F5 所需项目与探针实现已经存在，但 F5 尚未复验。
- `x86`、`arm64` 和 Publish 尚未实现。
- DWrite 退出释放尚未验证；当前只确认 `UnloadDWrite()` 定义存在，未确认调用。

## 未完成边界与任务归属

- 完整解决方案复验、Visual Studio F5、文化资源子目录修正、WpfDemo x86/arm64 和 Publish 的执行顺序与完成标准统一维护在 [01-phase-plan.md](01-phase-plan.md)。
- 共享 native 清单的维护性收敛以及 `UnloadDWrite()` 生命周期调查记录在 [backlog.md](backlog.md)，不作为该实现文档的独立计划。
- 本文只在实现结构或验证边界变化时更新，不复制仓库级错误、进程 ID 或阶段进度。
