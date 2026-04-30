# 当前概览

## 项目目标

将当前 WPF 仓库重组为一个更易维护的结构，逐步收敛项目依赖、共享源码路径和构建入口，最终达到以下目标：

- 可以直接在 Visual Studio 中打开并构建 `Microsoft.Dotnet.Wpf.sln`。
- 可以使用一条 `msbuild` 命令对该解决方案或关键项目完成构建。
- 在重组过程中尽量保持与原始 WPF 仓库的目录结构和模块边界一致，降低后续同步和排障成本。

## 当前已验证事实

### 基础构建环境

- 仓库根目录存在 `Directory.Build.props`、`Directory.Build.targets`、`global.json`、`eng/Versions.props`。
- 当前统一目标框架为 `.NET 8`。
- `global.json` 指定 SDK 为 `8.0.206`。
- 当前解决方案入口为 `Microsoft.Dotnet.Wpf.sln`。
- `Directory.Build.props` 当前定义了：
  - `WpfSourceDir=$(RepoRoot)src\Microsoft.DotNet.Wpf\src\`
  - `WpfSharedDir=$(RepoRoot)src\Microsoft.DotNet.Wpf\src\Shared\`
  - `WpfCommonDir=$(RepoRoot)src\Microsoft.DotNet.Wpf\src\Common\`
  - `WpfCycleBreakersDir=$(RepoRoot)cycle-breakers\`
  - `WpfCodeGenDir=$(RepoRoot)eng\WpfArcadeSdk\tools\`
- 当前仓库仍依赖本地 `C:\lindexi\Lib\Microsoft.WindowsDesktop.App\` 作为部分引用路径。
- `Directory.Build.targets` 当前包含对 `WindowsBase`、`PresentationCore`、`PresentationFramework`、`ReachFramework`、`System.Printing` 的 inbox 引用清理逻辑，用于避免仓库项目与 SDK 隐式框架引用同时进入编译图。

### 当前已存在的顶层源码目录

`src/Microsoft.DotNet.Wpf/src/` 当前已确认存在：

- `Common`
- `DirectWriteForwarder`
- `Extensions`
- `PresentationBuildTasks`
- `PresentationCore`
- `PresentationFramework`
- `PresentationUI`
- `ReachFramework`
- `Shared`
- `System.Printing`
- `System.Windows.Controls.Ribbon`
- `System.Windows.Input.Manipulations`
- `System.Xaml`
- `Themes`
- `UIAutomation`
- `WindowsBase`
- `WindowsFormsIntegration`

### 当前已确认存在的主要项目文件

当前仓库磁盘上已能找到以下关键项目文件：

- 托管主项目
  - `src/Microsoft.DotNet.Wpf/src/WindowsBase/WindowsBase.csproj`
  - `src/Microsoft.DotNet.Wpf/src/System.Xaml/System.Xaml.csproj`
  - `src/Microsoft.DotNet.Wpf/src/PresentationCore/PresentationCore.csproj`
  - `src/Microsoft.DotNet.Wpf/src/PresentationFramework/PresentationFramework.csproj`
  - `src/Microsoft.DotNet.Wpf/src/PresentationUI/PresentationUI.csproj`
  - `src/Microsoft.DotNet.Wpf/src/ReachFramework/ReachFramework.csproj`
  - `src/Microsoft.DotNet.Wpf/src/System.Windows.Controls.Ribbon/System.Windows.Controls.Ribbon.csproj`
  - `src/Microsoft.DotNet.Wpf/src/System.Windows.Input.Manipulations/System.Windows.Input.Manipulations.csproj`
  - `src/Microsoft.DotNet.Wpf/src/WindowsFormsIntegration/WindowsFormsIntegration.csproj`
  - `src/Microsoft.DotNet.Wpf/src/UIAutomation/UIAutomationTypes/UIAutomationTypes.csproj`
  - `src/Microsoft.DotNet.Wpf/src/UIAutomation/UIAutomationProvider/UIAutomationProvider.csproj`
  - `src/Microsoft.DotNet.Wpf/src/UIAutomation/UIAutomationClient/UIAutomationClient.csproj`
  - `src/Microsoft.DotNet.Wpf/src/UIAutomation/UIAutomationClientSideProviders/UIAutomationClientSideProviders.csproj`
- Native / mixed 项目
  - `src/Microsoft.DotNet.Wpf/src/DirectWriteForwarder/DirectWriteForwarder.vcxproj`
  - `src/Microsoft.DotNet.Wpf/src/System.Printing/System.Printing.vcxproj`
  - `src/Microsoft.DotNet.Wpf/src/Shared/OSVersionHelper/OSVersionHelper.vcxproj`
- 参考程序集项目
  - `WindowsBase-ref`
  - `System.Xaml-ref`
  - `PresentationCore-ref`
  - `PresentationFramework-ref`
  - `PresentationUI-ref`
  - `ReachFramework-ref`
  - `System.Printing-ref`
  - `System.Windows.Input.Manipulations-ref`
  - `System.Windows.Controls.Ribbon-ref`
  - `UIAutomationTypes-ref`
  - `UIAutomationProvider-ref`
  - `UIAutomationClient-ref`
  - `UIAutomationClientSideProviders-ref`
  - `WindowsFormsIntegration-ref`
- 主题项目
  - `PresentationFramework.Aero`
  - `PresentationFramework.Aero2`
  - `PresentationFramework.AeroLite`
  - `PresentationFramework.Classic`
  - `PresentationFramework.Fluent`
  - `PresentationFramework.Luna`
  - `PresentationFramework.Royale`
- 其他项目
  - `src/Microsoft.DotNet.Wpf/src/Shared/Tracing/mcwpf/mcwpf.csproj`

### 当前已确认存在的 cycle-breaker 项目

`cycle-breakers/` 当前已确认存在：

- `cycle-breakers/PresentationFramework/PresentationFramework-PresentationUI-api-cycle.csproj`
- `cycle-breakers/PresentationFramework/PresentationFramework-ReachFramework-impl-cycle.csproj`
- `cycle-breakers/PresentationFramework/PresentationFramework-System.Printing-api-cycle.csproj`
- `cycle-breakers/PresentationFramework/PresentationFramework-System.Printing-impl-cycle.csproj`
- `cycle-breakers/PresentationUI/PresentationUI-PresentationFramework-impl-cycle.csproj`
- `cycle-breakers/ReachFramework/ReachFramework-PresentationFramework-api-cycle.csproj`
- `cycle-breakers/ReachFramework/ReachFramework-System.Printing-api-cycle.csproj`
- `cycle-breakers/System.Printing/System.Printing-PresentationFramework-api-cycle.csproj`

### 当前解决方案已纳入的项目

`Microsoft.Dotnet.Wpf.sln` 当前已纳入：

- `System.Xaml`
- `WindowsBase`
- `WpfDemo`
- `System.Windows.Input.Manipulations`
- `UIAutomationTypes`
- `UIAutomationProvider`
- `DirectWriteForwarder`
- `Docs`
- `PresentationCore`
- `UIAutomationClient`
- `UIAutomationClientSideProviders`
- `ReachFramework`
- `PresentationFramework`
- `PresentationUI`
- `PresentationFramework.Classic`\r\n- `PresentationFramework.Aero`\r\n- `PresentationFramework.Aero2`\r\n- `PresentationFramework.AeroLite`\r\n- `PresentationFramework.Fluent`\r\n- `PresentationFramework.Luna`\r\n- `PresentationFramework.Royale`\r\n- `System.Windows.Controls.Ribbon`

### 当前已存在但尚未纳入解决方案的主要项目

以下项目已在磁盘中存在，但当前未出现在 `Microsoft.Dotnet.Wpf.sln`：

- `WindowsFormsIntegration`
- `System.Printing`（含 `ref` 与 native 项目）
- `PresentationBuildTasks`
- `Shared/Tracing/mcwpf`
- 多数 `ref/*.csproj`
- `cycle-breakers/*.csproj`

### 与原始仓库的顶层目录差异

相较原始 WPF 仓库，当前重组仓库尚未出现以下顶层目录：

- `PenImc`
- `System.Windows.Presentation`
- `WpfGfx`

## 当前构建状态

### 最新验证结果

使用当前工作区的整体构建入口重新验证后，`Microsoft.Dotnet.Wpf.sln` 可构建：

- 命令：`msbuild C:\lindexi\Code\God\WpfReorganize\Microsoft.Dotnet.Wpf.sln -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal`
- 结果：构建成功。
- 剩余警告：`DirectWriteForwarder.vcxproj` 仍报告 `D9035`，即 `/Zc:forScope-` 已否决并将在将来版本中移除。

`ReachFramework-ref` 已补齐 `PresentationFramework-System.Printing-api-cycle` 中的 `ISerializerFactory` 与 `XpsDocumentWriter` 桥接 API，并可独立构建：

- 命令：`msbuild C:\lindexi\Code\God\WpfReorganize\src\Microsoft.DotNet.Wpf\src\ReachFramework\ref\ReachFramework-ref.csproj -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal`
- 结果：构建成功。

此前 `UIAutomationClientSideProviders` 缺失 `UIAutomationClient` 参考程序集的问题没有复现。`UIAutomationClient` 独立构建可产出实现程序集和 ref 相关输出；解决方案入口在清理后重建也可通过。

`ReachFramework` 实现项目已越过 `XpsSerializerWriter` / `XpsDocumentWriter` 调用链中的 `PrintTicket` 与 `XpsDocument` 类型身份不一致阻塞，并可独立构建：

- 命令：`msbuild C:\lindexi\Code\God\WpfReorganize\src\Microsoft.DotNet.Wpf\src\ReachFramework\ReachFramework.csproj -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal`
- 结果：构建成功。

`PresentationFramework` 实现项目已进入可独立构建状态：

- 命令：`msbuild C:\lindexi\Code\God\WpfReorganize\src\Microsoft.DotNet.Wpf\src\PresentationFramework\PresentationFramework.csproj -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal`
- 结果：构建成功。

`PresentationUI` 实现项目已进入可独立构建状态：

- 命令：`msbuild C:\lindexi\Code\God\WpfReorganize\src\Microsoft.DotNet.Wpf\src\PresentationUI\PresentationUI.csproj -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal /clp:ErrorsOnly`
- 结果：构建成功。
- 当前处理方式：`PresentationUI` 改为引用 `System.Printing-ref`，并显式引用完整 `PresentationFramework` 实现输出，避免 `System.Printing-ref` 依赖的 `PresentationFramework-System.Printing-api-cycle` 同名程序集覆盖完整 `PresentationFramework` API。
- 当前仍有迁移性占位：`InstallationError`、`TenFeetInstallationError`、`TenFeetInstallationProgress` 与 `FindToolBar` 增加了最小 XAML partial 占位成员，用于在 `InternalMarkupCompilation` 链路尚未完全接入时保持托管编译可推进。后续需要恢复为真实标记编译生成代码。

`System.Windows.Controls.Ribbon` 与全部主题项目已进入可独立构建并已纳入解决方案：

- `System.Printing-ref` 已移除 `System.Windows.Xps.Packaging.XpsDocument` 占位，避免 `PresentationUI` 同时从 `ReachFramework` 与 `System.Printing` 解析到同名 `XpsDocument`。
- `PresentationFramework.Classic`、`PresentationFramework.Aero`、`PresentationFramework.Aero2`、`PresentationFramework.AeroLite`、`PresentationFramework.Fluent`、`PresentationFramework.Luna`、`PresentationFramework.Royale` 与 `System.Windows.Controls.Ribbon` 的实现项目和 ref 项目已显式引用完整 `PresentationFramework` x64 输出，用于补齐 `Thickness`、`Style`、`Control` 等完整 PresentationFramework API。
- `BuildInfo.SystemWindowsControlsRibbon` 已调整为 WCP 公钥，使 `PresentationCore` / `PresentationFramework` 对 Ribbon 的友元程序集声明与当前输出程序集强命名一致。

`System.Printing` C++/CLI 项目已越过早期 MSBuild 配置阻塞，但尚未可独立构建：

- 已修复：空 `WpfCppProps` 导入、旧 `TargetFrameworkIdentifier/TargetFrameworkVersion` 组合、默认 `v100` 平台工具集、缺失 `FilterItem1ByItem2` 自定义任务依赖、`/clr:pure` 与 .NET Core C++/CLI 不兼容。
- 当前首个错误面：改用 `/clr:netcore` 后，`System.Printing` 编译进入 C++/CLI 类型重定义与缺引用阶段，首批错误集中在 `SafeMemoryHandle`、`PrintSystemDispatcherObject`、`PrintJobSettings`、`ILegacyDevice`、`PrintQueue` 重定义，以及 `System.IO.Packaging` 引用缺失。
- 错误日志：`artifacts/system-printing-errors.log`。

`WindowsFormsIntegration` 已重新验证，当前未能独立构建：

- 命令：`msbuild C:\lindexi\Code\God\WpfReorganize\src\Microsoft.DotNet.Wpf\src\WindowsFormsIntegration\WindowsFormsIntegration.csproj -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal /clp:ErrorsOnly`
- 当前首个错误面：缺少完整 `PresentationFramework` 控件/API 引用，表现为 `FrameworkElement`、`Panel`、`Thickness`、`Window`、`AdornerDecorator` 等类型缺失，以及 `WindowsFormsHost` 对 `IKeyboardInputSink` 的接口签名不匹配。
- 错误日志：`artifacts/windowsformsintegration-errors.log`。

### 当前可直接确认的含义

1. 当前解决方案已在纳入 `ReachFramework`、`PresentationFramework`、`PresentationUI`、`PresentationFramework.Classic` 与 `System.Windows.Controls.Ribbon` 后恢复可构建基线。
2. 解决方案级构建报告仍保留 `DirectWriteForwarder` 的 `/Zc:forScope-` native 警告。
3. 多个主链项目已可独立构建，但仍存在 cycle-breaker、显式 HintPath 与同名程序集 API 暴露边界，后续仍需继续收敛。
4. `System.Printing` C++/CLI 项目已进入源码编译阶段，但尚未构建成功；`PresentationUI` 当前通过 `System.Printing-ref` 绕过实现程序集阻塞。

## 当前主要缺口

1. 当前解决方案已纳入项目可构建，但尚未纳管的主链项目仍需继续打通。
2. 解决方案纳管仍滞后于磁盘现状，至少以下主项目仍未进入 `Microsoft.Dotnet.Wpf.sln`：
   - `WindowsFormsIntegration`
   - `System.Printing`（C++/CLI 实现项目仍失败，ref 项目可用）
   3. 关键顶层模块仍未迁入：
   - `PenImc`
   - `System.Windows.Presentation`
   - `WpfGfx`
4. `PresentationFramework` 依赖链虽然目录与项目文件已存在，但 cycle-breaker 的同名 API 暴露仍需要继续收敛：
   - `ReachFramework-ref` 所需的 `System.Windows.Xps.XpsDocumentWriter`、`System.Windows.Documents.Serialization.ISerializerFactory` 已由 `PresentationFramework-System.Printing-api-cycle` 暴露，参考程序集项目可独立构建。
   - `ReachFramework` 实现项目已通过动态调用边界越过 `XpsSerializerWriter` 与 `XpsDocument` 调用 `XpsDocumentWriter` 时的 `PrintTicket` / `XpsDocument` 类型身份不一致。
   - `PresentationFramework` 实现项目已通过动态调用边界越过打印相关 `XpsDocumentWriter`、`SerializerWriter`、`ISerializerFactory` 与 `PresentationUI` 中 `FindToolBar` 的迁移边界阻塞。
    - `PresentationUI`、`PresentationFramework.Classic` 与 `System.Windows.Controls.Ribbon` 已纳入解决方案，但当前依赖 XAML partial 占位、显式完整 `PresentationFramework` 引用与友元公钥调整，后续需恢复真实标记编译链路并收敛同名程序集解析。
   - `AvTrace` 代码生成目标在 `PresentationFramework` 主项目独立构建中未形成当前阻塞。
5. 当前仓库仍保留个人机器本地引用路径，影响可移植性与构建复现性。

## 建议的当前优先级

1. 保持当前 `Microsoft.Dotnet.Wpf.sln` 可构建基线，不要为扩大纳管范围破坏现有项目。
2. 继续收敛 `ReachFramework` / `PresentationFramework` / `PresentationUI` 的动态边界和同名程序集解析，优先评估是否可以用更明确的项目引用或桥接 API 替换当前动态调用与显式 HintPath。
3. 继续处理 `System.Printing` C++/CLI 的类型重定义和 `System.IO.Packaging` 引用缺失问题。
4. 在 `PresentationUI`、Ribbon 与主题链路稳定后，继续处理 `WindowsFormsIntegration` 的完整 `PresentationFramework` API 引用和 `IKeyboardInputSink` 签名问题。\r\n5. 继续处理 `PresentationBuildTasks` 的 SDK 版本目标问题与 `Shared/Tracing/mcwpf` 的 DevDiv targets 导入问题。
6. 最后再处理缺失顶层模块：`PenImc`、`System.Windows.Presentation`、`WpfGfx`。

## 当前执行约束

1. 若仓库中已存在 `WindowsBase.csproj`、`PresentationCore.csproj`、`PresentationFramework.csproj` 等同名项目，排查类型解析错误时必须先检查是否引入了第二份 inbox 程序集。
2. 禁止再次通过修改 `eng/Versions.props` 中的 `AssemblyVersion` 来掩盖同名程序集冲突。
3. 迁移工作中尽量把真实项目加入解决方案，并保持可构建；不要通过移除项目来规避构建问题。


