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

### 当前已存在但尚未纳入解决方案的主要项目

以下项目已在磁盘中存在，但当前未出现在 `Microsoft.Dotnet.Wpf.sln`：

- `WindowsFormsIntegration`
- `PresentationFramework`
- `PresentationUI`
- `ReachFramework`
- `System.Printing`（含 `ref` 与 native 项目）
- `System.Windows.Controls.Ribbon`
- 各主题项目
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

使用当前工作区的整体构建入口重新验证后，`Microsoft.Dotnet.Wpf.sln` 已恢复可构建：

- 命令：`msbuild C:\lindexi\Code\God\WpfReorganize\Microsoft.Dotnet.Wpf.sln -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal`
- 结果：构建成功。

`ReachFramework-ref` 已补齐 `PresentationFramework-System.Printing-api-cycle` 中的 `ISerializerFactory` 与 `XpsDocumentWriter` 桥接 API，并可独立构建：

- 命令：`msbuild C:\lindexi\Code\God\WpfReorganize\src\Microsoft.DotNet.Wpf\src\ReachFramework\ref\ReachFramework-ref.csproj -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal`
- 结果：构建成功。

此前 `UIAutomationClientSideProviders` 缺失 `UIAutomationClient` 参考程序集的问题没有复现。`UIAutomationClient` 独立构建可产出实现程序集和 ref 相关输出；解决方案入口在清理后重建也可通过。

### 当前可直接确认的含义

1. 当前已纳入解决方案的项目恢复到可构建基线。
2. 构建仍存在多项警告，包括 `WindowsBase` 4.0.0.0 与 6.0.2.0 引用冲突警告，以及 `System.Formats.Nrbf` 从 8.0.0 解析到 9.0.0 的 NuGet 警告。
3. `ReachFramework-ref` 与 `System.Printing-ref` 可独立构建，但 `ReachFramework` / `PresentationFramework` 主链仍存在 cycle-breaker 与同名程序集 API 暴露问题。

## 当前主要缺口

1. 当前解决方案已纳入项目可构建，但尚未纳管的主链项目仍需继续打通。
2. 解决方案纳管明显滞后于磁盘现状，至少以下主项目仍未进入 `Microsoft.Dotnet.Wpf.sln`：
   - `WindowsFormsIntegration`
   - `PresentationFramework`
   - `PresentationUI`
   - `ReachFramework`
   - `System.Printing`
   - `System.Windows.Controls.Ribbon`
   - 各主题项目
3. 关键顶层模块仍未迁入：
   - `PenImc`
   - `System.Windows.Presentation`
   - `WpfGfx`
4. `PresentationFramework` 依赖链虽然目录与项目文件已存在，但当前真实阻塞集中在 `ReachFramework-ref` / `ReachFramework` / `PresentationFramework` 与 cycle-breaker 的同名 API 暴露上：
   - `ReachFramework-ref` 所需的 `System.Windows.Xps.XpsDocumentWriter`、`System.Windows.Documents.Serialization.ISerializerFactory` 已由 `PresentationFramework-System.Printing-api-cycle` 暴露，参考程序集项目可独立构建。
   - `ReachFramework` 实现项目仍未通过，当前错误集中在 `XpsSerializerWriter` 与 `XpsDocument` 调用 `XpsDocumentWriter` 时，`System.Printing.PrintTicket`、`XpsDocument`、`SerializerWriter` 等类型分别来自 `ReachFramework` 实现、`ReachFramework-PresentationFramework-api-cycle`、`System.Printing-ref` 或 `PresentationFramework` bridge，导致参数类型身份不一致。
   - `AvTrace` 代码生成目标仍需在 `PresentationFramework` 主项目继续验证。
5. 当前仓库仍保留个人机器本地引用路径，影响可移植性与构建复现性。

## 建议的当前优先级

1. 保持当前 `Microsoft.Dotnet.Wpf.sln` 可构建基线，不要为扩大纳管范围破坏现有项目。
2. 继续收敛 `ReachFramework` 实现项目的 cycle-breaker 引用，优先解决 `XpsSerializerWriter` / `XpsDocumentWriter` 调用链中的 `PrintTicket` 与 `XpsDocument` 类型身份不一致。
3. 再重新验证 `PresentationFramework` 的真实阻塞点。
4. 在 `PresentationFramework` 链路稳定后，再评估 `WindowsFormsIntegration` 的重新纳管与构建状态。
5. 最后再处理缺失顶层模块：`PenImc`、`System.Windows.Presentation`、`WpfGfx`。

## 当前执行约束

1. 若仓库中已存在 `WindowsBase.csproj`、`PresentationCore.csproj`、`PresentationFramework.csproj` 等同名项目，排查类型解析错误时必须先检查是否引入了第二份 inbox 程序集。
2. 禁止再次通过修改 `eng/Versions.props` 中的 `AssemblyVersion` 来掩盖同名程序集冲突。
3. 迁移工作中尽量把真实项目加入解决方案，并保持可构建；不要通过移除项目来规避构建问题。


