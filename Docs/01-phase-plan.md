# 分阶段计划

## 总体策略

整个重组工作建议按“先建立可持续迁移框架，再逐步纳入关键模块，最后收敛解决方案构建”的顺序执行。每个阶段都必须留下可被下一次 AI 对话直接继承的结果记录。

## 当前已明确的迁移项目顺序

该顺序用于指导“下一批先迁什么”，并且会随着真实构建结果持续调整：

1. 先纳管当前仓库已经存在但尚未完全进入解决方案主线的项目：
   - `UIAutomationClient`
   - `UIAutomationClientSideProviders`
2. 再迁入上层托管主线模块：
   - `PresentationFramework`
   - `WindowsFormsIntegration`
   - `ReachFramework`
   - `Themes`
3. 再迁入构建任务与扩展功能模块：
   - `PresentationBuildTasks`
   - `System.Windows.Controls.Ribbon`
   - `System.Printing`
   - `PresentationUI`
   - `Extensions`
4. 最后处理底层或 native 依赖更重的模块：
   - `PenImc`
   - `System.Windows.Presentation`
   - `WpfGfx`

---

## 阶段 0：基线确认

### 目标

确认当前仓库、原始仓库和构建基础设施的真实状态，避免后续迁移建立在错误假设上。

### 已完成

- 已确认当前仓库部分顶层目录和项目文件。
- 已确认原始仓库 `origin/src/src/` 的顶层模块列表。
- 已建立 Docs 文档目录用于后续交接。
- 已确认当前仓库根目录存在 `Microsoft.Dotnet.Wpf.sln`。

### 后续输出

- 一份稳定的“已迁移模块清单”。
- 一份稳定的“未迁移模块清单”。
- 一份“解决方案入口现状”说明。
- 一份持续更新的“迁移项目顺序”清单。

---

## 阶段 1：解决方案入口与项目清单收敛

### 目标

让仓库具备一个明确的解决方案级入口，使后续迁移工作可以围绕一个统一的构建目标推进。

### 任务

1. 以 `Microsoft.Dotnet.Wpf.sln` 作为当前统一入口继续推进。
2. 梳理当前所有 `*.csproj`、`ref/*.csproj` 与解决方案之间的对应关系。
3. 先把当前目录中已存在且依赖相对闭合的项目纳入解决方案。
4. 记录哪些项目暂时保留在目录中但不纳入首批构建，以及阻塞原因。

### 完成标准

- 有一个明确的解决方案文件路径。
- 有一份项目纳管清单。
- 能说明每个未纳入项目的原因。
- 至少完成一批“现存项目纳入解决方案”的实际迁移。

### 风险

- 原始 WPF 解决方案可能依赖尚未迁入的项目或原始目录假设。
- 直接照搬原始解决方案可能会引入大量失效路径。
- 部分现存项目虽然源码已在仓库中，但其依赖链仍可能指向尚未迁入的模块。

---

## 阶段 2：核心托管依赖链收敛

### 目标

围绕已迁入的核心项目，先建立一个尽可能小但可持续扩展的托管依赖链。

### 任务

1. 验证 `WindowsBase`、`System.Xaml`、`PresentationCore` 当前构建状态。
2. 验证 `UIAutomation` 系列项目的引用关系是否闭合。
3. 验证 `WindowsFormsIntegration`、`System.Windows.Input.Manipulations` 是否能在当前结构下构建。
4. 记录每个项目的外部依赖来源：
   - 本仓库项目引用
   - 本地 DLL 引用
   - SDK/框架引用
   - 暂未迁入的源码项目

### 完成标准

- 至少一批核心托管项目可稳定构建。
- 每个失败项目都有明确阻塞记录。

### 当前阶段补充记录

