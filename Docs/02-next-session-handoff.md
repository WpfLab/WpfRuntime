# 下一次 AI 对话交接

## 当前工作产出

本次已从“仅做文档基线”推进到“开始真实构建验证与缺失源码补齐”，目的是让后续迁移围绕一个真实可用的解决方案入口和可独立构建的项目链持续前进。

已完成：

- 确认当前仓库根目录存在 `Microsoft.Dotnet.Wpf.sln`。
- 核对了磁盘上解决方案当前已纳入项目。
- 验证当前 `Microsoft.Dotnet.Wpf.sln` 可以成功构建。
- 验证 `UIAutomationClient` 独立构建时会被 `PresentationCore` 阻塞。
- 对照当前仓库与 `origin/src/src/`，确认关键缺失模块仍未迁入。
- 对照 `origin/src/src/PresentationCore/PresentationCore.csproj` 与当前 `PresentationCore.csproj`，确认当前 `PresentationCore` 缺失一批源码与编译项。
- 已从 `origin/src/src/PresentationCore/` 拷贝首批 `TextInterface`、`Interop/DWrite`、`BinaryFormat`、`UISettings` 相关源码，并同步更新 `PresentationCore.csproj`。
- 已从 `origin/src/src/` 批量拷贝 `PresentationFramework`、`ReachFramework`、`Themes`、`PresentationBuildTasks`、`PresentationUI`、`System.Printing`、`System.Windows.Controls.Ribbon`、`Extensions` 到当前重组目录。
- 已验证 `PresentationFramework` 当前可以开始进入真实构建诊断，但先被缺失的 cycle-breaker 项目与 `AvTrace\GenAvMessages.targets` 阻塞。
- 已在仓库根目录新增 `cycle-breakers/` 目录，并补齐首批桥接项目与最小占位源码。
- 已在 `Directory.Build.props` 中新增 `WpfCycleBreakersDir`、`WpfCodeGenDir`。
- 已将 `PresentationFramework.csproj` 中的 `GenAvMessages.targets` 调整为条件导入，解除项目评估阶段的导入失败。
- 已将 `PresentationCore.csproj` 恢复为引用仓库内 `DirectWriteForwarder.vcxproj`，不再依赖外部 `DirectWriteForwarder.dll` 文件引用。
- 已将 `PresentationCore`、`UIAutomationClient`、`UIAutomationClientSideProviders`、`WindowsFormsIntegration` 纳入 `Microsoft.Dotnet.Wpf.sln`。
- 已验证根解决方案在纳入上述项目后仍可成功构建。
- 已确认 `DirectWriteForwarder.vcxproj` 在 Visual Studio 自带 `MSBuild.exe` 下可成功构建，`dotnet msbuild` 失败点是 VC++ 跟踪任务 `GetOutOfDateItems` / `FileTracker` 的主机兼容性问题。
- 已为 `PresentationCore.csproj` 补充 `System.Formats.Nrbf` 依赖与 `SYSLIB5005` 抑制，独立构建已越过 DirectWriteForwarder 阶段。
- 已确认此前将 `eng/Versions.props` 中 `AssemblyVersion` 改成 `4.0.0.0` 是错误修复。该做法只是把仓库内 `WindowsBase` 伪装成 inbox `WindowsBase`，没有消除双引用。
- 已通过 `PresentationCore` 的 `ResolveReferences` 日志确认：当前同时解析到仓库输出 `artifacts/obj/WindowsBase/Debug/net8.0/ref/WindowsBase.dll` 与 .NET 8 引用包 `Microsoft.NETCore.App.Ref/.../WindowsBase.dll`。
- 已将 `AssemblyVersion` 恢复为 `$(MajorVersion).$(MinorVersion).$(PatchVersion).0`，并明确后续禁止再用改版本号的方式处理 `WindowsBase` / BCL 冲突。
- 已确认 `PresentationCore` 的下一步重点不是继续改版本号，而是收敛 `WindowsBase` 引用来源；此外仍需处理 `WindowsBase` 引用程序集复制阶段的文件锁（`MSB3883`，目标文件为 `artifacts/obj/WindowsBase/Debug/net8.0/ref/WindowsBase.dll`）。
- 已从 `origin/src/cycle-breakers/` 补齐 `PresentationFramework-System.Printing-impl-cycle.csproj` 并加入 `Microsoft.Dotnet.Wpf.sln`。
- 已在 `Directory.Build.targets` 中加入公共 `ResolveReferences` 后处理逻辑：当项目已直接引用仓库内 `WindowsBase.csproj` 时，移除来自 `Microsoft.NETCore.App.Ref` 的 inbox `WindowsBase.dll` 编译引用。
- 已重新使用 Visual Studio 自带 `MSBuild.exe` 验证 `PresentationCore`；此前由 `WindowsBase` 双来源触发的 `Rect`、`Point`、`IRawElementProviderFragment` 相关 `CS7069` / `CS9333` 已消失，说明 `PresentationCore` 的主构建阻塞已不再是 `WindowsBase` 类型冲突。
- 已再次使用 `msbuild` 验证 `PresentationCore` 独立构建通过；此前记录的 `UISettingsRcw` / `TypefaceMap` 错误已不再复现。
- 已在 `Directory.Build.props` / `Directory.Build.targets` 中补齐 `MicrosoftPrivateWinFormsReference` 到 .NET 8 WindowsDesktop 参考包 `Accessibility.dll` 的解析逻辑。
- 已修正 `Shared/MS/Win32/UnsafeNativeMethodsCLR.cs` 中 `IAccessible` 类型歧义，避免 `WindowsBase` 同时看到本地互操作占位类型与 `Accessibility.IAccessible` 时编译失败。
- 已验证 `UIAutomationClient`、`UIAutomationClientSideProviders` 独立构建通过。
- 已将 `UIAutomationClient`、`UIAutomationClientSideProviders` 加入 `Microsoft.Dotnet.Wpf.sln`，并验证根解决方案可成功构建。
- 已推进 `PresentationFramework` 构建链：`System.Printing-ref`、`PresentationFramework-ReachFramework-impl-cycle`、`PresentationFramework-System.Printing-api-cycle` 已可独立构建通过。
- 已为 `PresentationFramework` cycle-breaker 补齐 `System.Windows.Window`、`System.Windows.Documents.Serialization.ISerializerFactory`、`System.Windows.Xps.XpsDocumentWriter` 等最小占位类型。
- 已调整 `System.Printing-ref` 和 `ReachFramework-ref` 的部分桥接引用，使其可以引用已生成的 cycle-breaker 输出，避免循环项目引用在编译期遮蔽所需类型。

