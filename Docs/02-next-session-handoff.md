# 下一次 AI 对话交接

## 交接目标

该文档用于让后续 AI 直接接手当前仓库，而不是重复调查仓库结构。以下内容已根据当前工作区重新核对。

## 工作模式强约束

1. 当前仓库迁移工作处于无人值守模式。后续 AI 应主动持续推进总目标“完成整个 WPF 仓库的重组迁移”，不要等待用户逐步下发子任务。
2. 不要把工作收缩成“只验证一个点”或“只更新文档”。只要没有遇到已验证的真实阻塞，就应继续在当前主线上迭代处理下一个问题。
3. 命令行 `msbuild` 成功不等于仓库迁移工作已经完成，也不等于 Visual Studio 解决方案状态健康；必须主动检查 `.sln` 纳管状态、IDE 项目加载状态、项目引用与生成链是否一致。
4. 若一次工作最终只产生文档修改，必须视为例外情况，并在文档中明确写出未继续推进代码/项目迁移的原因与证据。

## 本次反思形成的教训

1. 只做文档同步而没有继续推进迁移，是错误做法。后续 AI 在确认基线后，应直接进入下一个可落地迁移点，而不是在局部验证通过后结束。
2. 只看命令行构建结果是不够的。后续 AI 必须同时检查 `Microsoft.Dotnet.Wpf.sln` 声明的项目与 IDE 实际已加载项目是否一致，不能忽视解决方案中的加载失败或未加载状态。
3. 若当前最高优先级问题暂时没有立即修复，也应继续在同阶段内寻找下一个可推进任务，保持长时间、连续迭代，而不是把一次工作缩短成一次状态记录。
4. 后续 AI 的默认结束条件不应是“已经更新了文档”，而应是“已经推进了迁移”或“已经确认了无法继续推进的真实技术阻塞并留下了充分证据”。

## 当前已验证事实

### 解决方案与构建基础设施

