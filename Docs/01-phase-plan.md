# 分阶段计划

## 总体策略

当前工作应按“先恢复现有解决方案基线，再补齐解决方案纳管，再继续打通 `PresentationFramework` 主链，最后把缺失的 native 依赖改为二进制包接入”的顺序推进。

原因如下：

1. 磁盘上已经存在的项目数量明显多于解决方案已纳入项目数量。
2. 最新构建验证显示当前解决方案本身仍有失败点。
3. 如果现有基线都不稳定，继续扩大纳管范围只会放大排障成本。

## 执行模式

当前计划默认按无人值守模式执行，后续 AI 应持续推进，而不是把任务理解为一次性的文档维护或单点验证。

### 执行要求

1. 每次工作开始后，应先确认：
   - 命令行构建是否通过
   - `Microsoft.Dotnet.Wpf.sln` 中声明的关键项目是否在 IDE 中成功加载
   - 当前最高优先级阻塞是否仍然存在
2. 若某项验证通过，不应立即结束，而应继续处理同一主线上的下一个阻塞点。
3. 若当前选择的子问题无法推进，应立即切换到同阶段内的下一个可落地任务，而不是只更新文档后退出。
4. 若最终只能停在文档更新，必须把“已尝试但未完成的迁移动作”和“下一步首选落点”写入交接文档，避免后续 AI 再次空转。
5. 结束一次工作时，应优先达到“代码/项目迁移有实质推进”而不是“文档已同步”。

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
4. 最后再处理 native 二进制接入与迁移妥协代码清理：
    - `PenImc`：改为通过 NuGet 获取已构建 DLL，不再做源码迁移
    - `WpfGfx`：改为通过 NuGet 获取已构建 DLL，不再做源码迁移
    - 迁移妥协代码：bridge 文件、XAML partial 占位、动态调用边界、显式 HintPath（详见 `Docs/03-origin-diff-audit.md`）

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
- Visual Studio 默认 `Any CPU` 构建已验证可通过：`msbuild C:\lindexi\Code\God\WpfReorganize\Microsoft.Dotnet.Wpf.sln -restore /p:Configuration=Debug /p:Platform="Any CPU" /m:1 /v:minimal /clp:ErrorsOnly`。
- 已将全部 `cycle-breakers/*.csproj` 纳入解决方案，修复了打印桥接输出只在旧 `artifacts` 残留时可用、干净 `msbuild -restore` 会失败的问题。
- `.pl` 文件弹窗已定位为缺少 Perl 时 `Exec` 直接执行 `ThemeGenerator.pl` / `PreprocessXAML.pl` 触发 Windows 文件关联；当前改为先检测 `PerlCommand`，缺少 Perl 时跳过脚本并输出警告。
- `UIAutomationClient` 可独立构建，`UIAutomationClientSideProviders` 下游缺失参考程序集的问题没有复现。
- `ReachFramework`、`PresentationFramework`、`PresentationUI`、`PresentationFramework.Classic` 与 `System.Windows.Controls.Ribbon` 纳入解决方案后，解决方案完整重建仍可通过。
- 当前解决方案级剩余警告为 `DirectWriteForwarder.vcxproj` 的 `/Zc:forScope-` 已否决警告。
- IDE 工作区“生成解决方案”当前仍会在 `PresentationBuildTasks.dll` 复制到 `artifacts\bin\PresentationBuildTasks\Debug\net472\` 时被多进程锁定，这是独立于命令行 `msbuild -restore` 的现存阻塞。

### 任务

1. 保持现有解决方案构建基线可复现。
2. 若再次出现 `UIAutomationClientSideProviders` 下游 `CS0006`，先独立构建 `UIAutomationClient`，再检查解决方案增量状态和构建顺序。
3. 基线验证不能只看命令行 `msbuild`；还要检查 Visual Studio 中是否存在项目加载失败、未加载项目或 `.sln` 与 IDE 已加载项目不一致的情况。
4. 不要在基线未验证时继续扩大解决方案纳管范围。

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
   - 已写入 `.sln` 但在 IDE 中加载失败或未成功加载
2. 优先纳管与当前主链直接相关的现存项目：
   - `System.Windows.Presentation`：已从 `origin` 迁入，并已纳入解决方案；后续需确认其与原始仓库的强签名/友元边界在当前仓库长期保持一致。
   - `ReachFramework`：已纳入解决方案。
   - `PresentationFramework`：已纳入解决方案。
   - `PresentationUI`：已纳入解决方案。
   - `System.Windows.Controls.Ribbon`：已纳入解决方案。
   - `PresentationFramework.Classic`：已纳入解决方案。\r\n   - `PresentationFramework.Aero` / `Aero2` / `AeroLite` / `Fluent` / `Luna` / `Royale`：已纳入解决方案。
   - `WindowsFormsIntegration`：已纳入解决方案，且已重新验证可独立构建；后续需收敛其对完整 `PresentationFramework` 输出的显式 HintPath 依赖。
   - `System.Printing`：C++/CLI 实现项目仍未独立构建；已先收敛掉大面积类型重定义与 `System.IO.Packaging` 缺失，当前转为补齐 ReachFramework / PresentationFramework 打印桥接 API。
   - `cycle-breakers`：已全部纳入解决方案；不再属于“磁盘已存在但未纳管”的清单。
3. 对暂不纳入解决方案的项目，明确写出原因，不要只写“待处理”。
4. 对已经写入 `.sln` 但在 IDE 中加载失败的项目，优先当作真实阻塞处理，不能因为命令行构建暂时通过就搁置。

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
- 打印相关 `cycle-breakers` 已纳入 `Microsoft.Dotnet.Wpf.sln`，命令行 `msbuild -restore` 已不再依赖旧 `artifacts` 产物顺序。
- `ReachFramework-ref` 对 `System.Windows.Xps.XpsDocumentWriter`、`System.Windows.Documents.Serialization.ISerializerFactory` 等 API 的解析已由 `PresentationFramework-System.Printing-api-cycle` 补齐。
- `ReachFramework` 实现项目已可独立构建。当前采用动态调用边界绕开 `XpsSerializerWriter` 与 `XpsDocument` 调用 `XpsDocumentWriter` 时的 `PrintTicket` / `XpsDocument` 类型身份不一致。
- `PresentationFramework` 已抑制实现项目中的 `WPF0001`，并已可独立构建。当前采用动态调用边界绕开打印相关 `XpsDocumentWriter`、`SerializerWriter`、`ISerializerFactory` 与 `PresentationUI` 中 `FindToolBar` 的迁移边界阻塞。
- `PresentationUI` 已可独立构建。当前通过 `System.Printing-ref` 绕过 `System.Printing` C++/CLI 实现项目，并显式引用完整 `PresentationFramework` 输出，避免 `PresentationFramework-System.Printing-api-cycle` 同名程序集覆盖完整控件 API。
- `PresentationUI` 当前仍有 XAML partial 占位成员，说明 `InternalMarkupCompilation` 生成链路还未完全恢复，后续需要用真实标记编译产物替代占位。
- `PresentationFramework.Classic`、`PresentationFramework.Aero`、`PresentationFramework.Aero2`、`PresentationFramework.AeroLite`、`PresentationFramework.Fluent`、`PresentationFramework.Luna`、`PresentationFramework.Royale` 与 `System.Windows.Controls.Ribbon` 已可独立构建并已纳入解决方案。当前通过显式完整 `PresentationFramework` x64 输出补齐主题和 Ribbon 所需控件 API，并避免解决方案内部 AnyCPU 构建落到缺失输出目录。
- `System.Printing-ref` 已移除 `System.Windows.Xps.Packaging.XpsDocument` 占位，避免 `PresentationUI` 同时从 `ReachFramework` 与 `System.Printing` 解析到同名类型。
- `BuildInfo.SystemWindowsControlsRibbon` 当前使用 WCP 公钥，使 `PresentationCore` / `PresentationFramework` 对 Ribbon 的友元访问声明与当前输出程序集强命名一致。
- `WindowsFormsIntegration` 已加入解决方案,随解决方案入口构建通过,且已重新验证可独立构建。
- `PresentationBuildTasks` 已完成 SDK 目标框架调整 (从 `net9.0` 改为 `net8.0`),并已加入解决方案。
- `mcwpf` 已完成现代化改造 (从旧非 SDK 风格改为 SDK 风格),并已加入解决方案。
- `PresentationBuildTasks` 与 `mcwpf` 已重新验证可独立构建，不再是当前阻塞点。
- `ReachFramework-System.Printing-api-cycle` 已继续补齐 `PackagingProgressEventArgs.Action` / `NumberCompleted`、`PrintingCanceledException`、`PrintJobException`、`System.Printing.Interop` 占位命名空间、`PrintTicket.SaveTo/Clone`、`XpsDocument` 最小构造器与序列化管理器成员、`IXpsFixedDocumentSequenceReader` / `IXpsFixedDocumentReader` / `IXpsFixedPageReader` 最小读取属性、`IXpsOMPackageWriter.Close`、`IPrintDocumentPackageTarget.Cancel`、`PrintDocumentPackageStatusProvider.JobIdAcquiredEvent` / `JobId`；bridge 项目已重新验证可独立构建。
- `System.Windows.Presentation` 已从 `origin` 迁入到 `src/Microsoft.DotNet.Wpf/src/System.Windows.Presentation/`，并已纳入 `Microsoft.Dotnet.Wpf.sln`。为匹配当前仓库实际强签名输出，`BuildInfo.SystemWindowsPresentation` 已调整为 WCP 公钥；项目独立构建与解决方案入口构建已重新验证通过。
- 在迁入 `System.Windows.Presentation` 过程中，`ReachFramework-ref` 暴露出 `System.Windows.Xps.XpsDocumentWriter` 解析仍依赖项目引用顺序的问题。当前 `ReachFramework-ref.csproj` 已显式绑定 `System.Printing-ref` 的 ref 输出，避免后续增量构建或解决方案拓扑变化再次触发 `CS0234`。
- `System.Printing` 当前已越过前一组打印 bridge 与 `XpsDocument` 缺口，新的首个失败面已前移到 `GDIExporter` / ReachFramework 更深层 API：`System.Windows.Xps.Serialization.GeometryHelper.ArcToBezier`、`PrintSystemException`、`Microsoft.Internal.GDIExporter.CNativeMethods.ExtTextOutW`、`Microsoft.Internal.AlphaFlattener.Utility.GetFontUri`。
- 后续仍需继续处理 `ReachFramework` / `System.Printing` / `PresentationFramework` / `PresentationUI` 四方 cycle-breaker 的 API 边界，优先用明确桥接契约替换动态边界，并继续恢复 `PresentationUI` 的真实标记编译生成链路。
- Visual Studio 工作区全量构建当前新增已确认阻塞：`PresentationBuildTasks.dll` 在 `net472` 输出复制阶段被多个 `MSBuild.exe` 进程锁定；该问题尚未收敛，不应再尝试通过全量删除 `artifacts` 的方式规避。

### 完成标准

- `PresentationFramework` 能进入稳定、可重复的构建诊断状态。
- `ReachFramework-ref` / `System.Printing-ref` / `cycle-breakers` 的阻塞已被分类记录。

### 风险

- `PresentationFramework` 依赖面很广，任何上游变化都会联动多个项目。
- 代码生成、主题资源、桥接项目可能共同决定其最终构建顺序。

---

## 阶段 4：迁移妥协代码清理与 native 二进制接入

### 目标

在现有主链稳定后，不再迁入 `PenImc`、`WpfGfx` 这两个高维护成本 native 模块的源码，而是改为通过 NuGet 包获取已构建好的 DLL。同时，把之前为了构建通过而引入的妥协代码（bridge 文件、XAML partial 占位、动态调用边界、显式 HintPath）逐一收敛或替换为更接近 origin 的方案。

### 目标

- 迁移妥协代码清理：
  - `ReachFramework` 内的 bridge 文件：`SafeMemoryHandle.cs`、`PrintQueueBridge.cs`、`DocumentReferenceBridge.cs`
  - `WindowsBase` 内的 `CaseInsensitiveOrdinalStringComparer.cs`
  - `System.Xaml` 内的 `StaticExtensionConverter.cs`
  - `PresentationUI` 的 XAML partial 占位成员：`InstallationError`、`TenFeetInstallationError`、`TenFeetInstallationProgress`、`FindToolBar`
  - `PresentationFramework` 打印链路的动态调用边界
  - 主题项目 / Ribbon / `PresentationUI` / `WindowsFormsIntegration` 对完整 `PresentationFramework` 输出的显式 HintPath
- Native 二进制接入：
  - `PenImc`：改为通过 NuGet 获取已构建 DLL，不再做源码迁移
  - `WpfGfx`：改为通过 NuGet 获取已构建 DLL，不再做源码迁移

### 任务

1. 优先恢复 `PresentationUI` 的真实标记编译生成链路，替换当前 XAML partial 占位。
2. 逐项评估 bridge 文件是否可替换为更接近 origin 的方案或更稳定的项目引用。
3. 收敛 `ReachFramework` / `PresentationFramework` / `PresentationUI` 的动态边界。
4. 为 `PenImc`、`WpfGfx` 盘点现有 NuGet 包来源、目标框架、平台版本和 DLL 清单。
5. 调整托管侧引用与构建逻辑，使其从 NuGet 包解析二进制依赖。

### 完成标准

- 迁移妥协代码清单中的每一项都有明确状态：已清理、已替换、或已确认当前无法清理并记录阻塞原因。
- `PenImc` 与 `WpfGfx` 的依赖已经切换为 NuGet 二进制接入方案。
- 能说明每个模块对应的包来源、平台版本、目标框架，以及当前是否仍有托管侧编译阻塞。

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


