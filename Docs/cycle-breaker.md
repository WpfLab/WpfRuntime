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

当前仓库磁盘上已确认存在以下 `cycle-breaker` 项目：

- `PresentationFramework-PresentationUI-api-cycle.csproj`
- `PresentationFramework-ReachFramework-impl-cycle.csproj`
- `PresentationFramework-System.Printing-api-cycle.csproj`
- `PresentationFramework-System.Printing-impl-cycle.csproj`
- `PresentationUI-PresentationFramework-impl-cycle.csproj`
- `ReachFramework-PresentationFramework-api-cycle.csproj`
- `ReachFramework-System.Printing-api-cycle.csproj`
- `System.Printing-PresentationFramework-api-cycle.csproj`

从命名可以看出，这些项目分别用于打断实现层或 API 层的循环依赖，不应视为临时性产物。

## 当前可确认的循环关系

### 1. PresentationFramework ↔ PresentationUI

证据：

- `PresentationUI.csproj` 引用 `PresentationFramework.csproj`
- `PresentationFramework.csproj` 引用 `PresentationUI-PresentationFramework-impl-cycle.csproj`
- 当前仓库还存在 `PresentationFramework-PresentationUI-api-cycle.csproj`

判断：

- 该循环属于实现层循环
- 同时可以看到 API 层桥接项目也已存在
- 不应假设只需保留单一桥接项目即可

### 2. PresentationFramework ↔ ReachFramework

证据：

- `PresentationFramework.csproj` 引用 `ReachFramework.csproj`
- `ReachFramework.csproj` 引用 `PresentationFramework-ReachFramework-impl-cycle.csproj`
- `PresentationFramework-ref.csproj` 引用 `ReachFramework-ref.csproj`
- `ReachFramework-ref.csproj` 引用 `PresentationFramework-ReachFramework-impl-cycle.csproj`
- 当前仓库还存在 `ReachFramework-PresentationFramework-api-cycle.csproj`

判断：

- 该循环是 `PresentationFramework` 与 `ReachFramework` 之间的双向依赖
- 问题同时影响实现层、ref 层以及 API 桥接层

### 3. PresentationFramework ↔ System.Printing

证据：

- `PresentationFramework.csproj` 引用 `System.Printing-ref.csproj`
- `System.Printing-ref.csproj` 反向引用 `PresentationFramework-System.Printing-api-cycle.csproj`
- 当前仓库还存在 `PresentationFramework-System.Printing-impl-cycle.csproj`
- 当前仓库还存在 `System.Printing-PresentationFramework-api-cycle.csproj`

判断：

- 该循环属于 API 层循环
- 当前至少同时存在 API 层与实现层桥接项目，后续排查时不能只检查单个方向

### 4. ReachFramework ↔ System.Printing

证据：

- `ReachFramework.csproj` 引用 `System.Printing-ref.csproj`
- `ReachFramework-ref.csproj` 引用 `System.Printing-ref.csproj`
- `System.Printing-ref.csproj` 反向引用 `ReachFramework-System.Printing-api-cycle.csproj`

判断：

- 该循环同样属于 API 层循环
- 对应的打断方式为 `api-cycle`

## 对迁移工作的影响

当前阶段的优先目标是"忠实重建 + 可构建"。若立即移除 `cycle-breaker`，通常需要进行以下类型的调整：

- 迁移类型到新的程序集
- 拆分公共 API 到新的基础程序集
- 修改公开依赖边界
- 调整资源、标记编译或打印相关装配边界

以上调整均属于架构层重构，不属于当前迁移阶段的最小必要修改，且会显著增加迁移风险。

## 当前建议

现阶段建议按以下顺序推进：

1. 保留现有 `cycle-breaker` 设计
2. 补齐缺失的 `cycle-breaker` 项目（当前 8 个桥接项目已全部纳入 `Microsoft.Dotnet.Wpf.sln`，命令行 `msbuild -restore` 已可稳定通过）
3. 在处理 `PresentationFramework` / `ReachFramework` / `System.Printing` / `PresentationUI` 构建错误时，优先检查桥接项目是否已纳管、是否已被正确引用、是否缺少最小占位类型
4. 先打通迁移链路并保证仓库可稳定构建
5. 在构建稳定后，再单独评估是否进行"去 cycle-breaker 化"重构

## 与 origin 的偏移说明

当前 cycle-breaker 项目的组织方式已经偏离 origin：

- origin 的 cycle-breaker 位于 `src/Microsoft.DotNet.Wpf/cycle-breakers/`，但当前仓库将其提升到根目录 `cycle-breakers/`。
- 当前 `PresentationFramework-System.Printing-api-cycle`、`ReachFramework-System.Printing-api-cycle` 等桥接项目中包含了一批 origin 不存在的 bridge 文件（如 `SerializationManagers.cs`、`XpsDocument.cs`、`IXpsOMPackageWriter.cs`、`PrintTicketManager.cs`），这些是当前迁移阶段为绕过 C++/CLI 和打印链编译阻塞而引入的最小占位。
- 这些 bridge 文件本身属于迁移妥协代码的一部分，应在后续清理阶段逐项评估是否可以回归到 origin 的模块边界。

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
- 各 `cycle-breaker` 项目在解决方案中的纳管状态（已全部纳管）
- 各 `cycle-breaker` 项目是否已产出目标程序集
- `PresentationFramework` / `ReachFramework` / `System.Printing` 当前仍缺少哪些桥接类型或生成步骤