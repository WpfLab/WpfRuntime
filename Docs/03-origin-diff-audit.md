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
4. 针对当前仓库独有补丁文件，抽样检查其是否存在于 `origin`，用于识别"迁移性桥接"与"潜在长期偏离"。

## 当前已验证事实

### `origin` 目录状态正常

- `origin` 目录存在。
- `origin` 顶层当前至少包含：
  - `src`
  - `.gitignore`
- `origin` 没有被清空，可以作为当前迁移对照基线。

### 顶层模块差异

当前仓库仍缺少以下原始顶层模块的源码目录，但它们后续不再作为源码迁移目标：

- `PenImc`：通过 NuGet 二进制 DLL 接入（详见 `Docs/04-NuGet-Binary.md`）
- `WpfGfx`：通过 NuGet 二进制 DLL 接入（详见 `Docs/04-NuGet-Binary.md`）

这两个 native 模块不再纳入源码迁移清单；当前仓库的差异重点已从"缺失模块"转为"迁移妥协代码"。

### 项目文件数量差异

排除 `obj/bin` 后，已验证到：

- `origin` 项目文件数：`103`
- 当前仓库项目文件数：`51`

差量主要集中在 `WpfGfx`、`PenImc`、`redist` 和 `tests`。

### 可比源码文件规模

排除 `obj` / `bin` / `artifacts` / `TestResults` 后：

- `origin` 清洗后文件数：`6119`
- 当前仓库清洗后文件数：`4632`

差量约 `1487` 个文件，主要集中在 `WpfGfx`（约 1381）、`PenImc`（约 52）、`tests`（约 189）。

### `git` 目录级差异量级

对可比的 `src` 子树执行 `git diff --no-index --shortstat` 后：

- `2426 files changed`
- `14729 insertions(+)`
- `532883 deletions(-)`

该差异量级主要反映"尚未迁移"和"重组变更"，不能直接解读为"错误删除"。

## 当前仓库相对 origin 的主要偏离类型

### 1. 结构性缺失（已不再作为源码迁移目标）

- `WpfGfx` 源码未迁入，改为 NuGet 二进制接入
- `PenImc` 源码未迁入，改为 NuGet 二进制接入
- `tests` 目录下的关键单元测试与 DRT 项目未迁入
- `redist` 目录下的几个原始项目未迁入

### 2. 迁移妥协代码（新工作重心）

当前仓库存在一批 `origin` 中不存在的文件，用于暂时绕开引用环、C++/CLI 编译边界或目标框架差异。这些是需要逐项清理的迁移妥协代码：

**Bridge 文件（`origin` 中不存在）：**

- `src\Microsoft.DotNet.Wpf\src\ReachFramework\MS\Internal\Printing\Configuration\SafeMemoryHandle.cs`
- `src\Microsoft.DotNet.Wpf\src\ReachFramework\PrintConfig\PrintQueueBridge.cs`
- `src\Microsoft.DotNet.Wpf\src\ReachFramework\Serialization\manager\DocumentReferenceBridge.cs`
- `src\Microsoft.DotNet.Wpf\src\WindowsBase\MS\Internal\IO\Packaging\CaseInsensitiveOrdinalStringComparer.cs`
- `src\Microsoft.DotNet.Wpf\src\System.Xaml\System\Windows\Markup\StaticExtensionConverter.cs`

**XAML partial 占位（origin 由 `.g.cs` 生成）：**

- `PresentationUI` 中的 `InstallationError`、`TenFeetInstallationError`、`TenFeetInstallationProgress`、`FindToolBar` XAML partial 占位成员

**动态调用边界：**

- `PresentationFramework` 打印链路中 `XpsDocumentWriter`、`SerializerWriter`、`ISerializerFactory` 的动态调用
- `ReachFramework` 中 `XpsSerializerWriter` 调用 `XpsDocumentWriter` 的动态边界（**已验证阻塞**：三个 `PrintTicket` 类型身份不一致，详见下方专项分析）

### 2.1 `XpsSerializerWriter` 动态调用边界专项分析（已验证）

`src\Microsoft.DotNet.Wpf\src\ReachFramework\SerializerFactory\XpsSerializerWriter.cs` 中的 `(dynamic)` 转换已被确认为当前架构下的**必要迁移妥协**。

**对比事实：**

- origin 的 `XpsSerializerWriter.cs` 无任何 `(dynamic)`，所有 `PrintTicket` 方法使用 `override` + 直接传参
- 当前版本移除了 `override`（部分方法）并添加 `(dynamic)` 转换

**根因：**

三个不同的 `PrintTicket` 类型身份在当前架构中共存：
1. `ReachFramework` 自行编译的 `PrtTicket_Public_Simple.cs`（定义 `System.Printing.PrintTicket`）
2. cycle-breaker `ReachFramework-PresentationFramework-api-cycle` 中的 stub `PrintTicket`
3. SDK inbox `System.Printing.dll` 中的 `PrintTicket`（`XpsDocumentWriter` 使用）

**origin 如何避免：**