## 当前已知事实

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

### 当前仓库已确认存在的主要项目

- `src/Microsoft.DotNet.Wpf/src/WindowsBase/WindowsBase.csproj`
- `src/Microsoft.DotNet.Wpf/src/System.Xaml/System.Xaml.csproj`
- `src/Microsoft.DotNet.Wpf/src/PresentationCore/PresentationCore.csproj`
- `src/Microsoft.DotNet.Wpf/src/UIAutomation/UIAutomationTypes/UIAutomationTypes.csproj`
- `src/Microsoft.DotNet.Wpf/src/UIAutomation/UIAutomationProvider/UIAutomationProvider.csproj`
- `src/Microsoft.DotNet.Wpf/src/UIAutomation/UIAutomationClient/UIAutomationClient.csproj`
- `src/Microsoft.DotNet.Wpf/src/UIAutomation/UIAutomationClientSideProviders/UIAutomationClientSideProviders.csproj`
- `src/Microsoft.DotNet.Wpf/src/WindowsFormsIntegration/WindowsFormsIntegration.csproj`
- `src/Microsoft.DotNet.Wpf/src/System.Windows.Input.Manipulations/System.Windows.Input.Manipulations.csproj`
- `src/Microsoft.DotNet.Wpf/src/DirectWriteForwarder/DirectWriteForwarder.vcxproj`

### 当前解决方案已纳入的项目

- `System.Xaml`
- `WindowsBase`
- `System.Windows.Input.Manipulations`
- `UIAutomationTypes`
- `UIAutomationProvider`
- `PresentationCore`
- `UIAutomationClient`
- `UIAutomationClientSideProviders`
- `WindowsFormsIntegration`
- `DirectWriteForwarder`
- `Docs`
- `Demo/WpfDemo`

### 当前已确认未纳入解决方案但已存在于目录中的主要项目

- `PresentationFramework`
- `ReachFramework`
- `Themes` 相关项目
- `PresentationBuildTasks`
- `PresentationUI`
- `System.Printing`
- `System.Windows.Controls.Ribbon`

### 当前已列明的迁移顺序

1. 先收敛已存在项目：
   - `UIAutomationClient`
   - `UIAutomationClientSideProviders`
2. 再迁入上层主线：
   - `PresentationFramework`
   - `WindowsFormsIntegration`
   - `ReachFramework`
   - `Themes`
3. 再迁入构建任务和扩展模块：
   - `PresentationBuildTasks`
   - `System.Windows.Controls.Ribbon`
   - `System.Printing`
   - `PresentationUI`
   - `Extensions`
4. 最后处理底层或 native 依赖较重模块：
   - `PenImc`
   - `System.Windows.Presentation`
   - `WpfGfx`

### 尚未进入当前重组顶层目录的关键模块

