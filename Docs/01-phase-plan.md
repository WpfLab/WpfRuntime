# 分阶段计划

## 总体策略

当前工作应按“先恢复现有解决方案基线，再补齐解决方案纳管，再继续打通 `PresentationFramework` 主链，最后补缺失模块”的顺序推进。

原因如下：

1. 磁盘上已经存在的项目数量明显多于解决方案已纳入项目数量。
2. 最新构建验证显示当前解决方案本身仍有失败点。
3. 如果现有基线都不稳定，继续扩大纳管范围只会放大排障成本。

## 当前建议执行顺序

1. 先恢复当前解决方案构建基线：
   - `UIAutomationClient`
   - `UIAutomationClientSideProviders`
   - `PresentationCore`
   - `System.Xaml`
   - `WindowsBase`
2. 再收敛“磁盘已存在但尚未纳入解决方案”的项目：
   - `WindowsFormsIntegration`
   - `PresentationFramework`
   - `PresentationUI`
   - `ReachFramework`
   - `System.Printing`
   - `System.Windows.Controls.Ribbon`
   - 主题项目：已纳入解决方案。
3. 再继续打通 `PresentationFramework` 主依赖链：
   - `ReachFramework-ref`
   - `System.Printing-ref`
   - `cycle-breakers`
   - `AvTrace` 代码生成链
4. 最后再迁入缺失顶层模块：
   - `PenImc`
   - `System.Windows.Presentation`
   - `WpfGfx`

---

## 阶段 0：基线与清单重校验

### 目标

确认当前文档、当前磁盘状态和当前解决方案状态一致，避免后续工作建立在过期记录上。

### 已完成

- 已重新核对 `Docs/README.md`、`00-overview.md`、`01-phase-plan.md`、`02-next-session-handoff.md`。
- 已重新核对 `Microsoft.Dotnet.Wpf.sln` 当前实际纳入项目。
- 已重新盘点 `src/Microsoft.DotNet.Wpf/src/` 顶层目录。
- 已重新盘点 `cycle-breakers/` 当前存在的桥接项目。

### 完成标准

- 文档中的“已纳入解决方案项目”“磁盘已有项目”“缺失顶层模块”三份清单相互一致。
- 后续构建记录以最新验证结果为准，不再保留互相矛盾的历史描述。

---

## 阶段 1：恢复当前解决方案构建基线

### 目标

先让 `Microsoft.Dotnet.Wpf.sln` 的现有纳管项目重新回到可诊断、可复现的状态。

### 当前状态

- `Microsoft.Dotnet.Wpf.sln` 已恢复可构建。
- 验证命令：`msbuild C:\lindexi\Code\God\WpfReorganize\Microsoft.Dotnet.Wpf.sln -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal`
- `UIAutomationClient` 可独立构建，`UIAutomationClientSideProviders` 下游缺失参考程序集的问题没有复现。
- `ReachFramework`、`PresentationFramework`、`PresentationUI`、`PresentationFramework.Classic` 与 `System.Windows.Controls.Ribbon` 纳入解决方案后，解决方案完整重建仍可通过。
- 当前解决方案级剩余警告为 `DirectWriteForwarder.vcxproj` 的 `/Zc:forScope-` 已否决警告。

### 任务

1. 保持现有解决方案构建基线可复现。
2. 若再次出现 `UIAutomationClientSideProviders` 下游 `CS0006`，先独立构建 `UIAutomationClient`，再检查解决方案增量状态和构建顺序。
3. 不要在基线未验证时继续扩大解决方案纳管范围。

### 完成标准

- `Microsoft.Dotnet.Wpf.sln` 恢复可构建。
- 当前已纳入项目可通过同一条 `msbuild` 命令复现。

### 风险

- 当前解决方案基线恢复后，继续纳管 `ReachFramework`、`PresentationFramework` 等项目会暴露新的依赖问题。
- `ref` 项目与实现项目的输出链可能不只受项目引用影响，还受自定义 targets 影响。

---

## 阶段 2：解决方案纳管与项目清单收敛

### 目标

让解决方案入口逐步跟上磁盘现状，避免“目录里已经有项目，但解决方案看不到”的长期分裂状态。

### 任务

