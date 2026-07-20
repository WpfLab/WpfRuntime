# WpfDemo 仓库 WPF 构建运行改造计划

## 目标

将 `Demo/WpfDemo/WpfDemo.csproj` 改造成仓库内 WPF 的开发测试宿主，达到以下体验：

- 修改 `WindowsBase`、`PresentationCore`、`PresentationFramework` 等仓库项目后，只构建 `WpfDemo` 即可自动构建受影响的 WPF 依赖。
- 在 `PresentationCore` 等实现项目中新增公开 API 后，`WpfDemo` 可立即编译调用，不需要手工同步 `ref/*.csproj` 源文件。
- `WpfDemo` 的 XAML 由仓库自产 `PresentationBuildTasks.dll` 编译。
- 运行时加载 `artifacts/bin` 中刚构建的 WPF 托管程序集和随应用部署的 native DLL，而不是机器上的 `Microsoft.WindowsDesktop.App` 共享框架。
- Visual Studio 中将 `WpfDemo` 设为启动项目后可直接按 F5；命令行可通过一条 `msbuild` 命令构建。

首期范围固定为 `Debug|x64`。在 x64 链路稳定并具备加载来源断言后，再扩展 x86 和 arm64。

## 当前实施状态

Debug|x64 首期代码已实施，命令行和真实进程验证已通过：

- Builder clean 后，仅构建 `Demo/WpfDemo/WpfDemo.csproj` 即可传递构建仓库 WPF 主链。
- C#/XAML 已使用实现项目自动 ref；临时新增 `PresentationCore` public API 并由 WpfDemo 调用的验证已通过。
- XAML 已使用仓库 `PresentationBuildTasks.dll`，运行时已切换为 app-local 仓库 WPF 与 NuGet native DLL。
- `WpfDemo.exe --verify-repo-wpf` 真实退出码为 0，核心托管/native 模块加载路径均位于 WpfDemo 输出目录。
- 七个外部主题项目已纳入 WpfDemo 的项目级构建依赖；构建会验证每个主题 DLL 都包含 `<Assembly>.g.resources` 并登记在 `.deps.json`，避免缺失 BAML 的主题程序集造成启动黑屏。
- `PresentationCore` 模块初始化已恢复 `DWriteLoader.LoadDWrite()` 与进程退出清理；`DirectWriteForwarder` 已恢复 MicrosoftShared 公钥签名，字体/DirectWrite 初始化和首屏文本布局已通过真实进程验证。
- 解决方案 `Any CPU` 和 `x64` 已映射到 WpfDemo 项目 x64。
- 尚未完成的验收项只有 Visual Studio F5，以及 x86/arm64 扩展。

以下“已验证基线”中的“当前行为”描述的是改造前状态，用于保留问题背景。

## 已验证基线

### 改造前 WpfDemo 实际行为

当前项目文件只有标准 WPF SDK 配置：

- `TargetFramework=net8.0-windows`
- `UseWPF=true`
- 无任何 `ProjectReference`
- 由 SDK 隐式加入 `Microsoft.WindowsDesktop.App.WPF`

`Demo/WpfDemo` 自带以下文件，因 MSBuild 向上查找规则而隔离了仓库根配置：

- `Demo/WpfDemo/Directory.Build.props`
- `Demo/WpfDemo/Directory.Build.targets`
- `Demo/WpfDemo/global.json`

实测结果：

