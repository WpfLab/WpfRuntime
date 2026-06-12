# 后备待办记录

该文档用于记录工作过程中发现、但不属于当前优先任务范围的问题。记录在这里的问题不代表当前构建基线失败，也不应打断当前迁移顺序；后续在主链迁移稳定后再逐项评估和处理。

## 记录规则

- 只记录已经观察到的问题、明确的改进方向或需要后续验证的事项。
- 不写依赖对话轮次的描述，例如“本轮”“本次”。
- 每个条目应说明：问题、当前影响、建议处理时机、后续动作。
- 若某个条目已经进入正式阶段计划，应在这里标记为“已转入计划”，不要保留含糊的重复记录。

## 待办事项

### 收敛 Visual Studio 中 `PresentationBuildTasks.dll` 锁文件

- 状态：代码侧已处理，待在 Visual Studio 中复验。
- 问题：Visual Studio 工作区“生成解决方案”此前会在 `artifacts\bin\PresentationBuildTasks\Debug\net472\PresentationBuildTasks.dll` 复制阶段报 `MSB3027/MSB3021`，提示文件被多个 `MSBuild.exe` 进程锁定。
- 已验证处理：
  - `PresentationBuildTasks.csproj` 在 `BuildingInsideVisualStudio=true` 且 `TargetFramework=net472` 时，改为输出到 `artifacts\ide-bin\PresentationBuildTasks\Debug\net472\PresentationBuildTasks.dll`，避免把 IDE 构建产物直接复制到稳定任务程序集加载路径。
  - 构建结束后仅在稳定路径缺失时，补种一次 `artifacts\bin\PresentationBuildTasks\Debug\net472\PresentationBuildTasks.dll`，避免稳定 `UsingTask` 加载路径与当前 IDE 编译输出争用同一文件。
  - `Microsoft.WinFX.targets` 保留对 `artifacts\bin` 稳定路径的优先加载；仅当稳定路径不存在且处于 IDE 构建时，才回退到 `artifacts\ide-bin` 路径。
- 当前影响：
  - `msbuild src\Microsoft.DotNet.Wpf\src\PresentationBuildTasks\PresentationBuildTasks.csproj -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal` 仍可成功构建。
  - 使用 `/p:BuildingInsideVisualStudio=true /p:TargetFramework=net472` 单独构建 `PresentationBuildTasks` 时，输出已写入 `artifacts\ide-bin\PresentationBuildTasks\Debug\net472\PresentationBuildTasks.dll`，且稳定路径与 IDE 路径可并存，不再需要把正在被任务加载的稳定 DLL 作为本次编译输出目标。
  - 继续以 `/p:BuildingInsideVisualStudio=true` 构建 `PresentationUI` 时，首个失败点已前移为 `MC1000`：`PresentationFramework-PresentationUI-api-cycle` 相关输出路径缺失；未再复现 `PresentationBuildTasks.dll` 复制锁文件错误。
- 后续动作：在 Visual Studio 中再次执行“生成解决方案”确认锁文件问题已消失；若仍有 IDE 构建失败，优先转向排查 `PresentationUI` 标记编译阶段缺失的 `PresentationFramework.dll` 引用路径，而不是回退本条修改。

### 移除 Perl 构建依赖

- 状态：待后续处理。
- 问题：当前主题生成链路仍存在 `ThemeGenerator.pl`、`PreprocessXAML.pl` 等 Perl 脚本依赖。缺少 Perl 时，当前构建通过 `VerifyPerlCommand` 跳过脚本执行并输出警告，避免 Windows 通过 `.pl` 文件关联弹窗。
- 当前影响：解决方案基线可以构建，但如果需要重新生成主题产物，仍需要安装 Perl 或设置 `PerlCommand` 指向有效 Perl 可执行文件。
- 目标：等待项目完成迁移之后，移除整个 WPF 仓库对 Perl 的依赖，使仓库只需 `msbuild` 即可执行构建，不再需要额外工具。
- 建议处理时机：主链项目迁移稳定、主题生成链路和标记编译链路完成梳理之后。
- 后续动作：评估将 Perl 脚本改写为 MSBuild task、托管工具或内置目标；确认生成产物是否应纳入源码、作为中间产物生成，或由新的托管生成器统一输出。

### 迁移妥协代码清理

- 状态：待后续处理。已转入阶段计划（`Docs/01-phase-plan.md` 阶段 4），但具体执行顺序尚未排定。
- 问题：当前仓库存在一批为迁移通过而临时写入的妥协代码，长期保留会掩盖与 origin 的真实偏差：
  - `ReachFramework` bridge 文件：`SafeMemoryHandle.cs`、`PrintQueueBridge.cs`、`DocumentReferenceBridge.cs`
  - `WindowsBase` 独有补丁：`CaseInsensitiveOrdinalStringComparer.cs`
  - `System.Xaml` 独有补丁：`StaticExtensionConverter.cs`
  - `PresentationUI` XAML partial 占位成员（`InstallationError`、`TenFeetInstallationError`、`TenFeetInstallationProgress`、`FindToolBar`）
  - `PresentationFramework` 打印链路动态调用边界
  - 主题项目 / Ribbon / `PresentationUI` / `WindowsFormsIntegration` 对完整 `PresentationFramework` 输出的显式 HintPath
- 当前影响：构建可通过，但代码已与 origin 分叉；新开发人员难以区分临时补丁和真实实现。
- 建议处理时机：在当前主链构建稳定后，按优先级逐项清理。
- 后续动作：
  1. 先恢复 `PresentationUI` 的真实标记编译生成链路，替换 XAML partial 占位
  2. 收敛打印链路动态边界，替换为更接近 origin 的实现
  3. 逐项评估 bridge 文件是否可替换为更稳定的项目引用或 origin 方案
  4. 消除显式 HintPath 引用

### PenImc 和 WpfGfx 的 NuGet 二进制接入

- 状态：待后续处理。方案已记录在 `Docs/04-NuGet-Binary.md`。
- 问题：`PenImc` 和 `WpfGfx` 不再走源码迁移路线，但当前尚未通过 NuGet 包接入已构建 DLL。
- 当前影响：主链编译不直接依赖这些模块的源码，但如果遇到 `DllImport` 缺失或运行时加载问题，需要知道是二进制 DLL 未接入造成的。
- 建议处理时机：在迁移妥协代码清理到一定程度后接入。
- 后续动作：按 `Docs/04-NuGet-Binary.md` 的方案，使用 `GeneratePathProperty` 引用 `Microsoft.WindowsDesktop.App.Runtime` 包。