1. 记录当前所有关键项目的状态：
   - 已在解决方案中
   - 已在磁盘中但未纳入解决方案
   - 目录尚未迁入
2. 优先纳管与当前主链直接相关的现存项目：
   - `ReachFramework`：已纳入解决方案。
   - `PresentationFramework`：已纳入解决方案。
   - `PresentationUI`：已纳入解决方案。
   - `System.Windows.Controls.Ribbon`：已纳入解决方案。
   - `PresentationFramework.Classic`：已纳入解决方案。\r\n   - `PresentationFramework.Aero` / `Aero2` / `AeroLite` / `Fluent` / `Luna` / `Royale`：已纳入解决方案。
   - `WindowsFormsIntegration`：仍需解决完整 `PresentationFramework` API 引用和 `IKeyboardInputSink` 签名问题。
   - `System.Printing`：C++/CLI 实现项目仍需解决类型重定义和 `System.IO.Packaging` 引用问题。
3. 对暂不纳入解决方案的项目，明确写出原因，不要只写“待处理”。

### 完成标准

- 有一份清晰的项目纳管清单。
- 能说明每个未纳入项目的阻塞原因。
- 解决方案中的项目清单与文档保持同步。

### 风险

- 直接把过多项目一次性纳入解决方案会引入大量新失败点。
- 某些项目虽然已经在磁盘中存在，但其真实依赖链仍未闭合。

---

## 阶段 3：`PresentationFramework` 主链打通

### 目标

围绕 `PresentationFramework` 建立后续迁移主线，为 `WindowsFormsIntegration`、主题项目和更上层模块打基础。

### 优先顺序

1. `ReachFramework-ref`
2. `System.Printing-ref`
3. `PresentationFramework`
4. `PresentationUI`
5. `WindowsFormsIntegration`
6. 主题项目

### 任务

1. 重新验证 `ReachFramework-ref` 与 `System.Printing-ref` 的最新构建结果。
2. 对照 `cycle-breakers/` 当前桥接项目，确认缺口是：
   - 缺类型
   - 缺引用
   - 缺项目纳管
   - 缺生成步骤
3. 继续确认 `AvTrace` 代码生成目标是否已完整接入，而不是仅仅跳过导入失败。
4. 在 `PresentationFramework` 到达稳定阻塞点后，再判断 `WindowsFormsIntegration` 是否具备重新纳管条件。

### 当前已验证状态