- x64 和 Any CPU 均可构建并启动。
- `WpfDemo.runtimeconfig.json` 声明 `Microsoft.WindowsDesktop.App`。
- 输出目录仅有 WpfDemo 自身文件。
- 进程实际从 `C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App\8.0.1\` 加载 `WindowsBase.dll`、`PresentationCore.dll`、`PresentationFramework.dll`、`DirectWriteForwarder.dll`、`PenImc_cor3.dll`、`PresentationNative_cor3.dll` 和 `wpfgfx_cor3.dll`。

因此，改造前的 WpfDemo 无法验证仓库内 WPF 的实现改动或新增 API。

### 已验证的目标技术路径

已在 `artifacts` 下创建隔离探针验证以下组合可构建、可启动：

1. 移除 `Microsoft.WindowsDesktop.App.WPF`。
2. 用 `PresentationFramework.csproj` 作为顶层构建依赖，并设置 `ReferenceOutputAssembly=false`。
3. 让 C# 与 XAML 编译引用实现项目自动生成的参考程序集：
   - `artifacts/obj/<Project>/x64/Debug/net8.0/ref/<Assembly>.dll`
4. 让运行时使用实现程序集：
   - `artifacts/bin/<Project>/x64/Debug/net8.0/<Assembly>.dll`
5. 通过 `IncludeRuntimeDependency=true` 将实现程序集登记到 `.deps.json`。
6. 从 `Microsoft.WindowsDesktop.App.Runtime.win-x64` 部署 WPF native DLL，并从 `DirectWriteForwarder` 输出部署 `ijwhost.dll`。
7. 显式导入仓库 `PresentationBuildTasks/Microsoft.WinFX.targets`。
8. 显式引用 Builder 已维护的 WPF 运行时 NuGet 依赖。

最终探针成功启动，以下模块均从探针输出目录加载：

- `WindowsBase.dll`
- `PresentationCore.dll`
- `PresentationFramework.dll`
- `DirectWriteForwarder.dll`
- `PenImc_cor3.dll`
- `PresentationNative_cor3.dll`
- `wpfgfx_cor3.dll`

## 关键设计决策

### 1. 编译引用与运行实现必须分离

不能直接让普通 `ProjectReference` 决定 WpfDemo 的编译引用。实测直接引用实现项目时，XAML 标记编译报：

```text
MC1000: 已知类型值 380='LostFocusEventManager' 不是有效的已知类型
```

正确方式是：

- `ProjectReference` 只负责构建顺序。
- 编译与 XAML 使用实现项目自动生成的 ref 输出。
- 运行使用实现项目的 bin 输出。

这样既满足 XAML 编译对参考程序集的要求，也能让新增公开 API 随实现项目构建自动进入 ref 输出。

### 2. 不使用手工 `*-ref.csproj` 作为 WpfDemo 的主要编译契约

`src/**/ref/*-ref.csproj` 由手工维护的参考源码生成。若只在 `PresentationCore` 实现项目中新增 API，手工 ref 项目不会自动包含该 API，不符合“新增 API 后直接构建 WpfDemo 即可试验”的目标。

WpfDemo 应优先使用 SDK 为实现项目生成的：

```text
artifacts/obj/<Project>/<platform>/<configuration>/net8.0/ref/<Assembly>.dll
```

手工 ref 项目仍保留给仓库现有构建链和 API 对比流程，不作为 WpfDemo 新 API 试验的必经步骤。

### 3. 必须移除 WindowsDesktop WPF 共享框架

仅把同名 WPF DLL 复制到应用目录无效。只要 `runtimeconfig.json` 仍声明 `Microsoft.WindowsDesktop.App`，宿主仍优先从共享框架加载 WPF。

WpfDemo 最终只应声明：

```text
Microsoft.NETCore.App
```

仓库 WPF 程序集作为 app-local 运行时资产进入 `.deps.json`。

### 4. 复用 Builder 的运行时闭包

`eng/Builder/NuGetPackageService.cs` 已维护并验证：

- 需要移除的 inbox WPF 程序集名称。
- 运行时 NuGet 依赖清单。
- x64/x86 native DLL 清单。
- 包消费项目的加载来源验证方式。

WpfDemo 的 props/targets 应复用或抽取这套清单，避免 Demo 与 NuGet 包消费逻辑漂移。

## 分阶段实施步骤

### 阶段 1：恢复仓库配置继承

1. 删除 `Demo/WpfDemo/global.json`，让项目继承根 `global.json` 的 .NET 8 SDK 选择。
2. 删除或改造 `Demo/WpfDemo/Directory.Build.props` 与 `Directory.Build.targets`，确保根配置被显式导入而不是被截断。
3. 移除 WpfDemo 自定义的 `ArtifactsPath` 别名，统一使用根配置提供的 `ArtifactsDir`、`ArtifactsBinDir` 和标准输出路径。
4. 验证 `NETCoreSdkVersion=8.0.101`、`WpfSourceDir`、`ArtifactsBinDir`、`WpfNativePlatform` 均可求值。

完成标准：WpfDemo 与 WPF 项目使用同一 SDK、同一 artifacts 布局和同一平台映射。

### 阶段 2：建立 x64 构建根

1. 将 WpfDemo 首期平台限定为 x64；在解决方案中把 `Any CPU` 映射到 WpfDemo 的 x64。
2. 添加 `PresentationFramework.csproj` 构建依赖：
   - `Targets="Restore;Build"`
   - `ReferenceOutputAssembly="false"`
3. 确保该构建根能传递构建 `PresentationCore`、`WindowsBase`、`System.Xaml`、UIAutomation、ReachFramework、cycle-breaker 和 `DirectWriteForwarder`。
4. 显式构建 `PresentationFramework.Aero`、`Aero2`、`AeroLite`、`Classic`、`Fluent`、`Luna` 和 `Royale`；这些程序集由 `PresentationFramework` 在运行时按主题名加载，不在主框架的传递构建图中。
5. 对关键输出增加构建前验证，缺少时给出明确错误路径。

完成标准：改动 PresentationCore 后，仅构建 WpfDemo 会重建 PresentationCore 及其自动 ref 输出。

### 阶段 3：接入仓库标记编译任务

1. 设置 `ImportFrameworkWinFXTargets=true`，阻止 SDK 导入预装的 `Microsoft.WinFX.targets`。这是一个反向开关：值为 `true` 表示跳过 SDK 默认导入，随后由项目显式导入仓库 targets。
2. 在 SDK targets 后导入仓库内：
   - `src/Microsoft.DotNet.Wpf/src/PresentationBuildTasks/Microsoft.WinFX.targets`
3. 保留按 MSBuild 宿主选择 `net472`/`net8.0` 的现有逻辑。
4. 确认 Visual Studio 与命令行都使用 `artifacts/bin/PresentationBuildTasks/x64/Debug/<tfm>/PresentationBuildTasks.dll`。

完成标准：WpfDemo XAML 构建不加载 SDK 自带 PresentationBuildTasks。

### 阶段 4：替换编译与 XAML 引用

1. 移除隐式 `Microsoft.WindowsDesktop.App.WPF` FrameworkReference。
2. 在 `ResolveReferences` 后移除同名 inbox/ProjectReference 解析结果。
3. 从实现项目自动 ref 输出注入以下首期编译程序集：
   - `WindowsBase`
   - `System.Xaml`
   - `System.Windows.Input.Manipulations`
   - `UIAutomationTypes`
   - `UIAutomationProvider`
   - `PresentationCore`
   - `PresentationFramework`
4. 根据 WpfDemo 使用的上层 API再加入 `ReachFramework`、`PresentationUI`、Ribbon、WindowsFormsIntegration 等 ref 输出。
5. 为每个 ref 路径增加存在性检查，错误中指出应由哪个实现项目生成。

完成标准：普通 XAML 可编译；在 PresentationCore 添加公开 API 后，WpfDemo 可立即编译调用。

### 阶段 5：登记并部署托管实现闭包

1. 从 `artifacts/bin` 收集仓库实现程序集，不从 ref 输出复制。
2. 将实现程序集加入：
   - `ReferenceDependencyPaths`，设置 `IncludeRuntimeDependency=true`
   - `ReferenceCopyLocalPaths`，保证复制到 `TargetDir`
3. 至少覆盖 Builder 的运行时程序集清单，并排除：
   - `*-ref`
   - `*-api-cycle`
   - `*-impl-cycle`
   - `PresentationBuildTasks`
   - `mcwpf`
4. 复制各程序集本地化资源子目录。
5. 验证 `.deps.json` 包含 `WindowsBase.dll`、`PresentationCore.dll`、`PresentationFramework.dll`、`DirectWriteForwarder.dll` 和全部外部主题 DLL。
6. 使用 PE 元数据检查每个主题 DLL 都包含与程序集同名的 `.g.resources` manifest resource。

完成标准：WpfDemo 输出包含一致架构的完整仓库 WPF 托管闭包，runtimeconfig 不再声明 WindowsDesktop 共享框架。

### 阶段 6：接入 native 与 NuGet 运行时依赖

1. 使用 `PackageDownload` 拉取 `Microsoft.WindowsDesktop.App.Runtime.win-x64`；版本必须来自与 Builder 共用的 `RepoWpfWindowsDesktopRuntimeVersion` 属性。当前 Builder 的已验证值为 `8.0.6`，不得在 WpfDemo 中另写一份常量。
2. 复制至少以下 native DLL：
   - `D3DCompiler_47_cor3.dll`
   - `PenImc_cor3.dll`
   - `PresentationNative_cor3.dll`
   - `vcruntime140_cor3.dll`
   - `wpfgfx_cor3.dll`
3. 从 DirectWriteForwarder 构建输出复制 `ijwhost.dll`。
4. 显式加入 Builder 当前维护的运行时包版本：
   - `System.Configuration.ConfigurationManager`
   - `System.Diagnostics.EventLog`
   - `System.DirectoryServices`
   - `System.Drawing.Common`
   - `System.Formats.Nrbf`
   - `System.IO.Packaging`
   - `System.Resources.Extensions`
   - `System.Security.Cryptography.Xml`
   - `System.Security.Permissions`
   - `System.Windows.Extensions`
5. 将版本清单抽取成共享 MSBuild 文件，供 Builder 和 WpfDemo 共用，避免双份硬编码。

完成标准：WpfDemo 可启动，不因托管包或 native DLL 缺失退出。

### 阶段 7：增加自证式 Demo 探针

1. 将 `eng/Builder/PackageTestApp` 中的加载来源验证逻辑抽取或移植到 WpfDemo。
2. 在启动后显示并断言：
   - `typeof(DependencyObject).Assembly.Location`
   - `typeof(Visual).Assembly.Location`
   - `typeof(Application).Assembly.Location`
3. 断言路径均位于 `AppContext.BaseDirectory`。
4. 显示程序集版本、目标框架和当前进程架构。
5. 提供一个适合临时试验 PresentationCore API 的独立页面或代码区。
6. 未捕获异常写入标准错误并返回非零退出码，便于自动验证。

完成标准：用户能在界面或日志中直接确认当前运行的是仓库 WPF，而不是系统 WPF。

### 阶段 8：统一开发入口与验证

1. 在 Visual Studio 中将 WpfDemo 的 `Any CPU` 映射到 x64，并设为推荐启动项目。
2. 推荐命令：

```powershell
msbuild Demo\WpfDemo\WpfDemo.csproj -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /nr:false /v:minimal
```

3. 增加可选命令或 Builder 子命令，用于启动 WpfDemo 并自动检查加载来源。
4. 完成干净构建、增量构建、新 API 编译和实际启动验证。
5. x64 稳定后，再按相同契约扩展 x86/arm64，native 包和 C++ 平台映射必须随架构切换。

## 验证矩阵

| 场景 | 操作 | 预期结果 |
|---|---|---|
| 干净构建 | 清理 artifacts 后构建 WpfDemo | 自动构建所需 WPF 项目并成功 |
| 增量实现改动 | 修改 PresentationCore 方法体后构建 WpfDemo | PresentationCore 与 WpfDemo 增量重建 |
| 新增 API | 在 PresentationCore 新增 public API，并在 WpfDemo 调用 | 无需修改手工 ref 源即可编译 |
| XAML | 修改 MainWindow.xaml | 使用仓库 PresentationBuildTasks 成功生成 BAML |
| 运行时来源 | 启动 WpfDemo | 核心 WPF 模块路径均为 WpfDemo 输出目录 |
| 共享框架隔离 | 检查 runtimeconfig | 仅声明 Microsoft.NETCore.App |
| native 闭包 | 启动窗口、文本、输入和基本绘制 | 无 DllNotFoundException/BadImageFormatException |
| Visual Studio | 将 WpfDemo 设为启动项目并 F5 | 自动构建依赖并进入调试 |
| 命令行 | 执行推荐 msbuild 命令 | 一条命令成功构建 |
| 错误诊断 | 删除一个 required runtime DLL 后启动 | 明确失败并报告缺失文件或加载来源 |

## 风险与控制措施

### 同名程序集混入

风险：SDK inbox、手工 ref、自动 ref、实现程序集和 cycle-breaker 可能同时进入引用图。

控制：集中维护同名程序集清单；在一个 target 中完成移除和重新注入；构建后校验每个程序集唯一来源。

### 新增 API 与 XAML 编译不同步

风险：直接使用实现 DLL会触发 `LostFocusEventManager` 已知类型错误；使用手工 ref 又看不到新增 API。

控制：固定使用实现项目自动生成的 ref 输出进行编译/XAML，使用 bin 输出运行。

### deps.json 未登记 app-local WPF

风险：只复制 DLL 而不登记 runtime dependency 会导致宿主报 FileNotFoundException。

控制：为实现程序集设置 `IncludeRuntimeDependency=true`，并在自动测试中检查 deps.json 和实际加载路径。

### ProjectReference 的包依赖不再传递

风险：`ReferenceOutputAssembly=false` 后，WPF 项目的 NuGet 运行时包不会自动成为 WpfDemo 依赖。

控制：共享 Builder 的运行时包清单，在 WpfDemo 显式引用。

### 平台不一致

风险：Any CPU 应用加载 x64 的 DirectWriteForwarder/native DLL 会产生架构错误。

控制：首期锁定 x64；解决方案 Any CPU 映射到 x64；所有 ref、impl、native 路径统一使用 `WpfNativePlatform`。

### Visual Studio 锁定 PresentationBuildTasks

风险：IDE 进程加载任务程序集后可能锁定输出。

控制：沿用 PresentationBuildTasks 当前“删除或改名锁定 DLL”的机制；验证时使用 `/nr:false`，并单独覆盖 Visual Studio F5 场景。

## 完成定义

满足以下全部条件才视为改造完成：

1. 清理 artifacts 后，只构建 WpfDemo 即可成功。
2. PresentationCore 新增 public API 后，WpfDemo 可直接编译调用。
3. WpfDemo XAML 使用仓库 PresentationBuildTasks 构建。
4. WpfDemo runtimeconfig 不声明 Microsoft.WindowsDesktop.App。
5. WpfDemo 启动后，核心托管与 native WPF 模块全部从应用输出目录加载。
6. Visual Studio F5 与命令行 x64 构建均通过。
7. 自动验证能在误加载系统 WPF 时失败，而不是只凭“窗口能打开”判断成功。
