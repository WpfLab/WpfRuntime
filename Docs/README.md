# WPF 重组计划文档

本文档目录用于记录当前仓库的重组状态、构建现状和交接信息，目标是让后续 AI 对话可以直接接手，而不是重新调查一遍仓库。

## 文档列表

- [00-overview.md](00-overview.md)：当前仓库的已验证事实、解决方案现状、主要缺口。
- [01-phase-plan.md](01-phase-plan.md)：后续执行顺序、每个阶段的目标、完成标准与风险。
- [02-next-session-handoff.md](02-next-session-handoff.md)：给后续 AI 直接使用的起手顺序、必读文件、当前阻塞点。
- [03-origin-diff-audit.md](03-origin-diff-audit.md)：当前仓库与 `origin` 原始 WPF 代码的差异审计、潜在风险与优先收敛方向。
- [04-NuGet-Binary.md](04-NuGet-Binary.md)：`PenImc` 和 `WpfGfx` 等 native 模块的 NuGet 二进制 DLL 接入方案。
- [05-builder-plan.md](05-builder-plan.md)：Builder 构建器项目完善计划，驱动构建与 NuGet 打包。
- [backlog.md](backlog.md)：后备待办记录，用于保存工作过程中发现但不属于当前优先任务范围的问题。
- [cycle-breaker.md](cycle-breaker.md)：`PresentationFramework` / `ReachFramework` / `System.Printing` / `PresentationUI` 之间桥接项目的保留原因与使用建议。

## 建议阅读顺序

1. 先阅读 `Docs/README.md`，了解文档结构和维护规则。
2. 再阅读 `00-overview.md`，确认当前磁盘状态、解决方案纳管状态和最新构建结论。
3. 再阅读 `01-phase-plan.md`，确认下一步应该先解决什么问题。
4. 阅读 `03-origin-diff-audit.md`，了解当前仓库与 `origin` 的结构差距、迁移性补丁和优先收敛方向。
5. 如果需要处理 native 模块依赖或 NuGet 打包，阅读 `04-NuGet-Binary.md` 和 `05-builder-plan.md`，了解 binary 接入方案和 Builder 构建器计划。
6. 阅读 `backlog.md`，了解已经记录但暂不打断当前迁移顺序的后备待办。
7. 如果准备处理 `PresentationFramework`、`ReachFramework`、`System.Printing` 或 `PresentationUI`，继续阅读 `cycle-breaker.md`。
8. 开始新的 AI 对话前，将 `02-next-session-handoff.md` 作为交接输入基础。

## 更新规则

- 只记录已经验证过的事实；未验证内容必须明确写成“待确认”或“不确定”。
- 若新的构建结果与旧记录冲突，应直接改写旧结论，不要把互相矛盾的描述同时保留。
- 优先记录以下信息：
  - 当前解决方案实际纳入的项目
  - 当前磁盘上已存在但尚未纳管的项目
  - 最新构建入口、构建命令和首个真实失败点
  - 项目引用、cycle-breaker、共享目录、代码生成链的变化
- 每次推进后都要更新“当前状态”“阻塞点”“下一步建议”。
- 文档中不要写“本轮”“本次”之类依赖上下文轮次的描述，应直接写当前状态和后续动作。
- 工作过程中发现的问题若不属于当前优先任务范围，应记录到 `backlog.md`，不要打断当前迁移顺序。

## 当前维护原则

1. 整个 WPF 仓库非常大，不要试图一次性加载全部文件，优先围绕当前阻塞模块收集上下文。
2. 对 `origin/src` 原始文件的迁移，优先采用拷贝到当前仓库的方式，不要让当前解决方案长期直接引用 `origin/src`。
3. 涉及 native/WPF 主链构建时，优先使用 `msbuild`，不要默认使用 `dotnet msbuild`。
4. 不要通过把项目从解决方案中移除来制造“构建通过”；应尽量把真实项目纳入解决方案并修复构建。
5. 若仓库中已经存在 `WindowsBase.csproj`、`PresentationCore.csproj`、`PresentationFramework.csproj` 等同名项目，排查构建错误时必须先检查是否又从 SDK 隐式框架引用拿到了第二份同名程序集。

## 无人值守执行原则

1. 当前仓库迁移任务默认处于无人值守模式。后续 AI 不应等待用户把任务拆细，而应主动围绕总目标“完成整个 WPF 仓库的重组迁移”持续推进。
2. 一次工作中不应只做文档整理、状态复述或单点验证；只要没有遇到已验证的真实阻塞，就应继续选择下一个最高优先级迁移点并迭代推进。
3. 不要因为某个局部验证通过就结束工作。命令行 `msbuild` 成功只说明一部分问题已经通过，不能替代对解决方案纳管状态、Visual Studio 项目加载状态、主链迁移缺口的继续排查。
4. 每次开始时，除检查命令行构建外，还应检查 `Microsoft.Dotnet.Wpf.sln` 声明的项目与 IDE 实际已加载项目是否一致；若发现项目加载失败、未加载或解决方案清单与 IDE 状态不一致，应优先记录并处理，不能视而不见。
5. 只有在已经尝试推进代码、项目或构建迁移并遇到明确阻塞时，才允许以文档更新作为阶段性收尾。若最终只更新了文档，必须在文档中写清：已经尝试了哪些迁移动作、为什么无法继续、当前首个真实阻塞点是什么、下一次应从哪里接着做。
6. 结束一次工作前，优先确保至少完成以下事项之一：
   - 修复一个真实构建/加载阻塞
   - 完成一个项目或模块的迁移/纳管
   - 收敛一个 cycle-breaker、引用顺序或代码生成链问题
   - 明确验证并记录一个无法继续推进的真实技术阻塞

## 命令使用提示

- 推荐命令形态：`msbuild <project-or-sln> -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal`
- 不要调用 `C:\Program Files\Microsoft Visual Studio\2026\Professional\MSBuild\Current\Bin\MSBuild.exe`。
- 若必须写完整路径，使用 Visual Studio 18 安装路径；否则直接使用 `msbuild`。
