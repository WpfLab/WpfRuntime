# 与 origin 的差异审计

## 目标

该文档用于记录当前重组仓库与 `C:\lindexi\Code\God\WpfReorganize\origin\src\Microsoft.DotNet.Wpf` 原始代码之间，已经验证过的结构差异、潜在风险和优先修复方向，避免后续迁移在不知情的情况下继续偏离原始 WPF 仓库。

## 审计范围与方法

### 对比范围

- 原始目录：`C:\lindexi\Code\God\WpfReorganize\origin\src\Microsoft.DotNet.Wpf`
- 当前目录：`C:\lindexi\Code\God\WpfReorganize\src\Microsoft.DotNet.Wpf`
- 解决方案入口：`C:\lindexi\Code\God\WpfReorganize\Microsoft.Dotnet.Wpf.sln`

### 已验证方法

1. 先确认 `origin` 目录存在且非空。
2. 使用 `git diff --no-index` 对 `origin` 与当前仓库做目录级对比。
3. 排除 `obj` / `bin` / `artifacts` / `TestResults` 后，重新统计项目、源码和顶层目录差异。
4. 针对当前仓库独有补丁文件，抽样检查其是否存在于 `origin`，用于识别“迁移性桥接”与“潜在长期偏离”。

## 当前已验证事实

### `origin` 目录状态正常

- `origin` 目录存在。
- `origin` 顶层当前至少包含：
  - `src`
  - `.gitignore`
- `origin` 没有被清空，可以作为当前迁移对照基线。

### 顶层模块差异仍然明显

对比 `origin/src/Microsoft.DotNet.Wpf/src` 与 `src/Microsoft.DotNet.Wpf/src` 后，当前仓库仍缺少以下原始顶层模块：

- `PenImc`
- `System.Windows.Presentation`
- `WpfGfx`

这说明当前仓库虽然已经打通了大部分托管主链，但与原始仓库相比仍不是完整镜像。

### 项目文件数量差异较大

排除 `obj/bin` 后，已验证到：

- `origin` 项目文件数：`103`
- 当前仓库项目文件数：`51`

当前仓库比原始仓库少了约一半项目，差异主要集中在以下区域：

1. `WpfGfx` 原生图形链
2. `PenImc`
3. `System.Windows.Presentation`
4. `redist` 下的 native/redist 项目
5. `tests` 下的单元测试与 DRT 项目

### 可比源码文件规模仍有较大缺口

排除 `obj` / `bin` / `artifacts` / `TestResults` 后，已验证到：

- `origin` 清洗后文件数：`6119`
- 当前仓库清洗后文件数：`4632`

当前仓库仍少约 `1487` 个文件。缺口主要集中在：

- `src\WpfGfx`：约 `1381` 个文件
- `tests\IntegrationTests`：约 `129` 个文件
- `tests\UnitTests`：约 `60` 个文件
- `src\PenImc`：约 `52` 个文件
- `src\System.Windows.Presentation`：约 `7` 个文件
- `cycle-breakers`：若以 `origin/src/Microsoft.DotNet.Wpf` 子树为基线对比，当前仓库根目录下的 `cycle-breakers` 不在同一对比根内，不能据此误判为“当前仓库缺少 cycle-breaker”

### `git` 目录级差异量级很大

对可比的 `src` 子树执行 `git diff --no-index --shortstat` 后，已观察到：

- `2426 files changed`
- `14729 insertions(+)`
- `532883 deletions(-)`

该结果只能说明“当前重组仓库与原始仓库差异量级很大”，不能直接解读为“当前仓库错误删除了 50 多万行代码”。

原因是：

1. 当前仓库本来就尚未迁入 `WpfGfx`、`PenImc`、`System.Windows.Presentation`、测试工程与 redist 项目。
2. 当前仓库引入了重组后的根级 `cycle-breakers`、文档、解决方案整理与局部桥接补丁。
3. 直接看全文 diff 很容易把“尚未迁移”和“迁移错误”混在一起。

因此，后续判断风险时，应优先看“缺了哪些模块、增加了哪些桥接、哪些行为与 origin 已经分叉”，不要只看行数。

## 当前仓库相对 origin 的主要偏离类型

### 1. 结构性缺失

这类问题表示当前仓库尚未把 origin 的模块完整搬入，因此天然存在与原始仓库不一致的行为风险。

当前已确认的重点缺失：

- `WpfGfx` 整体未迁入
- `PenImc` 整体未迁入
- `System.Windows.Presentation` 整体未迁入
- `tests` 目录下的关键单元测试与 DRT 项目未迁入
- `redist` 目录下的几个原始项目未迁入

### 2. 迁移性桥接补丁增多

当前仓库存在一批 `origin` 中不存在的文件，用于暂时绕开引用环、C++/CLI 编译边界或目标框架差异。例如：

