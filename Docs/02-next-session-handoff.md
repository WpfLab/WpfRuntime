# 下一次 AI 对话交接

## 交接目标

该文档用于让后续 AI 直接接手当前仓库。WpfDemo Debug|x64 仓库 WPF 消费链路已经实施并通过命令行构建、静态产物和真实启动验证；不要重新回到“标准 SDK WPF + 系统共享框架”的旧方案。

## 当前构建基线

需要干净状态时使用 Builder clean，不要使用 `git clean -xdf`：

```powershell
dotnet run --project eng\Builder\Builder.csproj -- clean
msbuild Microsoft.Dotnet.Wpf.slnx -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /nr:false /v:minimal /clp:ErrorsOnly
msbuild Microsoft.Dotnet.Wpf.slnx -restore /p:Configuration=Debug /p:Platform="Any CPU" /m:1 /nr:false /v:minimal /clp:ErrorsOnly
```

两条解决方案命令当前均已验证成功。Builder clean 可能报告 Visual Studio 锁定的目录；应关闭 VS 后重试，不要依赖反复增量构建“凑”出成功结果。

## WpfDemo 当前状态

### 已完成

- `Demo/WpfDemo/global.json` 已删除，WpfDemo 继承根 `global.json` 的 .NET SDK 8.0.101。
- Demo 的 `Directory.Build.props/targets` 显式继承根配置，并导入 `eng/WpfDemo/RepoWpfConsumer.props/targets`。
- WpfDemo 首期限定 `Debug|x64`；解决方案将 WpfDemo 的 `Any CPU` 和 `x64` 均映射到项目 x64。
- `PresentationFramework.csproj` 作为 `Targets="Restore;Build"` 的顶层构建依赖，且 `ReferenceOutputAssembly=false`。
- C# 和 XAML 使用实现项目自动生成的 `artifacts/obj/<Project>/x64/Debug/net8.0/ref/*.dll`，不以手工 `*-ref.csproj` 作为新增 API 试验契约。
- WpfDemo 在 SDK targets 后显式导入仓库 `PresentationBuildTasks/Microsoft.WinFX.targets`；已验证实际任务程序集是 `artifacts/bin/PresentationBuildTasks/x64/Debug/net472/PresentationBuildTasks.dll`。
- 仓库实现程序集被登记到 `.deps.json` 并复制到 WpfDemo 输出；WindowsDesktop runtime native DLL、`ijwhost.dll` 和本地化资源也会部署。
- `WpfDemo.runtimeconfig.json` 只声明 `Microsoft.NETCore.App`，不声明 `Microsoft.WindowsDesktop.App`。
- `eng/WpfRuntimeDependencies.props` 是 Builder 与 WpfDemo 共用的 runtime 版本、PackageReference、managed/native 文件清单；不要再在两处新增常量。
- 已临时在 `PresentationCore` 新增 public API 并由 WpfDemo 调用，一条 WpfDemo 构建成功；临时测试代码已回退，证明自动 ref 传播契约成立。

### 构建与真实运行验证

```powershell
msbuild Demo\WpfDemo\WpfDemo.csproj -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /nr:false /v:minimal /clp:ErrorsOnly

$dir = Resolve-Path artifacts\bin\WpfDemo\x64\Debug\net8.0-windows
$process = Start-Process -FilePath "$dir\WpfDemo.exe" -ArgumentList '--verify-repo-wpf' -WorkingDirectory $dir -Wait -PassThru
$process.ExitCode
Get-Content "$dir\WpfDemo.repo-wpf-validation.txt"
```

已验证真实退出码为 0。报告确认以下模块均从 WpfDemo 输出目录加载：

- `WindowsBase.dll`
- `PresentationCore.dll`
- `PresentationFramework.dll`
- `DirectWriteForwarder.dll`
- `PenImc_cor3.dll`
- `PresentationNative_cor3.dll`
- `wpfgfx_cor3.dll`

注意：WpfDemo 是 WinExe，PowerShell 直接使用 `& WpfDemo.exe` 不会可靠等待 GUI 进程，也不能用当时的 `$LASTEXITCODE` 判断成功；必须使用 `Start-Process -Wait -PassThru`。

### 关键实现文件

