# WPF 重组计划文档

本文档目录用于记录当前仓库的重组状态、构建现状和交接信息，目标是让后续 AI 对话可以直接接手，而不是重新调查一遍仓库。

## 文档列表

- [00-overview.md](00-overview.md)：当前仓库的已验证事实、解决方案现状、主要缺口。
- [01-phase-plan.md](01-phase-plan.md)：后续执行顺序、每个阶段的目标、完成标准与风险。
- [02-next-session-handoff.md](02-next-session-handoff.md)：给后续 AI 直接使用的起手顺序、必读文件、当前阻塞点。
- [cycle-breaker.md](cycle-breaker.md)：`PresentationFramework` / `ReachFramework` / `System.Printing` / `PresentationUI` 之间桥接项目的保留原因与使用建议。

## 建议阅读顺序

1. 先阅读 `Docs/README.md`，了解文档结构和维护规则。
2. 再阅读 `00-overview.md`，确认当前磁盘状态、解决方案纳管状态和最新构建结论。
3. 再阅读 `01-phase-plan.md`，确认下一步应该先解决什么问题。
4. 如果准备处理 `PresentationFramework`、`ReachFramework`、`System.Printing` 或 `PresentationUI`，继续阅读 `cycle-breaker.md`。
5. 开始新的 AI 对话前，将 `02-next-session-handoff.md` 作为交接输入基础。

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

## 当前维护原则

1. 整个 WPF 仓库非常大，不要试图一次性加载全部文件，优先围绕当前阻塞模块收集上下文。
2. 对 `origin/src` 原始文件的迁移，优先采用拷贝到当前仓库的方式，不要让当前解决方案长期直接引用 `origin/src`。
3. 涉及 native/WPF 主链构建时，优先使用 `msbuild`，不要默认使用 `dotnet msbuild`。
4. 不要通过把项目从解决方案中移除来制造“构建通过”；应尽量把真实项目纳入解决方案并修复构建。
5. 若仓库中已经存在 `WindowsBase.csproj`、`PresentationCore.csproj`、`PresentationFramework.csproj` 等同名项目，排查构建错误时必须先检查是否又从 SDK 隐式框架引用拿到了第二份同名程序集。

## 命令使用提示

- 推荐命令形态：`msbuild <project-or-sln> -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal`
- 不要调用 `C:\Program Files\Microsoft Visual Studio\2026\Professional\MSBuild\Current\Bin\MSBuild.exe`。
- 若必须写完整路径，使用 Visual Studio 18 安装路径；否则直接使用 `msbuild`。
