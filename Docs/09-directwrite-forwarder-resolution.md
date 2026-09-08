# DirectWriteForwarder framework-dependent 加载问题

## 问题范围

本专题记录 `WpfLab.WpfRuntime` NuGet 包被普通 WPF 开发项目消费时，`DirectWriteForwarder.dll` 未从应用输出目录加载的问题。

目标消费场景是：

- 项目目标框架为 `net8.0-windows`。
- 使用 `dotnet build` 生成 framework-dependent 输出。
- 使用 `dotnet run --no-build` 启动。
- 项目引用本仓库构建出的 `WpfLab.WpfRuntime` NuGet 包。
- 运行时继续依赖 `Microsoft.WindowsDesktop.App`，不能用 self-contained 发布改变问题模型。

## 原测试缺陷

此前 `PackageTestService` 使用 `dotnet publish --self-contained true`，然后直接启动发布目录中的 EXE。该测试会将运行时闭包完整复制到发布目录，不能覆盖普通开发者使用 `dotnet build` 和共享框架运行的程序集解析行为。

该测试即使通过，也不能证明 framework-dependent 应用会加载包内 `DirectWriteForwarder.dll`。以此得出 `ModuleInitializer` 加载顺序有效的结论是错误的。

当前包测试已改为：

1. 在隔离 NuGet 源和隔离包缓存中创建消费项目。
2. 执行 `dotnet build`，并显式保持 `SelfContained=false`。
3. 使用 SDK 默认的 `bin/Release/<TFM>/<RID>/` 输出目录。
4. 执行 `dotnet run --no-build --no-restore`。
5. 验证托管程序集实际加载路径、MVID 和 SHA-256。
6. 验证 `MS.Internal.Text.TextInterface.TextAnalyzer.Itemize` 的精确 ABI。
7. 实际执行 `FormattedText` 文本 shaping 和 XAML 控件创建。

## 已复现行为

在 `net8.0-windows/win-x86` 的真实 build/run 场景中：

- 应用输出目录存在 NuGet 包提供的 `DirectWriteForwarder.dll`。
- 应用 `.deps.json` 包含 `DirectWriteForwarder.dll` runtime 资产登记。
- `WindowsBase.dll`、`PresentationCore.dll` 和 `PresentationFramework.dll` 从应用输出目录加载。
- `DirectWriteForwarder.dll` 实际从已安装的 `Microsoft.WindowsDesktop.App/8.0.x` 共享框架目录加载。
- 随后 `PresentationCore` 调用包内新 ABI 时可能出现 `TextAnalyzer.Itemize` 的 `MissingMethodException`。

因此问题不是旧文件残留、NuGet 缓存混用或输出目录缺少文件，而是 framework-dependent 默认加载上下文中的程序集身份与统一行为。

## ModuleInitializer 的职责与能力边界

`PresentationCore/ModuleInitializer.cs` 本身仍有正常的 WPF 初始化职责，包括：

- 尽早设置进程 DPI awareness。
- 调用 `DWriteLoader.LoadDWrite()` 初始化 DirectWrite。
- 调用 `MS.Internal.NativeWPFDLLLoader.LoadDwrite()` 触发 WPF native/C++/CLI 组件初始化。

这些初始化职责与本次程序集身份冲突不同，应继续保留。

当前文件中后来加入的 app-local 程序集加载逻辑属于独立 workaround：

- `LoadAppLocalDirectWriteForwarder()`。
- `AssemblyLoadContext.Default.LoadFromAssemblyPath(...)`。
- 为确保该调用先执行而增加的 `NoInlining` 辅助方法和加载顺序调整。

该 workaround 不能可靠覆盖已经由共享框架满足的同身份程序集引用。`NoInlining` 可以避免 JIT 在方法入口过早解析静态依赖，但不能解决以下情况：

- 包内和共享框架中的程序集简单名称、版本、区域性和公钥标记构成兼容身份。
- 默认加载上下文已经选择共享框架程序集来满足引用。

统一程序集版本修复后，`PresentationCore` 引用 `DirectWriteForwarder, Version=42.42.42.42424`，共享框架中的 `8.0.0.0` 不能满足该引用。此时正常的 `.deps.json` 和默认加载上下文应直接选择应用输出目录中的包内 forwarder，不再需要手工按路径抢先加载。

因此，最终收敛目标是：保留正常 DPI、DirectWrite 和 native 初始化职责；删除仅用于 app-local 程序集抢先加载的 workaround。删除后必须重新执行真实 framework-dependent NuGet 消费测试，只有加载路径、ABI 和文本 shaping 继续通过，才能确认该 workaround 可以安全移除。

## DirectWriteForwarder 版本缺陷

已确认 `DirectWriteForwarder.vcxproj` 构建求值期间存在 `$(AssemblyVersion)`，但 C++/CLI 项目不会像 SDK 风格 C# 项目一样自动生成托管 `AssemblyVersionAttribute`。

当前未显式生成该特性时，产出的 `DirectWriteForwarder.dll` 程序集版本为 `0.0.0.0`。这是构建链缺陷，不是期望设计。