| 文件 | 作用 |
|---|---|
| `eng/WpfRuntimeDependencies.props` | Builder/WpfDemo 共用版本、包和资产清单 |
| `eng/WpfDemo/RepoWpfConsumer.props` | 评估期平台、WinFX 开关、PackageReference/PackageDownload |
| `eng/WpfDemo/RepoWpfConsumer.targets` | 自动 ref 替换、runtime/deps、native/资源复制和产物校验 |
| `Demo/WpfDemo/WpfDemo.csproj` | x64 构建根、移除 WindowsDesktop WPF FrameworkReference、导入仓库 WinFX targets |
| `Demo/WpfDemo/Diagnostics/WpfRuntimeProbe.cs` | 托管与 native app-local 加载来源断言 |
| `eng/Builder/WpfRuntimeDefinition.cs` | Builder 读取共享 MSBuild 资产定义 |

## 下一步工作（按优先级）

### 优先级 0：Visual Studio F5 验收

1. 打开 `Microsoft.Dotnet.Wpf.slnx`。
2. 选择 Debug/x64；再验证解决方案 `Any CPU` 映射场景。
3. 将 WpfDemo 设为启动项目并 F5。
4. 确认窗口中的加载来源报告全部指向 WpfDemo 输出目录。
5. 修改一个 `PresentationCore` 方法体后再次 F5，确认依赖重建和断点命中。

当前命令行与真实进程验证均已通过，但 Visual Studio F5 尚未人工执行。若失败，优先查看首个真实错误，不要移除 WpfDemo 的 repo consumer 配置或重新加入 `Microsoft.WindowsDesktop.App.WPF`。

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

### 其他已知问题

- **`System.Printing` C++/CLI 项目仍未独立构建成功**，当前首个错误面在 bridge 缺少 `GeometryHelper`、`PrintSystemException`、`GDIExporter` 等更深层 API。这个项目在主链上不阻塞任何其他项目（`PresentationUI` 通过 `System.Printing-ref` 绕过了它），可以留在妥协代码清理之后处理。
- **`WindowsFormsIntegration` 显式 HintPath 仍未收敛**，但路径已从硬编码 `x64` 统一到 `$(WpfNativePlatform)`。当前不阻塞构建。
- **`ReachFramework-ref` 对 `System.Printing-ref` 的显式 HintPath** 已添加并验证通过。当前不阻塞构建。
- **Visual Studio `PresentationBuildTasks.dll` 锁文件问题**已记录在 `Docs/backlog.md`，不影响命令行 `msbuild -restore`。

## 需要先阅读的文档

1. `Docs/README.md`
2. `Docs/00-overview.md`
3. `Docs/01-phase-plan.md`
4. `Docs/06-wpfdemo-build-run-plan.md`
5. `Docs/07-wpfdemo-implementation.md`
6. `Docs/03-origin-diff-audit.md`（其中“XAML partial 占位”的措辞不够准确，实际状态见本文件）
7. `Docs/05-builder-plan.md`
8. `Docs/04-NuGet-Binary.md`
9. `Docs/cycle-breaker.md`

## 当前需要持续遵守的约束

- 禁止通过修改 `eng/Versions.props` 中 `AssemblyVersion` 来掩盖同名程序集冲突。
- 若出现 `Rect`、`Point`、`DependencyObject` 等基础类型错误，先检查是否同时引用了仓库项目和 SDK inbox 程序集。
- 不要通过把项目从解决方案中移除来制造"构建通过"。
- 清理妥协代码时，**始终对照 origin 对应文件**，不要凭记忆改。
- WpfDemo 编译必须继续使用实现项目自动 ref，运行必须继续使用 bin 实现；不要把普通 ProjectReference 输出直接作为 XAML 编译引用。
- WpfDemo 首期只支持 Debug|x64；在未参数化 native 包和 C++ 平台映射前，不要宣称 x86/arm64 已支持。

## 推荐命令

- 解决方案：`msbuild Microsoft.Dotnet.Wpf.slnx -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /nr:false /v:minimal /clp:ErrorsOnly`
- WpfDemo：`msbuild Demo\WpfDemo\WpfDemo.csproj -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /nr:false /v:minimal /clp:ErrorsOnly`
- 单个项目：`msbuild <project>.csproj -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal /clp:ErrorsOnly`
- 运行 Builder：`dotnet run --project eng\Builder\Builder.csproj`
- 对比 origin：`git diff --no-index -- origin\src\...\File.cs src\...\File.cs`