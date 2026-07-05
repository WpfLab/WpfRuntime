# 下一次 AI 对话交接

## 交接目标

该文档用于让后续 AI 直接接手当前仓库，不需要重复调查仓库结构。

> **⚠️ 首先阅读 `Docs\03-session-exploration-2024-06.md`**，其中记录了最近一次会话的详细探索、已完成的修复、当前阻塞点和推荐的前进策略。

## 当前构建基线

```bash
# 删除 artifacts 后执行（每次验证前必须清理，不要删除 .vs）
Remove-Item -Recurse -Force artifacts
msbuild Microsoft.Dotnet.Wpf.slnx -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal /clp:ErrorsOnly
```

结果：命令行清理后构建曾验证成功；Visual Studio 打开解决方案后构建出现新的 IDE 项目级构建顺序问题，需继续在 Visual Studio 中复验。

Visual Studio 清理后 restore 需要所有被 `ProjectReference` 引用的 `ref/*.csproj` 纳入 `Microsoft.Dotnet.Wpf.slnx`。当前 `WindowsBase-ref`、`PresentationFramework-ref`、`PresentationUI-ref`、`ReachFramework-ref`、`UIAutomationTypes-ref`、`UIAutomationProvider-ref`、`UIAutomationClient-ref`、`UIAutomationClientSideProviders-ref`、各主题 ref 项目等均已纳入解决方案，避免 `NU1105` 和后续 `CS0006`/`MC1000` 级联失败。

Visual Studio 构建日志中的 `PresentationUI` `MC1000` 已确认不是 XAML 内容本身错误，而是上游输出缺失的级联错误：`ReachFramework-ref` 缺少 `project.assets.json`，导致 `ReachFramework` ref 输出缺失、`PresentationFramework.dll` 未生成，最后 `PresentationUI` 标记编译找不到完整 `PresentationFramework.dll`。当前已在以下引用上显式加入 `Targets="Restore;Build"`：`ReachFramework -> ReachFramework-ref`、`PresentationFramework-ref -> ReachFramework-ref`、`PresentationFramework -> ReachFramework`、`PresentationUI -> PresentationFramework`。这些项目文件已通过 XML 静态验证，尚需在 Visual Studio 中验证“生成解决方案”。

> **⚠️ 重要教训**：验证构建是否通过时，**必须每次先删除 artifacts 目录**，然后直接执行 `msbuild` 面向 sln 文件一次性构建。不能通过多次不断尝试构建、依赖前一次构建残留的产物来"凑"出构建成功。这是之前犯过的错误，会导致虚假的"构建通过"假象。

### Visual Studio 构建已知问题

```bash
msbuild Microsoft.Dotnet.Wpf.slnx -restore /p:Configuration=Debug /p:Platform="Any CPU" /m:1 /v:minimal
```

`Any CPU` 命令行构建曾通过 `WpfNativePlatform` 映射到 `x64` 收敛。当前需要优先复验的是 Visual Studio 打开解决方案后的构建顺序：确认 `ReachFramework-ref` 的 restore assets 会在 `ReachFramework` / `PresentationFramework` / `PresentationUI` 之前生成，且 `PresentationUI` 不再因缺少 `PresentationFramework.dll` 报 `MC1000`。

## 下一步工作（已排好优先级，可直接开始）

### 优先级 0：完善 Builder 构建器项目，打通 NuGet 打包

Builder 项目（`eng\Builder\Builder.csproj`）是驱动构建 + NuGet 打包的入口工具。详细计划见 `Docs\05-builder-plan.md`。

**当前进度（已实现，打包已打通）：**