- `ReachFramework-ref` 与 `System.Printing-ref` 可独立构建，但仍有 cycle-breaker 相关的同名类型警告。
- `ReachFramework-ref` 对 `System.Windows.Xps.XpsDocumentWriter`、`System.Windows.Documents.Serialization.ISerializerFactory` 等 API 的解析已由 `PresentationFramework-System.Printing-api-cycle` 补齐。
- `ReachFramework` 实现项目已可独立构建。当前采用动态调用边界绕开 `XpsSerializerWriter` 与 `XpsDocument` 调用 `XpsDocumentWriter` 时的 `PrintTicket` / `XpsDocument` 类型身份不一致。
- `PresentationFramework` 已抑制实现项目中的 `WPF0001`，并已可独立构建。当前采用动态调用边界绕开打印相关 `XpsDocumentWriter`、`SerializerWriter`、`ISerializerFactory` 与 `PresentationUI` 中 `FindToolBar` 的迁移边界阻塞。
- `PresentationUI` 已可独立构建。当前通过 `System.Printing-ref` 绕过 `System.Printing` C++/CLI 实现项目，并显式引用完整 `PresentationFramework` 输出，避免 `PresentationFramework-System.Printing-api-cycle` 同名程序集覆盖完整控件 API。
- `PresentationUI` 当前仍有 XAML partial 占位成员，说明 `InternalMarkupCompilation` 生成链路还未完全恢复，后续需要用真实标记编译产物替代占位。
- `PresentationFramework.Classic`、`PresentationFramework.Aero`、`PresentationFramework.Aero2`、`PresentationFramework.AeroLite`、`PresentationFramework.Fluent`、`PresentationFramework.Luna`、`PresentationFramework.Royale` 与 `System.Windows.Controls.Ribbon` 已可独立构建并已纳入解决方案。当前通过显式完整 `PresentationFramework` x64 输出补齐主题和 Ribbon 所需控件 API，并避免解决方案内部 AnyCPU 构建落到缺失输出目录。
- `System.Printing-ref` 已移除 `System.Windows.Xps.Packaging.XpsDocument` 占位，避免 `PresentationUI` 同时从 `ReachFramework` 与 `System.Printing` 解析到同名类型。
- `BuildInfo.SystemWindowsControlsRibbon` 当前使用 WCP 公钥，使 `PresentationCore` / `PresentationFramework` 对 Ribbon 的友元访问声明与当前输出程序集强命名一致。
- `System.Printing` C++/CLI 实现项目已越过 MSBuild 配置错误并进入源码编译阶段，当前阻塞为 `SafeMemoryHandle`、`PrintQueue` 等类型重定义和 `System.IO.Packaging` 引用缺失。
- `WindowsFormsIntegration` 已重新验证，当前阻塞为完整 `PresentationFramework` 控件/API 引用缺失和 `IKeyboardInputSink` 接口签名不匹配。最新日志：`artifacts/windowsformsintegration-errors.latest.log`。\r\n- `PresentationBuildTasks` 已重新验证，当前阻塞为项目面向 `net9.0`，但 `global.json` 固定 SDK `8.0.206`，错误为 `NETSDK1045`。最新日志：`artifacts/presentationbuildtasks-errors.latest.log`。\r\n- `Shared/Tracing/mcwpf` 已重新验证，当前阻塞为导入 `C:\tools\Microsoft.DevDiv.Settings.targets` 失败。最新日志：`artifacts/mcwpf-errors.latest.log`。
- 后续仍需继续处理 `ReachFramework` / `System.Printing` / `PresentationFramework` / `PresentationUI` 四方 cycle-breaker 的 API 边界，优先用明确桥接契约替换动态边界。

### 完成标准

- `PresentationFramework` 能进入稳定、可重复的构建诊断状态。
- `ReachFramework-ref` / `System.Printing-ref` / `cycle-breakers` 的阻塞已被分类记录。

### 风险

- `PresentationFramework` 依赖面很广，任何上游变化都会联动多个项目。
- 代码生成、主题资源、桥接项目可能共同决定其最终构建顺序。

---

## 阶段 4：缺失顶层模块补齐

### 目标

在现有主链稳定后，再迁入仓库顶层尚未出现的模块。

### 目标模块

1. `PenImc`
2. `System.Windows.Presentation`
3. `WpfGfx`

### 任务

1. 先补目录与项目文件。
2. 再处理 `ProjectReference`、共享源码路径和 native 依赖。
3. 每迁入一个模块，就同步更新 Docs 文档。

### 完成标准

- 三个缺失顶层模块都已在当前仓库可见。
- 能说明每个模块是否已纳入解决方案、是否可构建、当前首个阻塞点是什么。

---

## 阶段 5：解决方案级构建与文档持续交接

### 目标

让 Visual Studio 构建和命令行 `msbuild` 构建都能围绕统一入口稳定复现，并让后续 AI 不需要再次重建上下文。

### 每次结束前必须更新

- `00-overview.md`：当前状态、当前构建结果、主要缺口。
- `01-phase-plan.md`：优先级、阶段顺序、阻塞变化。
- `02-next-session-handoff.md`：建议起手任务、最新构建入口、必读文件。

### 推荐记录格式

- 最近新增或修改的项目/目录
- 最近验证过的构建入口与命令
- 当前首个真实失败点
- 后续第一步该做什么
- 若中断，后续 AI 先读哪些文件

## 当前执行约束

1. 只要仓库中已经存在 `WindowsBase.csproj`、`PresentationCore.csproj`、`PresentationFramework.csproj` 等同名项目，就必须先排查是否又从 SDK 隐式引用拿到了第二份同名程序集。
2. 禁止再次通过修改 `eng/Versions.props` 中 `AssemblyVersion` 的方式掩盖程序集冲突。
3. 涉及 native/WPF 主链时优先使用 `msbuild`，不要默认使用 `dotnet msbuild`。
4. 不能通过把项目从解决方案中移除来制造“构建通过”；应尽量保留项目并修复构建。