- 当前解决方案入口：`Microsoft.Dotnet.Wpf.sln`
- 当前 SDK：`8.0.206`
- 当前统一目标框架：`.NET 8`
- 当前关键属性：
  - `WpfSourceDir=src\Microsoft.DotNet.Wpf\src\`
  - `WpfSharedDir=src\Microsoft.DotNet.Wpf\src\Shared\`
  - `WpfCommonDir=src\Microsoft.DotNet.Wpf\src\Common\`
  - `WpfCycleBreakersDir=cycle-breakers\`
  - `WpfCodeGenDir=eng\WpfArcadeSdk\tools\`
- 当前仓库仍依赖本地 `C:\lindexi\Lib\Microsoft.WindowsDesktop.App\` 路径。

### 当前仓库已有的顶层目录

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

### 当前解决方案已纳入的项目

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
- `PresentationFramework.Classic`
- `PresentationFramework.Aero`
- `PresentationFramework.Aero2`
- `PresentationFramework.AeroLite`
- `PresentationFramework.Fluent`
- `PresentationFramework.Luna`
- `PresentationFramework.Royale`
- `System.Windows.Controls.Ribbon`
- `WindowsFormsIntegration`
- `PresentationBuildTasks`
- `mcwpf`

### 当前磁盘已存在但尚未纳入解决方案的主要项目

- `System.Printing`
- 大部分 `ref/*.csproj`
- `cycle-breakers/*.csproj`

### 当前已确认存在的 cycle-breaker 项目

- `PresentationFramework-PresentationUI-api-cycle`
- `PresentationFramework-ReachFramework-impl-cycle`
- `PresentationFramework-System.Printing-api-cycle`
- `PresentationFramework-System.Printing-impl-cycle`
- `PresentationUI-PresentationFramework-impl-cycle`
- `ReachFramework-PresentationFramework-api-cycle`
- `ReachFramework-System.Printing-api-cycle`
- `System.Printing-PresentationFramework-api-cycle`

### 当前尚未进入顶层目录的关键模块

- `PenImc`
- `System.Windows.Presentation`
- `WpfGfx`

## 最新构建结果

### 当前解决方案构建成功

对当前工作区重新执行整体构建后，当前解决方案入口可构建：

- 命令：`msbuild C:\lindexi\Code\God\WpfReorganize\Microsoft.Dotnet.Wpf.sln -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal`
- 结果：构建成功。
- 剩余警告：`DirectWriteForwarder.vcxproj` 报告 `D9035`，即 `/Zc:forScope-` 已否决并将在将来版本中移除。

Visual Studio 默认 `Any CPU` 入口也已验证：

- 命令：`msbuild C:\lindexi\Code\God\WpfReorganize\Microsoft.Dotnet.Wpf.sln -restore /p:Configuration=Debug /p:Platform="Any CPU" /m:1 /v:minimal /clp:ErrorsOnly`
- 结果：构建成功。
- `Any CPU` 下通过 `WpfNativePlatform=x64` 解析显式完整 `PresentationFramework` 输出，避免 `PresentationUI`、`WindowsFormsIntegration` 找不到完整控件/API。
- 构建期间不断弹出打开 `.pl` 文件的问题已定位为缺少 Perl 时直接执行 `.pl` 脚本导致 Windows 文件关联接管。当前 `VerifyPerlCommand` 会在需要运行主题生成脚本时检测 `PerlCommand`，缺少 Perl 时输出警告并跳过脚本，不再弹窗。若需要重新生成主题 XAML，安装 Perl 或设置 `PerlCommand`。

### 这意味着什么

1. 当前解决方案在纳入 `ReachFramework`、`PresentationFramework`、`PresentationUI`、`System.Windows.Controls.Ribbon` 与全部现有主题实现项目后仍可构建。
2. `UIAutomationClientSideProviders` 下游 `CS0006` 没有复现；`UIAutomationClient` 独立构建可通过。
3. 后续重点应放在 `ReachFramework` / `PresentationFramework` 主链，而不是继续排查已恢复的 `UIAutomationClient` 问题。
4. 当前主链阻塞集中在 cycle-breaker API 边界：`ReachFramework-ref` 需要 `XpsDocumentWriter` / `ISerializerFactory` 等 `PresentationFramework` API，但将相关类型放入 `ReachFramework` bridge 后又会引发与 `PresentationFramework` / `System.Printing` 的同名类型冲突。
5. `ReachFramework-ref` 已通过 `PresentationFramework-System.Printing-api-cycle` 补齐 `XpsDocumentWriter` / `ISerializerFactory` 并可独立构建。
6. `ReachFramework` 实现项目已可独立构建；当前通过动态调用边界绕开 `XpsSerializerWriter` 与 `XpsDocument` 调用 `XpsDocumentWriter` 时的 `PrintTicket` / `XpsDocument` 类型身份不一致。
7. `PresentationFramework` 实现项目已可独立构建；当前通过动态调用边界绕开打印相关 `XpsDocumentWriter`、`SerializerWriter`、`ISerializerFactory` 与 `PresentationUI` 中 `FindToolBar` 的迁移边界阻塞。
8. `PresentationUI` 实现项目已可独立构建；当前通过 `System.Printing-ref` 绕过 `System.Printing` C++/CLI 实现项目，并显式引用完整 `PresentationFramework` 输出，避免 `PresentationFramework-System.Printing-api-cycle` 同名程序集覆盖完整控件 API。
9. `System.Printing-ref` 已移除 `System.Windows.Xps.Packaging.XpsDocument` 占位，避免 `PresentationUI` 同时从 `ReachFramework` 与 `System.Printing` 解析同名类型。
10. `PresentationFramework.Classic`、`PresentationFramework.Aero`、`PresentationFramework.Aero2`、`PresentationFramework.AeroLite`、`PresentationFramework.Fluent`、`PresentationFramework.Luna`、`PresentationFramework.Royale` 与 `System.Windows.Controls.Ribbon` 已可独立构建并已纳入解决方案。当前通过显式完整 `PresentationFramework` x64 输出补齐主题和 Ribbon 所需控件 API。
11. `BuildInfo.SystemWindowsControlsRibbon` 当前使用 WCP 公钥，使 `PresentationCore` / `PresentationFramework` 对 Ribbon 的友元访问声明与当前输出程序集强命名一致。
12. `System.Printing` C++/CLI 实现项目已进一步收敛：当前已把 `PresentationFramework-System.Printing-impl-cycle` / `ReachFramework` 实现程序集引用改为打印专用 API bridge，并通过 `ForcedUsingFiles` 显式引入 `System.IO.Packaging.dll`。此前的 `SafeMemoryHandle`、`PrintQueue` 等类型重定义和 `System.IO.Packaging` 缺失已不再是首个失败点；当前源码编译首先卡在 ReachFramework bridge 缺少 `PackageSerializationManager`、`XpsSerializationManager`、`XpsSerializationManagerAsync`、`XpsOMSerializationManager`、`XpsOMSerializationManagerAsync`、`NgcSerializationManager`、`NgcSerializationManagerAsync` 等序列化管理器 API。
13. `ReachFramework-System.Printing-api-cycle` 已新增 `System.Windows.Xps.Serialization.SerializationManagers.cs`，用最小 bridge 方式补齐 `PackageSerializationManager`、`BasePackagingPolicy`、`XpsSerializationManager` / `Async`、`XpsOMSerializationManager` / `Async`、`NgcSerializationManager` / `Async`、`MXDWSerializationManager` 及部分 RCW 声明；bridge 项目本身已重新验证可独立构建。
14. `System.Printing` 当前首个失败点已前移：缺失 ReachFramework 序列化管理器 API 已不再是首个阻塞，当前先卡在 `PackagingProgressEventArgs` bridge 缺少 `Action` / `NumberCompleted`、缺少 `PrintingCanceledException` 与 `System.Printing.Interop` 相关声明，以及 `XpsDocumentWriter` / `XpsDocumentNotificationLevel` 与当前 `System.Printing` 自带头文件中的同名声明冲突。
15. `WindowsFormsIntegration` 当前已纳入解决方案并随解决方案入口构建通过，且已重新验证可独立构建；后续仍需继续收敛其对完整 `PresentationFramework` x64 输出的显式依赖。
16. `PresentationBuildTasks` 已完成从 `net9.0` 到 `net8.0` 的目标框架调整，当前已纳入解决方案并可独立构建。
17. `Shared/Tracing/mcwpf` 已改写为 SDK 风格项目，移除对 `Microsoft.DevDiv.Settings.targets` / `Microsoft.DevDiv.targets` 的依赖，当前已纳入解决方案并可独立构建。

## 建议起手顺序

1. 先阅读：
   - `Docs/README.md`
   - `Docs/00-overview.md`
   - `Docs/01-phase-plan.md`
   - `Docs/cycle-breaker.md`
   - `Microsoft.Dotnet.Wpf.sln`
   - `Directory.Build.props`
   - `Directory.Build.targets`
2. 先用上述 `msbuild` 命令确认解决方案基线仍可构建；若再次出现 `.sln` 解析失败，优先检查 `NestedProjects` 是否引用了不存在的 solution folder GUID。
3. 再核对 `Microsoft.Dotnet.Wpf.sln` 中声明的关键项目与 IDE 实际已加载项目是否一致；若存在加载失败、未加载或状态异常，优先把它当作真实阻塞处理并记录。
4. 在完成基线检查后，不要停在文档同步，应直接继续处理当前最高优先级迁移项，且在同一次工作中持续迭代，直到完成实质迁移或遇到已验证阻塞。
5. 继续处理：
   - `ReachFramework` 与 `PresentationFramework` 的动态边界收敛
    - `PresentationUI` 的 XAML partial 占位替换为真实标记编译生成链路
   - `System.Printing` C++/CLI 类型重定义与 `System.IO.Packaging` 引用缺失
    - `PresentationFramework` / `PresentationUI` / `WindowsFormsIntegration` / 主题链路的完整 API 引用与同名 bridge 解析顺序
6. 排查 `ReachFramework` 时重点检查：
   - `PresentationFramework-ReachFramework-impl-cycle` 与 `PresentationFramework-System.Printing-api-cycle` 的同名 `PresentationFramework.dll` 引用是否被 MSBuild 去重。
   - `XpsDocumentWriter` / `ISerializerFactory` 已由 `PresentationFramework-System.Printing-api-cycle` 暴露给 `ReachFramework-ref`，但实现项目不应同时引用会造成 `XpsDocumentWriter` 或 `PrintTicket` 重复暴露的 bridge 输出。
   - `PrintTicket`、`PrintTicketLevel`、`FixedDocumentSequence`、`SerializerWriter`、`XpsDocument` 是否同时从多个同名程序集暴露。
7. 排查 `PresentationFramework` 时重点检查：
   - 打印相关 `XpsDocumentWriter`、`SerializerWriter`、`ISerializerFactory` 是否可以通过更明确的桥接项目或引用顺序替代动态调用。
   - `FindToolBar` 当前仍位于 `PresentationUI`，但 `PresentationFramework` 的 `DocumentViewer`、`FlowDocumentReader`、`FlowDocumentScrollViewer`、`SinglePageViewer` 与 `DocumentViewerHelper` 会直接使用该类型。
8. 排查 `System.Printing` 时新增重点检查：
   - `src/Microsoft.DotNet.Wpf/src/System.Printing/System.Printing.vcxproj` 当前已改为引用 `PresentationFramework-System.Printing-api-cycle` 与 `ReachFramework-System.Printing-api-cycle`，不要直接回退到完整 `ReachFramework` 或 `PresentationFramework-System.Printing-impl-cycle`，否则会重新引入 `SafeMemoryHandle` / `PrintQueue` 等类型重定义。
    - 继续在 `cycle-breakers/ReachFramework/System.Windows.Xps.Serialization.SerializationManagers.cs` 基础上补齐剩余最小 bridge API，优先处理 `PackagingProgressEventArgs.Action` / `NumberCompleted`、`PrintingCanceledException`、`System.Printing.Interop`、`RCW::PrintDocumentPackageStatusProvider`。
    - 继续收敛 `XpsDocumentWriter` / `XpsDocumentNotificationLevel` 的同名类型来源，避免 bridge 与 `System.Printing` 自带头文件同时暴露同名声明。
    - `cycle-breakers/PresentationFramework/System.Windows.Controls.PrintDialog.cs` 与 `cycle-breakers/ReachFramework/System.Windows.Xps.Serialization.XpsDocumentEvent.cs` / `System.Printing.PrintTicketManager.cs` / `System.Windows.Xps.Serialization.SerializationManagers.cs` 是当前已新增的最小 bridge 占位，应在此基础上继续补齐，而不是另起新的 bridge 方向。
9. 排查 `PresentationUI` 时重点检查：
   - `src/Microsoft.DotNet.Wpf/src/PresentationUI/PresentationUI.csproj` 中显式完整 `PresentationFramework` 引用是否仍需要保留，是否可以改为更稳定的项目引用输出顺序。
   - `InstallationError.xaml.cs`、`TenFeetInstallationError.xaml.cs`、`TenFeetInstallationProgress.xaml.cs` 与 `MS/Internal/Documents/FindToolBar.xaml.cs` 中的 XAML partial 占位应由真实标记编译产物替换。
   - `System.Printing-ref` 是否仍会通过 `PresentationFramework-System.Printing-api-cycle` 带入同名 `PresentationFramework.dll`，覆盖完整实现程序集。
10. 排查 `System.Printing` 时原有重点继续保留：
   - `CPP/Win32Inc.hpp` 和各头文件中的 `#using` 是否同时引入 ref、impl 与 bridge 中的同名类型。
   - `System.IO.Packaging` 是否需要通过 C++/CLI 项目的 `Reference`、`AdditionalPackageReference` 或显式 `/FU` 进入编译。
11. 排查 Ribbon 与主题项目时重点检查：
   - 显式完整 `PresentationFramework` 输出引用是否可替换为更稳定的项目引用输出顺序。
   - `BuildInfo.SystemWindowsControlsRibbon` 使用 WCP 公钥后是否会影响后续与原始仓库同步。
11. 只有在上层主链持续稳定后，再继续补缺失顶层模块：
   - `PenImc`
   - `System.Windows.Presentation`
   - `WpfGfx`

## 推荐命令方向

- 解决方案或托管项目：`msbuild <project-or-sln> -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal`
- 涉及 `DirectWriteForwarder` 等 native 项目时，优先使用 `msbuild`，不要默认使用 `dotnet msbuild`。

## 当前需要持续遵守的约束

- 禁止再次通过把 `eng/Versions.props` 中的 `AssemblyVersion` 改成 `4.0.0.0` 来掩盖 `WindowsBase` 或其他同名程序集冲突。
- 若出现 `Rect`、`Point`、`DependencyObject` 等基础类型错误，先检查是否同时引用了仓库项目和 SDK inbox 程序集。
- 不要通过把项目从解决方案中移除来制造“构建通过”。

## 结束前必须回写的信息

- 当前已验证的构建入口与命令
- 当前首个真实失败点
- 当前是否存在 `.sln` 已声明但 IDE 加载失败/未加载的项目
- 新增项目、目录或项目纳管变化
- 当前仍未解决的阻塞
- 后续 AI 开始时应该先做什么




## 最近完成的工作

### PresentationBuildTasks 迁移完成

- 问题:项目面向 `net9.0`,但当前 SDK 为 `8.0.206`,导致 `NETSDK1045` 错误。
- 解决:将目标框架从 `net472;net9.0` 改为 `net472;net8.0`。
- 状态:已加入解决方案,可独立构建,解决方案入口构建通过。

### mcwpf (事件跟踪代码生成工具) 现代化完成

- 问题:旧非 SDK 风格项目,导入不存在的内部构建系统路径。
- 解决:改写为 SDK 风格项目,禁用自动生成 AssemblyInfo,排除模板文件 `wpf_template.cs`。
- 状态:已加入解决方案,可独立构建,解决方案入口构建通过。

### WindowsFormsIntegration 已加入解决方案

- 状态:已加入解决方案,随解决方案入口构建通过。后续需继续收敛对完整 `PresentationFramework` x64 输出的显式依赖。