已执行过两项诊断实验：

- 硬编码 `AssemblyVersion("8.0.0.1")`：net8 x86/x64 build/run 可以加载 app-local forwarder 并通过 shaping，但该版本没有接入仓库统一版本体系，只能证明程序集身份是根因，不能作为最终实现。
- 使用普通 `$(AssemblyVersion)`，即 `8.0.0.0`：真实 build/run 仍加载共享框架 forwarder，因为共享框架版本也是 `8.0.0.0`，身份冲突未消除。

上述实验均已撤回。

## 统一隔离版本要求

本仓库自产 WPF 运行时程序集应使用统一隔离程序集版本：

`42.42.42.42424`

当前实现采用以下统一版本链：

1. Builder 以独立的 `WpfRuntimeAssemblyVersion` 属性将 `42.42.42.42424` 传入所有运行时项目构建，不与 NuGet 包版本混用。
2. 根 `Directory.Build.targets` 在 Arcade props 求值完成后、程序集属性生成前，将 `WpfRuntimeAssemblyVersion` 映射为 `AssemblyVersion`。
3. SDK 风格托管项目由正常程序集属性生成流程写入 `AssemblyVersionAttribute`。
4. C++/CLI `DirectWriteForwarder` 通过预处理宏接收同一个 `WpfRuntimeAssemblyVersion`，并在 `OtherAssemblyAttrs.cpp` 显式生成托管 `AssemblyVersionAttribute`，避免退化为 `0.0.0.0`。
5. Builder 在组包前使用 PE 元数据读取所有 x86/x64 运行时程序集的实际 CLR 版本；任一程序集不是 `42.42.42.42424` 时立即停止组包。
6. 消费探针同时检查实际加载路径、程序集版本、MVID、SHA-256、`TextAnalyzer.Itemize` ABI、文本 shaping 和 XAML 控件创建。

NuGet 包语义版本和 CLR 程序集版本是不同概念。Builder 的 `--version` 参数继续控制 NuGet 包版本；`42.42.42.42424` 控制本仓库运行时程序集身份隔离，不应从任意 NuGet 预发布版本字符串直接推导。

## 不采用的修复

以下方案不能作为该问题的最终修复：

- 修改或降级 `global.json` 中的 Arcade SDK。
- 为 `Demo/WpfDemo` 添加仅对仓库 Demo 生效的特殊程序集解析逻辑。
- 添加 `SkipDirectWriteForwarderProjectReference` 来绕开 NuGet 消费问题。
- 在 `OtherAssemblyAttrs.cpp` 中硬编码临时版本号。
- 只复制 app-local DLL，而不验证实际加载位置。
- 只检查 `.deps.json` 中存在 runtime 资产。
- 使用 self-contained publish 结果代替 framework-dependent build/run 验证。

## MSBuild 与 dotnet build 边界

仓库本身包含 C++/CLI 项目，完整产品构建应继续使用 Builder 找到的 Visual Studio `MSBuild.exe`。`dotnet build` 使用 Core MSBuild，不能可靠承载 Visual C++ targets；手工设置 `VCTargetsPath` 会在 Visual C++ 任务加载阶段产生 MSBuild API 不兼容，不是正确解决方式。

这不影响 NuGet 消费验证：开发者消费已经构建好的 NuGet 包时不应构建仓库内的 vcxproj，消费项目必须能够直接使用普通 `dotnet build` 和 `dotnet run`。

## 当前状态与下一步

当前已完成：

- 真实 framework-dependent build/run 测试能够稳定复现原问题。
- 已确认 app-local 文件存在且 `.deps.json` 已登记时，同身份 forwarder 仍可能由共享框架满足。
- 已确认 `0.0.0.0` 和 `8.0.0.0` 均不能作为本仓库包的隔离程序集身份。
- Builder 已向全部 x86/x64 WPF 运行时项目传播统一程序集版本 `42.42.42.42424`。
- `DirectWriteForwarder` 已显式写入相同的 C++/CLI 托管程序集版本。
- 组包前版本门禁已确认所有收集到的 x86/x64 运行时程序集均为 `42.42.42.42424`。
- `PresentationCore/ModuleInitializer.cs` 已恢复为正常初始化逻辑，只保留 DPI awareness、`DWriteLoader.LoadDWrite()` 和 `NativeWPFDLLLoader.LoadDwrite()`；手工 app-local 加载与 `NoInlining` workaround 已移除。
- 清理后重新生成的 `复包 `WpfLab.WpfRuntime.1.0.0-cleanup-validation.nupkg` 已通过 framework-dependent 消费矩阵。
- 消费矩阵覆盖 .NET 8、.NET 9、win-x86、win-x64、单目标和多目标项目，并通过 app-local 加载、精确 ABI、文本 shaping 与 XAML 控件验证。
- Builder 完整单元测试共 140 项通过。

当前结论：统一程序集身份修复是根本修复，`ModuleInitializer` 不再承担程序集解析 workaround。后续变更不得重新引入 self-contained-only 验证或手工抢先加载来替代 framework-dependent build/run 门禁。