- ✅ `Builder.csproj` 已配置（net8.0、LangVersion 12、独立 OutputPath、无 ImplicitUsings）
- ✅ `Program.cs` 已实现完整的编排逻辑（~440 行）
- ✅ 步骤 1 & 2：探索 `origin\NuGetPackage\` 和 NuGet 缓存 DLL 清单已完成
- ✅ 清理逻辑：逐目录清理 artifacts\bin 和 artifacts\obj，跳过锁定文件
- ✅ 构建驱动：按依赖顺序逐个项目调用 msbuild 构建（不走 sln），共 22 个项目
- ✅ 托管 DLL 收集：从 artifacts\bin 自动收集 20+ 个 WPF 托管 DLL
- ✅ Native DLL 收集：通过 `PackageDownload` + `WritePackagePaths` target 明确拉取运行时包，不再依赖 NuGet 缓存
- ✅ `.nuspec` 生成：动态生成 `DotNetCampus.WpfLib.nuspec`
- ✅ **打包已打通**：`_pack.csproj` 放在 `%TEMP%\WpfBuilderPack\<guid>\` 下，完全隔离于仓库 `Directory.Build.props` 链，`dotnet pack` 成功生成 `.nupkg`

**设计决策：**

- Builder **不构建 sln**，而是按依赖顺序逐项目调用 msbuild 构建具体 csproj，避免 Builder 自锁
- 构建 `/p:Platform=x64`，产物在 `artifacts\bin\<Project>\x64\Debug\net8.0\`
- 包 ID `DotNetCampus.WpfLib`，版本 `1.0.0`，作者 `dotnet campus`，TFM `net8.0`
- Native DLL 来源：通过 `PackageDownload` 从 NuGet 拉取 `microsoft.windowsdesktop.app.runtime.win-x64/win-x86@8.0.6`，路径写入 `PackagePaths.txt` 供 Program.cs 读取

**打包方案：** `_pack.csproj` 放在 `%TEMP%\WpfBuilderPack\<guid>\` 下，该目录在仓库树之外，不会继承 `Directory.Build.props` 中的 Arcade SDK 导入。`dotnet pack` 可直接成功。

**Builder 项目文件：**

| 文件 | 说明 |
|------|------|
| `eng\Builder\Builder.csproj` | 项目文件，net8.0 控制台，含 `PackageDownload` + `WritePackagePaths` target |
| `eng\Builder\Program.cs` | 完整编排逻辑 |
| `eng\Builder\bin\` | Builder 自身输出（不在 artifacts\ 内） |
| `eng\Builder\bin\staging\` | 打包暂存目录（lib/ + runtimes/ + .nuspec） |
| `eng\Builder\bin\nupkg\` | 最终 .nupkg 输出目录 |
| `eng\Builder\bin\PackagePaths.txt` | 由 MSBuild target 生成的运行时包路径 |

### 优先级 1：恢复 `PresentationUI` 的 XAML 标记编译链路

这是当前最高价值的清理项，完成后可以消除本次迁移中的一个显著偏差。

**真实状态（与文档描述有差异，请以此为准）：**

- `InstallationError.xaml.cs`、`TenFeetInstallationError.xaml.cs`、`TenFeetInstallationProgress.xaml.cs`、`FindToolBar.xaml.cs` 这 4 个文件 **都来自 origin**，并不是手写的"XAML partial 占位"。
- 当前与 origin 的差异极小：`InstallationError.xaml.cs` 多了 `: Grid` 基类和多了一行 `using System.Windows.Documents;`；`FindToolBar.xaml.cs` 多了 `: ToolBar` 基类。其余三个文件内容与 origin 几乎一致。
- 之所以加基类，是因为 origin 依靠 `.g.cs`（`InternalMarkupCompilation` 输出）提供基类和 `InitializeComponent()`，当前没有 `.g.cs` 所以手动补了基类。
- 当前 `PresentationUI` 目录下 **没有任何 `.g.cs` 文件**。

**修复方向：**

1. 在 `PresentationUI.csproj` 中启用 `InternalMarkupCompilation` 目标——让 MSBuild 在编译前从 XAML 生成 `.g.cs`。参考 origin 仓库的 `PresentationUI.csproj` 中是怎么配的（主要是 `Page` ItemGroup 和标记编译 task 注入）。
2. 生成 `.g.cs` 后，回退上面 4 个文件中对基类的新增行（去掉 `: Grid`、`: ToolBar`），恢复到与 origin 完全一致。
3. 验证方式：`msbuild ...\PresentationUI\PresentationUI.csproj -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal /clp:ErrorsOnly`

### 优先级 2：消除 `XpsSerializerWriter.cs` 中的动态调用边界

**状态：已验证阻塞。** `(dynamic)` 在当前架构下无法直接消除，只能在 `System.Printing` C++/CLI 构建恢复后处理。

**调查结论（已验证）：**

1. origin 的 `XpsSerializerWriter.cs` **没有任何** `(dynamic)` 转换，所有带 `PrintTicket` 的方法均使用 `override` + 直接传参。
2. 尝试将 `ReachFramework.csproj` 的 `ProjectReference` 配置调整到与 origin 完全一致后，仍然失败（12 个 CS0115 "没有找到适合的方法来重写" 错误全部持续）。
3. 根因：当前架构中 **三个不同的 `PrintTicket` 类型身份共存**：
   - `ReachFramework` 自行编译 `PrtTicket_Public_Simple.cs`（定义 `System.Printing.PrintTicket`）
   - cycle-breaker stub（`ReachFramework-PresentationFramework-api-cycle`）定义自己的 `PrintTicket`，用于 `SerializerWriter` 基类
   - SDK inbox `System.Printing.dll` 定义第三个 `PrintTicket`，用于 `XpsDocumentWriter`
4. origin 能避免此问题的原因：`System.Printing.vcxproj`（C++/CLI）编译为真实 DLL，直接引用 `ReachFramework.dll` 的 `PrintTicket`，让 `XpsDocumentWriter` 与 `ReachFramework` 共享同一个类型身份。
5. **阻塞条件**：需要 `System.Printing.vcxproj` C++/CLI 项目独立构建成功，才能像 origin 一样统一 `PrintTicket` 类型身份。届时 `XpsSerializerWriter.cs` 可直接替换为 origin 版本。

**后续操作建议**：在 `System.Printing` C++/CLI 构建恢复后，执行以下步骤：
1. 用 origin 的 `XpsSerializerWriter.cs` 直接覆盖当前文件
2. 验证 `ReachFramework.csproj` 构建通过

### 优先级 3：逐项评估 bridge 文件

以下文件 **在 origin 中完全不存在**，是真正的迁移妥协代码：

| 文件 | 路径 |
|------|------|
| `SafeMemoryHandle.cs` | `src\Microsoft.DotNet.Wpf\src\ReachFramework\MS\Internal\Printing\Configuration\SafeMemoryHandle.cs` |
| `PrintQueueBridge.cs` | `src\Microsoft.DotNet.Wpf\src\ReachFramework\PrintConfig\PrintQueueBridge.cs` |
| `DocumentReferenceBridge.cs` | `src\Microsoft.DotNet.Wpf\src\ReachFramework\Serialization\manager\DocumentReferenceBridge.cs` |
| `CaseInsensitiveOrdinalStringComparer.cs` | `src\Microsoft.DotNet.Wpf\src\WindowsBase\MS\Internal\IO\Packaging\CaseInsensitiveOrdinalStringComparer.cs` |

`StaticExtensionConverter.cs`（`src\Microsoft.DotNet.Wpf\src\System.Xaml\System\Windows\Markup\StaticExtensionConverter.cs`）需要特殊处理：
- origin 中存在一个**功能等价但命名不同**的文件：`StaticExtensionsToInstanceDescriptorsConverter.cs`（同目录）。
- 这是命名未对齐 origin 的问题，可以通过重命名并加到 csproj 中解决。

**修复方向：**

对每个文件判断：
1. 是否可以直接删除（对应的类型在 origin 中本来就有，只是当前引用没接上）？
2. 如果必须保留，是否可以用更接近 origin 的方案替代？

优先从最简单、影响面最小的文件开始（例如 `SafeMemoryHandle.cs`）。

### 优先级 4：回退 `PresentationUI` 中 `: Grid` / `: ToolBar` 的基类修改

这是优先级 1 的子项，但可以在标记编译链恢复**之前**尝试：

- 先确认 origin 的 `PresentationUI.csproj` 中对 XAML 的 `Page` 标记编译配置
- 尝试在当前的 `PresentationUI.csproj` 中配置标记编译
- 如果能成功生成 `.g.cs`，`InstallationErrorPage` 和 `FindToolBar` 的基类就不需要手动写了

### 本轮已验证但尚未处理的静态问题（做个备忘）

- **`System.Printing` C++/CLI 项目仍未独立构建成功**，当前首个错误面在 bridge 缺少 `GeometryHelper`、`PrintSystemException`、`GDIExporter` 等更深层 API。这个项目在主链上不阻塞任何其他项目（`PresentationUI` 通过 `System.Printing-ref` 绕过了它），可以留在妥协代码清理之后处理。
- **`WindowsFormsIntegration` 显式 HintPath 仍未收敛**，但路径已从硬编码 `x64` 统一到 `$(WpfNativePlatform)`。当前不阻塞构建。
- **`ReachFramework-ref` 对 `System.Printing-ref` 的显式 HintPath** 已添加并验证通过。当前不阻塞构建。
- **Visual Studio `PresentationBuildTasks.dll` 锁文件问题**已记录在 `Docs/backlog.md`，不影响命令行 `msbuild -restore`。

## 需要先阅读的文档

1. `Docs/README.md`
2. `Docs/00-overview.md`
3. `Docs/01-phase-plan.md`
4. `Docs/03-origin-diff-audit.md`（注意其中"XAML partial 占位"的措辞不够准确，实际应以本交接文档为准）
5. `Docs/05-builder-plan.md`（Builder 构建器项目完善计划）
6. `Docs/04-NuGet-Binary.md`（native 模块 NuGet 二进制接入方案）
7. `Docs/cycle-breaker.md`

## 当前需要持续遵守的约束

- 禁止通过修改 `eng/Versions.props` 中 `AssemblyVersion` 来掩盖同名程序集冲突。
- 若出现 `Rect`、`Point`、`DependencyObject` 等基础类型错误，先检查是否同时引用了仓库项目和 SDK inbox 程序集。
- 不要通过把项目从解决方案中移除来制造"构建通过"。
- 清理妥协代码时，**始终对照 origin 对应文件**，不要凭记忆改。

## 推荐命令

- 解决方案：`msbuild Microsoft.Dotnet.Wpf.sln -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal`
- 单个项目：`msbuild <project>.csproj -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal /clp:ErrorsOnly`
- 运行 Builder：`dotnet run --project eng\Builder\Builder.csproj`
- 对比 origin：`git diff --no-index -- origin\src\...\File.cs src\...\File.cs`