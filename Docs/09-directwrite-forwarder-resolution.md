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

## ModuleInitializer 的能力边界

`PresentationCore/ModuleInitializer.cs` 在模块初始化时调用 `AssemblyLoadContext.Default.LoadFromAssemblyPath`，不能可靠覆盖已经由共享框架满足的同身份程序集引用。

将静态依赖调用隔离到 `NoInlining` 方法可以避免 JIT 在方法入口过早解析依赖，但不能解决以下情况：

- 包内和共享框架中的程序集简单名称、版本、区域性和公钥标记构成兼容身份。
- 默认加载上下文已经选择共享框架程序集来满足引用。

因此，加载时序调整不是完整修复。最终修复必须保证包内 `PresentationCore` 所引用的 `DirectWriteForwarder` 身份不能由 inbox 共享框架版本满足。

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

最终修复必须：

1. 由 Builder 将统一隔离版本传入所有运行时项目构建，而不是仅修改单个源文件。
2. 让 SDK 风格托管项目和 C++/CLI `DirectWriteForwarder` 使用同一个版本输入。
3. 让 `DirectWriteForwarder` 显式生成托管 `AssemblyVersionAttribute`，避免退化为 `0.0.0.0`。
4. 确认 `PresentationCore` 的程序集引用记录为相同的 `DirectWriteForwarder, Version=42.42.42.42424`。
5. 在组包前校验所有目标运行时程序集的程序集版本，禁止 `DirectWriteForwarder` 为 `0.0.0.0` 或与其他运行时程序集不一致。
6. 使用真实 `dotnet build` 和 `dotnet run --no-build` 回归验证 app-local 加载与文本 shaping。

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

- 真实 framework-dependent build/run 测试能够稳定复现问题。
- 已确认 app-local 文件存在且 `.deps.json` 已登记，仍会加载共享框架 forwarder。
- 已确认 `0.0.0.0` 和 `8.0.0.0` 均不能作为最终程序集身份。
- 已确认目标统一隔离程序集版本为 `42.42.42.42424`。

下一步实施：

1. 在 Builder 中定义并向运行时项目传播统一隔离程序集版本。
2. 修复 `DirectWriteForwarder` 的 C++/CLI 托管程序集版本生成。
3. 增加组包前版本一致性校验和对应单元测试。
4. 重新构建 x86/x64 NuGet 包。
5. 执行 net8 framework-dependent `dotnet build` + `dotnet run --no-build` 回归测试。
6. 只有实际加载包内 `DirectWriteForwarder 42.42.42.42424` 且文本 shaping 通过，才可判定修复完成。
