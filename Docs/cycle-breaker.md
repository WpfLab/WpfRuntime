# cycle-breaker 评估记录

## 背景

当前仓库处于 WPF 工程迁移阶段，目标是优先完成原始结构的重建，并保证解决方案可构建。在这一阶段，`cycle-breaker` 相关项目是否保留，需要基于现有项目引用关系进行评估。

## 结论

在当前迁移阶段，`cycle-breaker` 应继续保留。

原因如下：

1. 现有工程中已明确存在多个 `cycle-breaker` 项目，说明这不是偶发性的项目配置问题，而是原始工程结构的一部分。
2. 当前依赖关系中不仅存在实现层循环，还存在 API 层循环，无法仅依靠调整构建顺序解决。
3. 若在迁移阶段强行移除 `cycle-breaker`，通常需要进行跨程序集的架构重构，风险高，且会削弱与原始 WPF 工程结构的对照性。

## 已识别的 cycle-breaker 项目

当前项目文件中已出现以下 `cycle-breaker` 项目：

- `PresentationUI-PresentationFramework-impl-cycle.csproj`
- `PresentationFramework-ReachFramework-impl-cycle.csproj`
- `PresentationFramework-System.Printing-api-cycle.csproj`
- `ReachFramework-System.Printing-api-cycle.csproj`

从命名可以看出，这些项目分别用于打断实现层或 API 层的循环依赖，不应视为临时性产物。

## 当前可确认的循环关系

### 1. PresentationFramework ↔ PresentationUI

证据：

- `PresentationUI.csproj` 引用 `PresentationFramework.csproj`
- `PresentationFramework.csproj` 引用 `PresentationUI-PresentationFramework-impl-cycle.csproj`

判断：

- 该循环属于实现层循环
- 对应的打断方式为 `impl-cycle`

### 2. PresentationFramework ↔ ReachFramework

证据：

- `PresentationFramework.csproj` 引用 `ReachFramework.csproj`
- `ReachFramework.csproj` 引用 `PresentationFramework-ReachFramework-impl-cycle.csproj`
- `PresentationFramework-ref.csproj` 引用 `ReachFramework-ref.csproj`
- `ReachFramework-ref.csproj` 引用 `PresentationFramework-ReachFramework-impl-cycle.csproj`

判断：

- 该循环是 `PresentationFramework` 与 `ReachFramework` 之间的双向依赖
- 问题同时影响实现层与 ref 层
- 对应的打断方式为 `impl-cycle`

### 3. PresentationFramework ↔ System.Printing

证据：

- `PresentationFramework.csproj` 引用 `System.Printing-ref.csproj`
- `System.Printing-ref.csproj` 反向引用 `PresentationFramework-System.Printing-api-cycle.csproj`

判断：

- 该循环属于 API 层循环
- 问题位于公开契约层，而不仅是实现代码层
- 对应的打断方式为 `api-cycle`

### 4. ReachFramework ↔ System.Printing

证据：

- `ReachFramework.csproj` 引用 `System.Printing-ref.csproj`
- `ReachFramework-ref.csproj` 引用 `System.Printing-ref.csproj`
- `System.Printing-ref.csproj` 反向引用 `ReachFramework-System.Printing-api-cycle.csproj`

判断：

- 该循环同样属于 API 层循环
- 对应的打断方式为 `api-cycle`

## 对迁移工作的影响

当前阶段的优先目标是“忠实重建 + 可构建”。若立即移除 `cycle-breaker`，通常需要进行以下类型的调整：

- 迁移类型到新的程序集
- 拆分公共 API 到新的基础程序集
- 修改公开依赖边界
- 调整资源、标记编译或打印相关装配边界

以上调整均属于架构层重构，不属于当前迁移阶段的最小必要修改，且会显著增加迁移风险。

## 当前建议

现阶段建议按以下顺序推进：

1. 保留现有 `cycle-breaker` 设计
2. 补齐缺失的 `cycle-breaker` 项目
3. 先打通迁移链路并保证仓库可稳定构建
4. 在构建稳定后，再单独评估是否进行“去 cycle-breaker 化”重构

## 何时可以评估移除 cycle-breaker

仅在满足以下条件时，才建议考虑移除 `cycle-breaker`：

- 已确认其仅为历史遗留，而非当前公开 API 的必要结构
- 可以将共享契约下沉到更底层的公共程序集
- 不会破坏与原始 WPF 工程结构的对照关系
- 已具备完整的构建验证与回归验证能力

在不满足上述条件之前，迁移阶段不应主动删除 `cycle-breaker`。

## 后续工作

下一步可继续整理以下内容：

- 四组循环依赖的明确项目依赖图
- 各 `cycle-breaker` 项目的目标位置与目录规划
- 缺失 `cycle-breaker` 项目的补齐清单