- `src\Microsoft.DotNet.Wpf\src\ReachFramework\MS\Internal\Printing\Configuration\SafeMemoryHandle.cs`
- `src\Microsoft.DotNet.Wpf\src\ReachFramework\PrintConfig\PrintQueueBridge.cs`
- `src\Microsoft.DotNet.Wpf\src\ReachFramework\Serialization\manager\DocumentReferenceBridge.cs`
- `src\Microsoft.DotNet.Wpf\src\WindowsBase\MS\Internal\IO\Packaging\CaseInsensitiveOrdinalStringComparer.cs`
- `src\Microsoft.DotNet.Wpf\src\System.Xaml\System\Windows\Markup\StaticExtensionConverter.cs`

这类文件不一定是错误，但它们说明当前仓库已经出现“为迁移而补”的局部实现。若不持续回看 origin，很容易在后续迭代中把这些临时桥接当成长期真相保留下来。

### 3. 解决方案纳管与原始仓库边界不一致

当前仓库已经把 `ReachFramework`、`PresentationFramework`、`PresentationUI`、主题项目、`WindowsFormsIntegration` 等主链项目纳入 `Microsoft.Dotnet.Wpf.sln`，并形成新的重组入口。

这有利于当前仓库构建，但也带来一个风险：

- 当前解决方案组织方式已经不再等同于 origin 的原始项目入口结构。
- 若只围绕当前 `.sln` 继续修补，而不回看 `origin` 缺失模块和原始项目边界，后续会越来越像“能编译的分叉仓库”，而不是“忠实迁移的重组仓库”。

## 潜在问题清单

### 高优先级

1. `System.Windows.Presentation` 缺失
   - 影响：`WindowsBase` 已经保留 `BuildInfo.SystemWindowsPresentation` 友元可见性，但模块本身未迁入，说明当前结构与原始仓库不完整。
   - 风险：后续若有代码假定该程序集存在，可能继续通过桥接或临时补丁绕开，而不是恢复原始模块边界。
   - 建议：优先迁入。该模块文件量小，适合作为差异收敛的第一步。

2. `WpfGfx` 整体缺失
   - 影响：当前仓库的 native 图形链与 origin 差距最大。
   - 风险：后续若继续仅靠托管侧补丁推进，容易让与图形、渲染、native 依赖有关的问题长期处于“未恢复原始结构”的状态。
   - 建议：在托管主链进一步稳定后，尽快按子模块分批迁入。

3. `PenImc` 整体缺失
   - 影响：输入法/文本输入相关原生模块仍未恢复。
   - 风险：可能导致当前仓库对文本输入相关路径的覆盖不完整。
   - 建议：在 `System.Windows.Presentation` 之后优先评估。

4. 测试工程未迁入
   - 影响：当前仓库缺少与 origin 对应的回归验证能力。
   - 风险：迁移过程中即使构建通过，也无法快速判断行为是否仍与原始仓库一致。
   - 建议：至少优先迁入 `System.Xaml.Tests`、`PresentationCore.Tests` 这类体量较小、价值较高的测试项目。

### 中优先级

5. 当前仓库独有 bridge 文件可能长期固化
   - 影响：桥接文件能帮助短期构建，但可能掩盖真实缺失 API 或错误的装配边界。
   - 风险：后续继续在 bridge 上叠 bridge，会让行为越来越偏离 origin。
   - 建议：每推进一个主链阻塞点，都要回看对应 origin 文件，判断当前 bridge 是“最小占位”还是“错误替代”。

6. `System.Printing` / `ReachFramework` / `PresentationFramework` 的动态边界过多
   - 影响：当前主链虽然可构建，但部分打印链路依赖动态边界和 API-cycle bridge。
   - 风险：编译通过不代表行为等价；若与 origin 打印链差距继续扩大，后续收敛成本会更高。
   - 建议：后续处理打印链时，应始终与 `origin` 源文件逐段对照，避免继续发散。

### 低优先级但需要持续观察

7. 局部文件命名或位置与 origin 不完全一致
   - 影响：例如部分补丁文件在当前仓库新增，或与 origin 文件名不完全一致。
   - 风险：长期看会增加后续同步 origin 变更的难度。
   - 建议：后续在收敛 bridge 或恢复真实实现时，尽量回归 origin 命名和目录结构。

## 当前建议的修复顺序

1. 先迁入 `System.Windows.Presentation`
   - 原因：模块小、依赖简单、可直接缩小与 origin 的结构差异。
2. 再评估 `PenImc`
   - 原因：体量远小于 `WpfGfx`，适合作为下一步 native 迁移入口。
3. 再拆分推进 `WpfGfx`
   - 原因：体量最大，应按子目录和项目逐步迁入，不适合一次性整体搬运。
4. 之后补齐高价值测试工程
   - 优先考虑：`System.Xaml.Tests`、`PresentationCore.Tests`

## 当前执行结论

- 当前仓库与 origin 的差异仍然很大，但差异主要由“缺失模块 + 迁移性桥接 + 测试缺席”构成，而不是单一代码错误。
- 当前最适合作为差异收敛第一步的目标是：`System.Windows.Presentation`。
- 在完成该模块迁入之前，不应把当前仓库误判为“已经基本等同于 origin”。