- 已验证当前 `Microsoft.Dotnet.Wpf.sln` 可成功构建。
- 已确认磁盘上的解决方案文件尚未纳入 `UIAutomationClient`、`UIAutomationClientSideProviders`、`PresentationCore`、`WindowsFormsIntegration`。
- 已确认 `UIAutomationClient` 的独立构建会被 `PresentationCore` 阻塞。
- 已确认 `PresentationCore` 当前与 `origin/src/src/PresentationCore/PresentationCore.csproj` 相比仍缺失一批编译项与源码。
- 本轮已开始按原始 `PresentationCore.csproj` 的清单补齐缺失文件，而不是直接纳管更多项目。
- 本轮已批量迁入 `PresentationFramework`、`ReachFramework`、`Themes`、`PresentationBuildTasks`、`PresentationUI`、`System.Printing`、`System.Windows.Controls.Ribbon`、`Extensions` 到当前重组目录。
- 已验证 `PresentationFramework` 可进入构建诊断，但当前首先被缺失的 cycle-breaker 项目与 `AvTrace\GenAvMessages.targets` 阻塞。

### 风险

- 共享源码和生成代码路径可能仍依赖原始仓库布局。
- 本地引用路径可能使构建无法脱离个人机器环境。
- 若继续以“先纳管、后补源码”的顺序推进，会反复被 `PresentationCore` 的未闭合依赖链阻塞。

---

## 阶段 3：引入 `PresentationFramework` 及关键缺失模块

### 目标

逐步把最关键的上层模块纳入当前结构，为最终形成完整 WPF 托管构建链打基础。

### 优先顺序建议

1. `PresentationFramework`
2. `WindowsFormsIntegration`（在 `PresentationFramework` 进入后重新验证）
3. `ReachFramework`
4. `Themes`
5. `PresentationBuildTasks`
6. `System.Windows.Controls.Ribbon`
7. `System.Printing`
8. `PresentationUI`
9. `Extensions`
10. `PenImc`
11. `System.Windows.Presentation`
12. `WpfGfx`

### 任务

1. 从 `origin/src/src/` 对照复制或映射目标模块目录结构。
2. 修正 `ProjectReference`、共享源码路径、生成代码输入路径。
3. 按模块记录迁移差异，而不是一次性做大批量不可追踪改动。
4. 每迁入一个模块，就更新 Docs 文档。

### 当前顺序修正

- 在正式进入 `PresentationFramework` 之前，先完成 `PresentationCore` 的缺失源码补齐。
- `WindowsFormsIntegration` 的重新验证仍依赖 `PresentationFramework`，但 `UIAutomationClient`、`UIAutomationClientSideProviders` 的独立构建验证先依赖 `PresentationCore` 完整化。
- 由于 `PresentationFramework` 已经进入当前仓库目录，后续不再是“是否迁入”的问题，而是“先补哪一类构建前置”的问题；优先级应调整为：`PresentationCore` 缺失源码、`PresentationFramework` 代码生成前置、cycle-breaker 项目。

### 完成标准

- `PresentationFramework` 进入当前仓库结构。
- 至少建立其直接依赖项目的迁移顺序。
- 对未完成模块有清晰阻塞说明。

### 风险

- `PresentationFramework` 依赖面极广，容易带出更多未迁入模块。
- 生成资源、主题、任务项目之间可能存在隐式构建顺序要求。
- 当前缺失的不只是源码目录，还包括一批专门用于打断循环依赖的桥接项目和代码生成目标，若不先补这些前置，继续单点修项目会反复受阻。

---

## 阶段 4：解决方案级构建打通

### 目标

让 Visual Studio 构建和命令行 `msbuild` 构建都能围绕统一入口稳定运行。

### 任务

1. 修正解决方案中的项目依赖与构建顺序。
2. 清理失效路径和仅在原始仓库有效的导入。
3. 梳理需要保留的本地依赖与可替换的项目引用。
4. 记录最小可复现构建命令。

### 完成标准

- Visual Studio 中可对目标解决方案发起构建。
- 命令行中有一条明确的 `msbuild` 或 `dotnet msbuild` 命令可复现。

---

## 阶段 5：文档收敛与持续交接

### 目标

确保该重组工程可以在多轮 AI 对话中稳定推进，而不是每次重新调查。

### 每次结束前必须更新

- `00-overview.md`：更新已迁移模块、缺失模块、当前构建状态。
- `01-phase-plan.md`：调整阶段优先级和阻塞情况。
- `02-next-session-handoff.md`：更新建议起手任务和必读上下文。

### 推荐记录格式

每次工作结束时至少补充以下内容：

- 本轮新增或修改的项目/目录
- 本轮验证过的构建命令或构建入口
- 当前失败点
- 下一轮最小可执行任务
- 若中断，下一轮先读哪些文件
