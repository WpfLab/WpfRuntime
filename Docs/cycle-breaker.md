# cycle-breaker 专题审计

## 职责与结论边界

根 [`Microsoft.Dotnet.Wpf.slnx`](../Microsoft.Dotnet.Wpf.slnx) 当前纳入 8 个 `cycle-breaker` 项目。这些项目是当前重组树为打断构建依赖循环而维护的桥接，不是长期替代真实实现的产品边界。

该文档只记录当前项目引用关系、保留条件和退出条件。完整仓库状态以 [00-overview.md](00-overview.md) 为准，实施顺序以 [01-phase-plan.md](01-phase-plan.md) 为准。

“直接消费者”在这里特指项目文件中的直接 `ProjectReference`。该口径不能覆盖生成任务、反射、硬编码产物路径或其他间接消费，因此“未找到直接消费者”不等同于“可以立即删除”。

## 根 `slnx` 中的 8 个项目

针对项目文件的定向引用搜索显示：7 个项目存在直接消费者；`PresentationFramework-System.Printing-impl-cycle` 当前未找到直接消费者，状态为待确认。

| 项目 | 直接消费者 / 状态 | 保留条件 |
|---|---|---|
| `PresentationFramework-PresentationUI-api-cycle.csproj` | `PresentationUI-PresentationFramework-impl-cycle.csproj`；有直接消费者 | 在 `PresentationUI` 实现桥仍需要最小 `PresentationFramework` API、且真实项目引用会形成循环时保留 |
| `PresentationFramework-ReachFramework-impl-cycle.csproj` | `ReachFramework.csproj`、`ReachFramework-ref.csproj`；有直接消费者 | 在 `ReachFramework` 实现层或 ref 层仍不能直接使用闭合后的 `PresentationFramework` 边界时保留 |
| `PresentationFramework-System.Printing-api-cycle.csproj` | `ReachFramework-System.Printing-api-cycle.csproj`、`System.Printing-ref.csproj`、`System.Printing.vcxproj`、`ReachFramework.csproj`、`ReachFramework-ref.csproj`；有直接消费者 | 在打印相关消费者仍需要最小 `PresentationFramework` API，且真实 `System.Printing` / `PresentationFramework` 引用边界尚未闭合时保留 |
| `PresentationFramework-System.Printing-impl-cycle.csproj` | 定向搜索未找到直接消费者；**待确认** | 先排查生成期、间接引用和硬编码输出路径；只有确认均无消费且替代拓扑明确后才可删除，不因静态搜索为空直接删除 |
| `PresentationUI-PresentationFramework-impl-cycle.csproj` | `PresentationFramework.csproj`；有直接消费者 | 在 `PresentationFramework` 仍需通过桥接消费 `PresentationUI` 实现面、直接项目引用会形成循环时保留 |
| `ReachFramework-PresentationFramework-api-cycle.csproj` | `PresentationFramework-ReachFramework-impl-cycle.csproj`、`PresentationFramework-System.Printing-api-cycle.csproj`、`PresentationFramework-System.Printing-impl-cycle.csproj`；有直接消费者 | 在这些桥接项目仍需要最小 `ReachFramework` / 打印契约时保留；应随其消费者收敛而缩小或退出 |
| `ReachFramework-System.Printing-api-cycle.csproj` | `System.Printing-ref.csproj`、`System.Printing.vcxproj`；有直接消费者 | 在 `System.Printing` 实现与 ref 尚不能使用真实、类型身份一致的 `ReachFramework` 契约时保留 |
| `System.Printing-PresentationFramework-api-cycle.csproj` | `PresentationFramework-System.Printing-api-cycle.csproj`、`PresentationFramework-System.Printing-impl-cycle.csproj`；有直接消费者 | 在 `PresentationFramework` 的打印桥仍需要最小 `System.Printing` API，且真实实现尚未接管时保留 |

上述“有直接消费者”只说明当前引用边存在，不证明项目内容已经最小化，也不证明所有桥接都需要永久保留。

## 与 origin 的表述边界

- 不以“origin 不含 cycle-breaker 项目”作为笼统前提，也不从当前目录位置或项目名称反推 origin 的项目组织。
- 这里只能确认列出的 8 个项目由当前重组树维护，并被根 `slnx` 纳管。
- 对桥接项目中的具体源文件，必须逐文件取得路径、历史或内容对比证据后才能判断来源关系；该文档不对 `XpsDocument` 等文件作来源缺失判断。
- origin 结构差异的统计方法和来源保护边界见 [03-origin-diff-audit.md](03-origin-diff-audit.md)。

## `System.Printing` 真实实现缺口

- `src/Microsoft.DotNet.Wpf/src/System.Printing/System.Printing.vcxproj` 当前尚未纳入根 `slnx`，真实实现仍未接管根构建图中的打印边界。
- 打印相关 cycle-breaker 只用于暂时闭合编译依赖和最小契约，不能作为 `System.Printing` 真实实现的长期替代。
- 收敛打印桥接前，应先按 [01-phase-plan.md 的阶段 4](01-phase-plan.md#阶段-4构建并纳管-systemprinting-实现) 构建并纳管真实实现，再逐个迁移消费者，检查同名程序集和类型身份，最后评估删除桥接项目。
- 不应通过持续扩充 stub API 来模拟完整 `System.Printing`；新增桥接成员必须有当前消费者证据和明确退出条件。

## 移除或收敛条件

每个 cycle-breaker 只能在以下条件均满足后评估移除：

1. 已核对直接 `ProjectReference`、生成期依赖、间接消费和硬编码产物路径。
2. 真实实现项目或稳定的 ref/API 边界已经提供所需契约，消费者能够迁移且不会重新形成项目循环。
3. 已检查同名程序集、重复类型和运行时绑定风险，不以扩大 bridge 内容掩盖类型身份问题。
4. 每次只移除一个引用边或一个项目，并同步更新根 `slnx`；相关独立项目和根入口均需重新验证。
5. 删除后仍能说明公开 API、资源、标记编译和打印链分别由哪个真实项目提供。

`PresentationFramework-System.Printing-impl-cycle` 应优先完成消费审计，但在上述证据齐全前保持“待确认”，不直接删除。

## 低优先级项目元数据维护项

当前发现两个 `PackageId` 与项目文件名不一致：

| 项目 | 当前 `PackageId` | 不一致点 |
|---|---|---|
| `PresentationFramework-System.Printing-impl-cycle.csproj` | `PresentationFramework-ReachFramework` | 与项目所表达的 `System.Printing` 边界不一致 |
| `ReachFramework-System.Printing-api-cycle.csproj` | `ReachFramework-SystemPrinting-api-cycle` | 与项目文件名中的 `System.Printing` 拼写不一致 |

这些是低优先级维护项。`PackageId` 还参与 `TargetOutputRelPath`，修改前应先核对产物路径和消费者；该专题只记录问题，不修改项目文件。
