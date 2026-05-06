# 后备待办记录

该文档用于记录工作过程中发现、但不属于当前优先任务范围的问题。记录在这里的问题不代表当前构建基线失败，也不应打断当前迁移顺序；后续在主链迁移稳定后再逐项评估和处理。

## 记录规则

- 只记录已经观察到的问题、明确的改进方向或需要后续验证的事项。
- 不写依赖对话轮次的描述，例如“本轮”“本次”。
- 每个条目应说明：问题、当前影响、建议处理时机、后续动作。
- 若某个条目已经进入正式阶段计划，应在这里标记为“已转入计划”，不要保留含糊的重复记录。

## 待办事项

### 收敛 Visual Studio 中 `PresentationBuildTasks.dll` 锁文件

- 状态：待后续处理。
- 问题：Visual Studio 工作区“生成解决方案”当前会在 `artifacts\bin\PresentationBuildTasks\Debug\net472\PresentationBuildTasks.dll` 复制阶段报 `MSB3027/MSB3021`，提示文件被多个 `MSBuild.exe` 进程锁定。
- 当前影响：命令行 `msbuild -restore` 已恢复可重复通过，但 IDE 全量构建仍不能视为完全收敛。
- 建议处理时机：在保持当前命令行基线稳定的前提下，单独排查 `Microsoft.WinFX.targets` 对 `PresentationBuildTasks.dll` 的任务程序集加载生命周期，避免再次破坏 `PresentationUI` 的标记编译链。
- 后续动作：优先检查是否可通过隔离 `net472` 任务程序集输出、避免复制到被 `UsingTask` 占用的目标路径、或调整 IDE 构建拓扑来消除锁文件；不要再通过全量清空 `artifacts` 目录来规避。

### 移除 Perl 构建依赖

- 状态：待后续处理。
- 问题：当前主题生成链路仍存在 `ThemeGenerator.pl`、`PreprocessXAML.pl` 等 Perl 脚本依赖。缺少 Perl 时，当前构建通过 `VerifyPerlCommand` 跳过脚本执行并输出警告，避免 Windows 通过 `.pl` 文件关联弹窗。
- 当前影响：解决方案基线可以构建，但如果需要重新生成主题产物，仍需要安装 Perl 或设置 `PerlCommand` 指向有效 Perl 可执行文件。
- 目标：等待项目完成迁移之后，移除整个 WPF 仓库对 Perl 的依赖，使仓库只需 `msbuild` 即可执行构建，不再需要额外工具。
- 建议处理时机：主链项目迁移稳定、主题生成链路和标记编译链路完成梳理之后。
- 后续动作：评估将 Perl 脚本改写为 MSBuild task、托管工具或内置目标；确认生成产物是否应纳入源码、作为中间产物生成，或由新的托管生成器统一输出。
