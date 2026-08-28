# WPF 重组文档

本目录只维护当前事实、后续阶段和无人值守交接。仓库根入口统一为 [`Microsoft.Dotnet.Wpf.slnx`](../Microsoft.Dotnet.Wpf.slnx)。根目录没有与该入口同名的传统解决方案文件；仓库内其他模块可以保留各自独立的 `.sln`。

## 目录索引

### 核心文档

- [00-overview.md](00-overview.md)：唯一的当前状态事实源，记录项目清单、构建状态、已落地能力、未决项和验证边界。
- [01-phase-plan.md](01-phase-plan.md)：只记录后续阶段、执行动作、完成标准和风险。
- [02-next-session-handoff.md](02-next-session-handoff.md)：只记录接手时的安全检查、首个阻塞、连续推进顺序和停止条件。

### 活跃专题

- [03-origin-diff-audit.md](03-origin-diff-audit.md)：维护与 `origin` 的已验证差异、迁移妥协和收敛依据，不承担仓库整体状态汇总。
- [05-builder-clean.md](05-builder-clean.md)：说明 Builder 清理命令的使用方式、清理范围和安全边界。
- [05-builder-plan.md](05-builder-plan.md)：记录 Builder 的构建、资产收集和打包设计及专题实施细节。
- [07-wpfdemo-implementation.md](07-wpfdemo-implementation.md)：记录 WpfDemo 消费仓库 WPF 的实现结构、MSBuild 数据流和扩展约束。
- [08-builder-pr-relay-design.md](08-builder-pr-relay-design.md)：设计 Builder 从 GitHub PR 链接搬运提交、本地验证后创建目标 PR，以及 Actions 构建产物回写机制。
- [PresentationBuildTasks-bootstrap.md](PresentationBuildTasks-bootstrap.md)：说明 `PresentationBuildTasks` 的任务程序集选择、按需构建和锁定输出处理机制。
- [strong-name-signing.md](strong-name-signing.md)：说明 WPF 强名称密钥来源、与原始仓库一致的身份映射及修改约束。
- [cycle-breaker.md](cycle-breaker.md)：记录循环依赖证据、cycle-breaker 的职责、保留条件和退出条件。
- [backlog.md](backlog.md)：记录不打断正式阶段顺序的已观察问题；进入正式计划的事项以 `01-phase-plan.md` 为准。

### 历史归档

- [archive/README.md](archive/README.md)：历史材料索引，只用于追溯设计演变，不作为当前状态或执行顺序依据。

三份核心文档分别负责当前事实、后续计划和接手操作；活跃专题只维护各自机制、证据与约束，不重复仓库整体状态；历史归档不参与当前状态判定。

## 阅读顺序

1. 阅读 `Docs/README.md`，确认文档职责和安全约束。
2. 阅读 [00-overview.md](00-overview.md)，取得当前工作区事实。
3. 阅读 [01-phase-plan.md](01-phase-plan.md)，按优先级选择后续阶段。
4. 执行前阅读 [02-next-session-handoff.md](02-next-session-handoff.md)，完成起手检查并从首个阻塞继续。
5. 核心三份文档阅读完成后，根据任务主题选择对应的活跃专题；专题中的状态描述若与 `00-overview.md` 冲突，以 `00-overview.md` 为准。
6. 只有在追溯设计演变、旧决策来源或历史问题线索时，才查阅 [archive/README.md](archive/README.md) 及其归档材料。

## 事实维护规则

- 只记录已经验证的事实；没有直接证据的内容明确标为“待确认”或“待 Visual Studio 验证”。
- 新证据推翻旧结论时直接改写旧结论，不并列保留互相矛盾的状态。
- `00-overview.md` 负责当前状态；`01-phase-plan.md` 不保存完成历史；`02-next-session-handoff.md` 不写会话流水。
- 项目计数必须注明统计口径；解决方案声明、磁盘项目和 IDE 加载状态必须分开表述。
- 构建成功只覆盖实际执行过的配置、平台和入口；增量构建、独立项目构建、还原成功均不能外推为完整解决方案构建成功。
- 避免依赖对话轮次的措辞、个人机器绝对路径和未经验证的完成宣称。
- Markdown 链接使用相对路径。历史材料完成分类后迁入 `archive/`，并同步修正索引。

## 无人值守原则

- 不等待用户拆分任务；完成安全检查后，按 [01-phase-plan.md](01-phase-plan.md) 的优先级连续推进。
- 局部验证通过后继续处理同一主线的下一个真实阻塞，不以状态复述代替实施。
- 遇到可诊断的构建、加载或环境问题时先收集首个真实错误并尝试解决；只有满足交接文档中的停止条件才中止。
- 实际状态发生变化后更新 `00-overview.md`；只有阶段顺序或接手流程发生变化时，才分别更新 `01-phase-plan.md` 或 `02-next-session-handoff.md`，避免复制易变化的状态。

## 安全约束

- 开始迁移前必须确认 `origin/` 非空；若为空，立即停止迁移。
- `origin/src` 被 `origin/.gitignore` 排除，不受外层 Git 状态保护。迁移源文件时优先使用可核对的脚本复制，并在复制前后检查源目录。
- 修改前检查 Git 变更并保护已有工作，不覆盖来源不明的修改。
- 禁止执行 `git clean -xdf`；清理必须限定到已确认可再生成的输出目录。
- 不通过移除项目制造构建成功。真实实现项目应在依赖闭合后纳入根 `slnx`，无法纳入时记录明确原因。
- 涉及 native/WPF 主链时优先使用 `msbuild`。出现基础类型或同名程序集冲突时，先检查仓库实现与 SDK inbox 引用是否同时进入编译图。