- `PenImc`
- `System.Windows.Presentation`
- `WpfGfx`

### 已知不确定点

1. 当前解决方案本身可以构建，但这不代表目录内未纳管项目也可独立构建。
2. `WindowsFormsIntegration` 仍依赖尚未迁入的 `PresentationFramework`。
3. `PresentationCore` 已纳入根解决方案，且独立构建已通过。
4. `PresentationCore` 当前的主要问题不再是继续寻找额外的 `TextInterface` C# 文件；`DirectWriteForwarder` 需要改用 Visual Studio 自带 `MSBuild.exe` 构建。当前仍需关注 `System.Formats.Nrbf` 版本回退警告、Windows-only API 警告与其他项目中的 `WindowsBase` 双来源警告。
5. 本地引用路径 `C:\lindexi\Lib\Microsoft.WindowsDesktop.App\` 可能影响可移植性和构建重现性。
6. 当前已补齐首批 bridge 项目，并额外补齐了 `PresentationFramework-System.Printing-impl-cycle`；后续仍需继续对照 `origin/src/cycle-breakers/` 与真实构建错误确认是否还有缺口。
7. `PresentationFramework` 对 `$(WpfCodeGenDir)AvTrace\GenAvMessages.targets` 已改为条件导入，但当前仓库仍未接入该目标文件本体，因此代码生成链尚未恢复。
8. 需要继续确认 `DirectWriteForwarder` 在 Visual Studio 内部构建与命令行构建的差异，以及是否需要调整项目配置来规避当前 VC++ 任务异常。
9. `ReachFramework-ref` 的重新验证命令被取消，尚未确认 `System.Windows.Xps.XpsDocumentWriter` 与 `ISerializerFactory` 占位补齐后是否完全通过。

## 下一次对话建议起手顺序

1. 先使用 `msbuild C:\lindexi\Code\God\WpfReorganize\src\Microsoft.DotNet.Wpf\src\ReachFramework\ref\ReachFramework-ref.csproj -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal` 验证 `ReachFramework-ref` 最新状态。
2. 若 `ReachFramework-ref` 通过，立即重新验证 `PresentationFramework.csproj`；若失败，优先继续补齐 `PresentationFramework` / `ReachFramework` / `System.Printing` cycle-breaker 类型或引用来源。
3. 继续检查公共 `WindowsBase` 收敛逻辑在 `UIAutomationTypes`、`UIAutomationProvider`、`ReachFramework-ref`、`System.Printing-ref` 等项目上的表现，确认是否还存在需要单独处理的 `MSB3243` 版本冲突警告，不要再尝试通过修改 `AssemblyVersion` 掩盖冲突。
4. 继续补齐 `AvTrace` 代码生成目标来源，并重新验证 `PresentationFramework`。
5. 把构建失败按“缺文件 / 缺引用 / 缺项目 / 路径不匹配 / 生成步骤缺失 / VC++ 工具链异常 / 双来源程序集冲突”分类记录。
6. 在 `PresentationFramework` 构建链进一步闭合后，再同步判断 `WindowsFormsIntegration` 的阻塞是否解除。

## 下一次对话建议直接复制的提示词

你正在接手 `WpfReorganize` 仓库的 WPF 重组工作。请先阅读：

1. `Docs/README.md`
2. `Docs/00-overview.md`
3. `Docs/01-phase-plan.md`
4. `Docs/02-next-session-handoff.md`

然后优先完成以下事项：

- 验证 `Microsoft.Dotnet.Wpf.sln` 当前纳管项目的构建状态。
- 优先使用 Visual Studio 自带 `MSBuild.exe` 验证 `DirectWriteForwarder` / `PresentationCore`，不要直接用 `dotnet msbuild` 诊断 native 阶段。
- 基于已经加入的公共 `WindowsBase` 收敛逻辑，优先验证 `ReachFramework-ref`，再继续打通 `PresentationFramework` 依赖链。
- 梳理当前仓库项目与解决方案入口的对应关系，尤其是尚未纳入的 `PresentationFramework`、`ReachFramework`、`Themes`、`PresentationBuildTasks`。
- 将发现的阻塞点回写到 Docs 文档中。

## 当前新增禁止事项

- 禁止再次把 `eng/Versions.props` 中的 `AssemblyVersion` 改成 `4.0.0.0` 来掩盖 `WindowsBase` 冲突。
- 禁止看到 `Rect`、`Point`、`DependencyObject` 等类型错误就直接改版本号；必须先检查是否同时引用了两份 `WindowsBase`。

## 结束前必须回写的信息

- 当前已迁移模块列表
- 新增项目或目录
- 验证过的构建入口
- 当前阻塞点
- 后续第一步该做什么