`System.Printing.vcxproj`（C++/CLI）作为真实 DLL 编译后，`XpsDocumentWriter` 引用 `ReachFramework.dll` 的 `PrintTicket`，所有类型身份统一。

**解除阻塞条件：**

需要 `System.Printing.vcxproj` C++/CLI 项目独立构建成功。此前 `(dynamic)` 无法消除。

**已验证过的修复尝试：**

将 `ReachFramework.csproj` 的 `ProjectReference` 配置完全对齐 origin（移除 `PrivateAssets`、`<Private>false</Private>` 和额外 `PresentationFramework-System.Printing-api-cycle` 引用）→ 仍然失败（12 个 CS0115）。

**显式 HintPath 引用：**

- 主题项目、Ribbon、`PresentationUI`、`WindowsFormsIntegration` 对完整 `PresentationFramework` 输出的显式 HintPath
- `ReachFramework-ref` 对 `System.Printing-ref` 的显式 HintPath

### 3. 解决方案纳管与原始仓库边界不一致

当前仓库已把 `ReachFramework`、`PresentationFramework`、`PresentationUI`、主题项目、`WindowsFormsIntegration`、`cycle-breakers` 等主链项目纳入 `Microsoft.Dotnet.Wpf.sln`，组织方式已不同于 origin 的原始入口结构。这对当前仓库是必要的，但需要持续对照 origin 避免长期分叉。

## 潜在问题清单

### 高优先级

1. 迁移妥协代码持续累积
   - 影响：Bridge 文件、XAML partial 占位、动态调用边界、显式 HintPath 引用等，都是当前"为迁移而补"的代码。
   - 风险：这些妥协代码长期存在会让仓库行为越来越偏离 origin，且新增开发人员难以区分哪些是临时补丁、哪些是真实实现。
   - 建议：逐项评估每个妥协代码是否可替换为更接近 origin 的方案。部分补丁（如 XAML partial 占位）当前仍是构建所必需，应先恢复对应的生成链再移除。

2. `PresentationUI` 的 XAML 标记编译链尚未恢复
   - 影响：`InstallationError`、`TenFeetInstallationError`、`TenFeetInstallationProgress`、`FindToolBar` 仍依赖手写最小 partial 占位成员。
   - 风险：占位长期保留会掩盖真实的标记编译接线缺口。
   - 建议：优先恢复 `PresentationUI` 的 `InternalMarkupCompilation` 生成链；在链路恢复前不要删除这些占位。

3. `System.Printing` / `ReachFramework` / `PresentationFramework` 的动态边界过多
   - 影响：部分打印链路依赖动态边界和 API-cycle bridge。
   - 风险：编译通过不代表行为等价。
   - 建议：处理打印链时应始终与 `origin` 源文件逐段对照。

4. 测试工程未迁入
   - 影响：缺少与 origin 对应的回归验证能力。
   - 建议：至少优先迁入 `System.Xaml.Tests`、`PresentationCore.Tests`。

### 中优先级

5. Bridge 文件可能长期固化
   - 建议：每推进一个主链阻塞点，回看对应 origin 文件判断 bridge 是"最小占位"还是"错误替代"。

6. 显式 HintPath 引用仍未完全消除
   - 建议：已收敛到 `$(WpfNativePlatform)`，后续继续消除对产物路径的依赖。

### 低优先级

7. `WpfGfx` 与 `PenImc` 暂未通过 NuGet 二进制接入
   - 建议：在妥协代码清理到一定程度后按 `Docs/04-NuGet-Binary.md` 接入。

8. 局部文件命名或位置与 origin 不完全一致
   - 建议：收敛 bridge 或恢复真实实现时尽量回归 origin 命名和目录结构。

## 当前建议的修复顺序

1. 清理迁移妥协代码，按优先级逐项处理：
   - 先恢复 `PresentationUI` 的真实标记编译生成链路，替换 XAML partial 占位
   - 再收敛 `ReachFramework` / `PresentationFramework` / `PresentationUI` 的动态边界
   - 逐项评估 bridge 文件是否可替换为更接近 origin 的方案
   - 消除显式 HintPath 引用，改为更稳定的项目引用
2. 并行处理 `System.Printing` C++/CLI 的 bridge 边界问题
3. 补齐高价值测试工程：`System.Xaml.Tests`、`PresentationCore.Tests`
4. 在妥协代码清理到一定程度后，为 `PenImc` 和 `WpfGfx` 接入 NuGet 二进制 DLL

## 当前执行结论

- 当前仓库与 origin 的差异仍然很大，但差异主要由"缺失模块 + 迁移性桥接 + 测试缺席"构成。
- `System.Windows.Presentation` 已完成迁入；`PenImc` 和 `WpfGfx` 不再走源码迁移路线。
- 新的工作重心已从"补齐缺失模块"转为"清理迁移妥协代码"。
- 主题项目和 Ribbon 对完整 `PresentationFramework` 的显式引用路径已从硬编码 `x64` 收敛到 `$(WpfNativePlatform)`。
- `PresentationUI` 的 XAML partial 占位当前仍是构建所必需，应在恢复真实标记编译链后再移除。