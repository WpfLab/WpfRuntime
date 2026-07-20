# 当前概览

## 项目目标

将当前 WPF 仓库重组为一个更易维护的结构，逐步收敛项目依赖、共享源码路径和构建入口，最终达到以下目标：

- 可以直接在 Visual Studio 中打开并构建 `Microsoft.Dotnet.Wpf.slnx`。
- 可以使用一条 `msbuild` 命令对该解决方案或关键项目完成构建。
- 在重组过程中尽量保持与原始 WPF 仓库的目录结构和模块边界一致，降低后续同步和排障成本；对 `PenImc`、`WpfGfx` 这类高维护成本 native 模块，则改为优先接入已构建好的 NuGet 二进制包，而不是继续做源码迁移。

## 当前已验证事实

### 基础构建环境

- 仓库根目录存在 `Directory.Build.props`、`Directory.Build.targets`、`global.json`、`eng/Versions.props`。
- 当前统一目标框架为 `.NET 8`。
- `global.json` 指定 SDK 为 `8.0.101`，并允许 `latestFeature` 滚动，用于避免安装了 .NET 10 SDK 的 Visual Studio 首次打开时将 `net8.0` 项目解析到本机未安装的最新 8.0 targeting/apphost 包。
- 当前解决方案入口为 `Microsoft.Dotnet.Wpf.slnx`。
- `Directory.Build.props` 当前定义了：
  - `WpfSourceDir=$(RepoRoot)src\Microsoft.DotNet.Wpf\src\`
  - `WpfSharedDir=$(RepoRoot)src\Microsoft.DotNet.Wpf\src\Shared\`
  - `WpfCommonDir=$(RepoRoot)src\Microsoft.DotNet.Wpf\src\Common\`
  - `WpfCycleBreakersDir=$(RepoRoot)cycle-breakers\`
  - `WpfCodeGenDir=$(RepoRoot)eng\WpfArcadeSdk\tools\`
- 当前仓库仍依赖本地 `C:\lindexi\Lib\Microsoft.WindowsDesktop.App\` 作为部分引用路径。
- `Directory.Build.targets` 当前包含对 `WindowsBase`、`PresentationCore`、`PresentationFramework`、`ReachFramework`、`System.Printing` 的 inbox 引用清理逻辑，用于避免仓库项目与 SDK 隐式框架引用同时进入编译图。

### `global.json` SDK 选择原则

`global.json` 中的 `sdk.version` 是 .NET SDK 版本，不是运行时、目标框架或 NuGet 包版本。对于 .NET 8，常见 SDK 版本形态是 `8.0.100`、`8.0.101`、`8.0.200`、`8.0.300` 等；`8.0.0` 属于运行时/包版本形态，不是合适的 SDK 版本写法。

当前仓库目标框架为 `net8.0`，并包含 WPF/native/C++/CLI 构建链。`global.json` 不应使用 `rollForward: latestMajor` 来兼容“只安装 .NET 10 SDK”的机器，因为这会允许 SDK 从 8.0 直接滚动到 .NET 10。已验证在 VS/MSBuild 选中 .NET 10 SDK 时，`net8.0` 项目可能被解析到本机未安装的 `Microsoft.NETCore.App.Ref` / `Microsoft.NETCore.App.Host.win-x64` 最新 8.0 补丁包，例如 `8.0.28`，从而触发 `NETSDK1145`。

更稳妥的策略是使用 .NET 8 SDK 的最低可接受版本，并限制在 .NET 8 SDK feature band 内滚动，例如 `8.0.100` 或当前已验证的 `8.0.101` 搭配 `rollForward: latestFeature`。如果机器完全没有 .NET 8 SDK，应明确失败并提示安装 .NET 8 SDK，而不是隐式滚动到 .NET 10 后产生更隐蔽的构建错误。

### 当前已存在的顶层源码目录

`src/Microsoft.DotNet.Wpf/src/` 当前已确认存在：

- `Common`
- `DirectWriteForwarder`
- `Extensions`
- `PresentationBuildTasks`
- `PresentationCore`
- `PresentationFramework`
- `PresentationUI`
- `ReachFramework`
- `Shared`
- `System.Printing`
- `System.Windows.Controls.Ribbon`
- `System.Windows.Input.Manipulations`
- `System.Xaml`
- `Themes`
- `UIAutomation`
- `System.Windows.Presentation`
- `WindowsBase`
- `WindowsFormsIntegration`

### 当前已确认存在的主要项目文件

当前仓库磁盘上已能找到以下关键项目文件：

- 托管主项目
  - `src/Microsoft.DotNet.Wpf/src/WindowsBase/WindowsBase.csproj`
  - `src/Microsoft.DotNet.Wpf/src/System.Xaml/System.Xaml.csproj`
  - `src/Microsoft.DotNet.Wpf/src/PresentationCore/PresentationCore.csproj`
  - `src/Microsoft.DotNet.Wpf/src/PresentationFramework/PresentationFramework.csproj`
  - `src/Microsoft.DotNet.Wpf/src/PresentationUI/PresentationUI.csproj`
  - `src/Microsoft.DotNet.Wpf/src/ReachFramework/ReachFramework.csproj`
  - `src/Microsoft.DotNet.Wpf/src/System.Windows.Presentation/System.Windows.Presentation.csproj`
  - `src/Microsoft.DotNet.Wpf/src/System.Windows.Controls.Ribbon/System.Windows.Controls.Ribbon.csproj`
  - `src/Microsoft.DotNet.Wpf/src/System.Windows.Input.Manipulations/System.Windows.Input.Manipulations.csproj`
  - `src/Microsoft.DotNet.Wpf/src/WindowsFormsIntegration/WindowsFormsIntegration.csproj`
  - `src/Microsoft.DotNet.Wpf/src/UIAutomation/UIAutomationTypes/UIAutomationTypes.csproj`
  - `src/Microsoft.DotNet.Wpf/src/UIAutomation/UIAutomationProvider/UIAutomationProvider.csproj`
  - `src/Microsoft.DotNet.Wpf/src/UIAutomation/UIAutomationClient/UIAutomationClient.csproj`
  - `src/Microsoft.DotNet.Wpf/src/UIAutomation/UIAutomationClientSideProviders/UIAutomationClientSideProviders.csproj`
- Native / mixed 项目
  - `src/Microsoft.DotNet.Wpf/src/DirectWriteForwarder/DirectWriteForwarder.vcxproj`
  - `src/Microsoft.DotNet.Wpf/src/System.Printing/System.Printing.vcxproj`
  - `src/Microsoft.DotNet.Wpf/src/Shared/OSVersionHelper/OSVersionHelper.vcxproj`
- 参考程序集项目
  - `WindowsBase-ref`
  - `System.Xaml-ref`
  - `PresentationCore-ref`
  - `PresentationFramework-ref`
  - `PresentationUI-ref`
  - `ReachFramework-ref`
  - `System.Printing-ref`
  - `System.Windows.Presentation-ref`
  - `System.Windows.Input.Manipulations-ref`
  - `System.Windows.Controls.Ribbon-ref`
  - `UIAutomationTypes-ref`
  - `UIAutomationProvider-ref`
  - `UIAutomationClient-ref`
  - `UIAutomationClientSideProviders-ref`
  - `WindowsFormsIntegration-ref`
- 主题项目
  - `PresentationFramework.Aero`
  - `PresentationFramework.Aero2`
  - `PresentationFramework.AeroLite`
  - `PresentationFramework.Classic`
  - `PresentationFramework.Fluent`
  - `PresentationFramework.Luna`
  - `PresentationFramework.Royale`
- 其他项目
  - `src/Microsoft.DotNet.Wpf/src/Shared/Tracing/mcwpf/mcwpf.csproj`

### 当前已确认存在的 cycle-breaker 项目

`cycle-breakers/` 当前已确认存在：

- `cycle-breakers/PresentationFramework/PresentationFramework-PresentationUI-api-cycle.csproj`
- `cycle-breakers/PresentationFramework/PresentationFramework-ReachFramework-impl-cycle.csproj`
- `cycle-breakers/PresentationFramework/PresentationFramework-System.Printing-api-cycle.csproj`
- `cycle-breakers/PresentationFramework/PresentationFramework-System.Printing-impl-cycle.csproj`
- `cycle-breakers/PresentationUI/PresentationUI-PresentationFramework-impl-cycle.csproj`
- `cycle-breakers/ReachFramework/ReachFramework-PresentationFramework-api-cycle.csproj`
- `cycle-breakers/ReachFramework/ReachFramework-System.Printing-api-cycle.csproj`
- `cycle-breakers/System.Printing/System.Printing-PresentationFramework-api-cycle.csproj`

### 当前解决方案已纳入的项目

`Microsoft.Dotnet.Wpf.slnx` 当前已纳入：

- `System.Xaml`
- `WindowsBase`
- `WpfDemo`
- `System.Windows.Input.Manipulations`
- `UIAutomationTypes`
- `UIAutomationProvider`
- `DirectWriteForwarder`
- `Docs`
- `PresentationCore`
- `UIAutomationClient`
- `UIAutomationClientSideProviders`
- `ReachFramework`
- `PresentationFramework`
- `PresentationUI`
- `PresentationFramework.Classic`
- `PresentationFramework.Aero`
- `PresentationFramework.Aero2`
- `PresentationFramework.AeroLite`
- `PresentationFramework.Fluent`
- `PresentationFramework.Luna`
- `PresentationFramework.Royale`
- `System.Windows.Controls.Ribbon`
- `WindowsFormsIntegration`
- `PresentationBuildTasks`
- `System.Windows.Presentation`
- `System.Windows.Presentation-ref`
- `mcwpf`
- `PresentationFramework-PresentationUI-api-cycle`
- `PresentationFramework-ReachFramework-impl-cycle`
- `PresentationFramework-System.Printing-api-cycle`
- `PresentationFramework-System.Printing-impl-cycle`
- `PresentationUI-PresentationFramework-impl-cycle`
- `ReachFramework-PresentationFramework-api-cycle`
- `ReachFramework-System.Printing-api-cycle`
- `System.Printing-PresentationFramework-api-cycle`

### 当前已存在但尚未纳入解决方案的主要项目

以下项目已在磁盘中存在，但当前未出现在 `Microsoft.Dotnet.Wpf.slnx`：

- `System.Printing` native 项目

### 与原始仓库的顶层目录差异

相较原始 WPF 仓库，当前重组仓库仍保留以下 native 顶层模块的目录差异，但它们已不再作为后续源码迁移目标：

- `PenImc`
- `WpfGfx`

后续处理方式改为按目标框架和平台版本，从 NuGet 获取这些模块对应的已构建 DLL，用于满足 `DllImport` 依赖和主链编译，而不再把这两个模块纳入源码迁移清单。

## 当前构建状态

### 最新验证结果

使用当前工作区的整体构建入口重新验证后，`Microsoft.Dotnet.Wpf.slnx` 可构建：

- 结果：构建成功。
- Visual Studio 首次打开后的 native 平台映射修复：`DirectWriteForwarder.vcxproj` 在 `Microsoft.Dotnet.Wpf.slnx` 中已显式映射 `Any CPU -> x64`、`x64 -> x64`、`x86 -> Win32`、`arm64 -> ARM64`，避免 VS 设计时生成只看到 ARM64 平台引用或错误推断 C++ 项目平台。
- SDK 选择修复：`global.json` 已绑定 .NET SDK `8.0.101`。在只写 `msbuild-sdks` 而未指定 SDK 时，VS/MSBuild 会选择已安装的 .NET 10 SDK，并为 `net8.0` 解析到本机缺失的 `Microsoft.NETCore.App.Ref` / `Microsoft.NETCore.App.Host.win-x64` `8.0.28`，导致 `NETSDK1145`。绑定 .NET 8 SDK 后，`DirectWriteForwarder` 与解决方案入口均已恢复构建。
- 已验证命令：`msbuild Microsoft.Dotnet.Wpf.slnx -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal`，结果构建成功，仅剩既有警告。
- 额外修复：已将全部 `cycle-breakers/*.csproj` 纳入 `Microsoft.Dotnet.Wpf.slnx`。此前 `System.Printing-ref` 通过硬编码 `artifacts` 路径读取 `PresentationFramework-System.Printing-api-cycle` / `ReachFramework-System.Printing-api-cycle` 输出，但这两个桥接项目不在解决方案中，导致“旧产物残留时偶尔可过、干净构建或刚克隆仓库时 `msbuild -restore` 失败”。纳管后，解决方案入口会稳定先生成这些桥接输出，命令行干净构建已恢复。
- 额外修复：`Microsoft.Dotnet.Wpf.slnx` 先前缺失 `System.Xaml`、`System.Windows.Input.Manipulations`、`PresentationCore` 三个 solution folder 节点，导致 `NestedProjects` 指向不存在的父 GUID，`msbuild` 无法解析解决方案；现已补回缺失节点并重新验证解决方案入口可构建。
- 剩余警告：`DirectWriteForwarder.vcxproj` 仍报告 `D9035`，即 `/Zc:forScope-` 已否决并将在将来版本中移除。
- 当前解决方案已纳入 `PresentationBuildTasks` 和 `mcwpf` 项目。
- 清理后构建修复：已将磁盘上存在的全部 `ref/*.csproj` 纳入 `Microsoft.Dotnet.Wpf.slnx`。Visual Studio 在清理后 restore 时，如果 `PresentationCore-ref`、`WindowsFormsIntegration-ref` 等项目引用的 `WindowsBase-ref`、`UIAutomationTypes-ref`、`UIAutomationProvider-ref`、`PresentationFramework-ref`、`ReachFramework-ref`、`PresentationUI-ref` 不在解决方案中，会报 `NU1105`，随后级联为 `ReachFramework-ref` 缺少 `project.assets.json`、`PresentationFramework` / `PresentationUI` / 主题项目缺少 ref 输出。纳管全部 ref 项目后，`msbuild Microsoft.Dotnet.Wpf.slnx -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal /clp:ErrorsOnly` 已验证通过。
- Visual Studio 打开解决方案后构建曾再次出现 `PresentationUI` 标记编译 `MC1000`，日志首个真实链路为：`ReachFramework-ref` 缺少 `artifacts/obj/ReachFramework-ref/project.assets.json`，随后 `PresentationFramework` 缺少 `artifacts/obj/ReachFramework/.../ref/ReachFramework.dll`，最终 `PresentationUI` 的 XAML 编译找不到 `artifacts/bin/PresentationFramework/x64/Debug/net8.0/PresentationFramework.dll`。当前已在 `ReachFramework`、`PresentationFramework-ref`、`PresentationFramework`、`PresentationUI` 的关键 `ProjectReference` 上补充 `Targets="Restore;Build"`，让 IDE 项目级构建路径显式还原并构建上游项目。该修复已完成 XML 静态验证，尚需在 Visual Studio 中复验。

Visual Studio 默认 `Any CPU` 构建也已重新验证可通过：

- 命令：`msbuild C:\lindexi\Code\WpfReorganize_dotnetcampus\Microsoft.Dotnet.Wpf.slnx -restore /p:Configuration=Debug /p:Platform="Any CPU" /m:1 /v:minimal /clp:ErrorsOnly`
- 结果：构建成功。
- 当前处理方式：`Directory.Build.props` 将 `Any CPU` / `AnyCPU` 下的 `WpfNativePlatform` 映射到 `x64`，`PresentationUI` 与 `WindowsFormsIntegration` 的显式完整 `PresentationFramework` 引用使用该属性，避免 Visual Studio 构建时落到不存在的 `PresentationFramework\Any CPU\...` 输出目录。
- 额外收敛：`PresentationFramework.Classic`、`PresentationFramework.Aero`、`PresentationFramework.Aero2`、`PresentationFramework.AeroLite`、`PresentationFramework.Fluent`、`PresentationFramework.Luna`、`PresentationFramework.Royale` 与 `System.Windows.Controls.Ribbon` 对完整 `PresentationFramework` 的显式引用路径已统一改为 `$(WpfNativePlatform)`，不再硬编码 `x64`。
- `.pl` 文件弹窗原因已确认：主题生成目标通过 `Exec` 调用 `ThemeGenerator.pl` / `PreprocessXAML.pl`，当 `PerlCommand` 未定义或机器没有 `perl` 时，Windows 会尝试按 `.pl` 文件关联打开脚本。当前 `Directory.Build.targets` 增加 `VerifyPerlCommand`，缺少 Perl 时输出警告并跳过脚本执行，不再直接通过文件关联打开 `.pl`。如需重新生成主题产物，应安装 Perl 或设置 `PerlCommand` 指向有效 Perl 可执行文件。

针对“刚从 git 拉下来的项目也要能通过 `msbuild -restore`”的要求，已重新验证以下入口：

- `msbuild C:\lindexi\Code\WpfReorganize_dotnetcampus\Microsoft.Dotnet.Wpf.slnx -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal /clp:ErrorsOnly`
- `msbuild C:\lindexi\Code\WpfReorganize_dotnetcampus\Microsoft.Dotnet.Wpf.slnx -restore /p:Configuration=Debug /p:Platform="Any CPU" /m:1 /v:minimal /clp:ErrorsOnly`

两条命令当前均可重复执行并通过，不再依赖预先残留的 `artifacts` 桥接产物。

`WpfDemo` 已完成仓库 WPF 开发宿主的 Debug|x64 首期改造：

- 构建命令：`msbuild Demo\WpfDemo\WpfDemo.csproj -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /nr:false /v:minimal /clp:ErrorsOnly`。
- 在 Builder clean 后，仅执行上述一条命令即可传递构建 `PresentationFramework` 主链，并成功生成 WpfDemo。
- 编译和 XAML 使用实现项目自动生成的 `artifacts/obj/<Project>/x64/Debug/net8.0/ref/*.dll`，运行时使用各实现项目的 `artifacts/bin` 主输出；临时新增 `PresentationCore` public API 并由 WpfDemo 调用的验证已通过，测试代码随后已回退。
- WpfDemo 显式使用仓库 `PresentationBuildTasks/Microsoft.WinFX.targets`；最终求值的任务程序集为 `artifacts/bin/PresentationBuildTasks/x64/Debug/net472/PresentationBuildTasks.dll`。
- `WpfDemo.runtimeconfig.json` 仅声明 `Microsoft.NETCore.App`，不再声明 `Microsoft.WindowsDesktop.App`；`WpfDemo.deps.json` 已登记 app-local WPF 实现与共享 runtime PackageReference。
- 自动运行验证：使用 `WpfDemo.exe --verify-repo-wpf`，真实进程退出码为 0。报告确认 `WindowsBase`、`PresentationCore`、`PresentationFramework`、`DirectWriteForwarder`、`PenImc_cor3`、`PresentationNative_cor3`、`wpfgfx_cor3` 均从 WpfDemo 输出目录加载。
- `eng/WpfRuntimeDependencies.props` 现在是 Builder 与 WpfDemo 共用的 runtime 版本、包、managed/native 资产清单；Builder 不再单独硬编码 WindowsDesktop runtime 8.0.6 和运行时程序集名称。
- `Microsoft.Dotnet.Wpf.slnx` 已将 WpfDemo 的 `Any CPU` 与 `x64` 解决方案配置映射到项目 `x64`；x86/arm64 尚未支持。
- 尚未执行的验收项：在 Visual Studio 中把 WpfDemo 设为启动项目后实际 F5，并验证修改 `PresentationCore` 后再次 F5 的依赖重建与断点命中。

`ReachFramework-ref` 已补齐 `PresentationFramework-System.Printing-api-cycle` 中的 `ISerializerFactory` 与 `XpsDocumentWriter` 桥接 API，并可独立构建：

- 命令：`msbuild C:\lindexi\Code\God\WpfReorganize\src\Microsoft.DotNet.Wpf\src\ReachFramework\ref\ReachFramework-ref.csproj -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal`
- 结果：构建成功。
- 额外收敛：当前 `ReachFramework-ref.csproj` 已显式引用 `artifacts/obj/System.Printing-ref/.../ref/System.Printing.dll`，避免 `System.Windows.Xps.XpsDocumentWriter` 仅靠项目引用顺序解析而导致间歇性 `CS0234`。
- 额外收敛：`ReachFramework.csproj` 与 `PresentationFramework/ref/PresentationFramework-ref.csproj` 对 `ReachFramework-ref.csproj` 的引用已显式执行 `Restore;Build`，避免 IDE 项目级构建时跳过 ref 项目的 NuGet assets 生成。

`System.Windows.Presentation` 已从 `origin` 迁入当前仓库，并已纳入 `Microsoft.Dotnet.Wpf.slnx`：

- 命令：`msbuild C:\lindexi\Code\God\WpfReorganize\src\Microsoft.DotNet.Wpf\src\System.Windows.Presentation\System.Windows.Presentation.csproj -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal /clp:ErrorsOnly`
- 结果：构建成功。
- 当前处理方式：`BuildInfo.SystemWindowsPresentation` 已从 DevDiv 公钥调整为当前仓库使用的 WCP 公钥，使 `WindowsBase` 对 `System.Windows.Presentation` 的友元访问与当前强签名输出一致。

此前 `UIAutomationClientSideProviders` 缺失 `UIAutomationClient` 参考程序集的问题没有复现。`UIAutomationClient` 独立构建可产出实现程序集和 ref 相关输出；解决方案入口在清理后重建也可通过。

`ReachFramework` 实现项目已越过 `XpsSerializerWriter` / `XpsDocumentWriter` 调用链中的 `PrintTicket` 与 `XpsDocument` 类型身份不一致阻塞，并可独立构建：

- 命令：`msbuild C:\lindexi\Code\God\WpfReorganize\src\Microsoft.DotNet.Wpf\src\ReachFramework\ReachFramework.csproj -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal`
- 结果：构建成功。

`PresentationFramework` 实现项目已进入可独立构建状态：

- 命令：`msbuild C:\lindexi\Code\God\WpfReorganize\src\Microsoft.DotNet.Wpf\src\PresentationFramework\PresentationFramework.csproj -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal`
- 结果：构建成功。

`PresentationUI` 实现项目已进入可独立构建状态：

- 命令：`msbuild C:\lindexi\Code\God\WpfReorganize\src\Microsoft.DotNet.Wpf\src\PresentationUI\PresentationUI.csproj -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal /clp:ErrorsOnly`
- 结果：构建成功。
- 当前处理方式：`PresentationUI` 改为引用 `System.Printing-ref`，并显式引用完整 `PresentationFramework` 实现输出，避免 `System.Printing-ref` 依赖的 `PresentationFramework-System.Printing-api-cycle` 同名程序集覆盖完整 `PresentationFramework` API。
- 当前仍有迁移性占位：`InstallationError`、`TenFeetInstallationError`、`TenFeetInstallationProgress` 与 `FindToolBar` 增加了最小 XAML partial 占位成员，用于在 `InternalMarkupCompilation` 链路尚未完全接入时保持托管编译可推进。已重新验证：当前若直接回退到 origin 形态会因缺失 `.g.cs` 生成成员导致构建失败，因此这些占位仍属于当前必要补丁；后续需要先恢复真实标记编译生成代码，再移除占位。

`System.Windows.Controls.Ribbon` 与全部主题项目已进入可独立构建并已纳入解决方案：

- `System.Printing-ref` 已移除 `System.Windows.Xps.Packaging.XpsDocument` 占位，避免 `PresentationUI` 同时从 `ReachFramework` 与 `System.Printing` 解析到同名 `XpsDocument`。
- `PresentationFramework.Classic`、`PresentationFramework.Aero`、`PresentationFramework.Aero2`、`PresentationFramework.AeroLite`、`PresentationFramework.Fluent`、`PresentationFramework.Luna`、`PresentationFramework.Royale` 与 `System.Windows.Controls.Ribbon` 的实现项目和 ref 项目仍通过显式完整 `PresentationFramework` 输出补齐 `Thickness`、`Style`、`Control` 等完整 API，但路径已统一收敛到 `$(WpfNativePlatform)`，减少了对固定 `x64` 产物目录的耦合。
- `BuildInfo.SystemWindowsControlsRibbon` 已调整为 WCP 公钥，使 `PresentationCore` / `PresentationFramework` 对 Ribbon 的友元程序集声明与当前输出程序集强命名一致。

`System.Printing` C++/CLI 项目已越过早期 MSBuild 配置阻塞，但尚未可独立构建：

- 已修复：空 `WpfCppProps` 导入、旧 `TargetFrameworkIdentifier/TargetFrameworkVersion` 组合、默认 `v100` 平台工具集、缺失 `FilterItem1ByItem2` 自定义任务依赖、`/clr:pure` 与 .NET Core C++/CLI 不兼容。
- 当前首个错误面：已将 `System.Printing` 的 C++/CLI 引用从 `PresentationFramework-System.Printing-impl-cycle` / `ReachFramework` 实现程序集收窄到 `PresentationFramework-System.Printing-api-cycle` / `ReachFramework-System.Printing-api-cycle`，并显式把 `System.IO.Packaging.dll` 通过 `ForcedUsingFiles` 注入编译器。`ReachFramework-System.Printing-api-cycle` 现已继续补齐以下最小 bridge：
  - `PackagingProgressEventArgs.Action` / `NumberCompleted`
  - `PrintingCanceledException`、`PrintJobException`、`System.Printing.Interop` 占位命名空间
  - `XpsDocument` 的最小构造器、`GetFixedDocumentSequence`、`FixedDocumentSequenceReader`、`CreateSerializationManager` / `CreateAsyncSerializationManager` / `DisposeSerializationManager`
  - `IXpsFixedDocumentSequenceReader` / `IXpsFixedDocumentReader` / `IXpsFixedPageReader` 最小读取属性
  - `IXpsOMPackageWriter.Close`、`IPrintDocumentPackageTarget.Cancel`、`PrintDocumentPackageStatusProvider.JobIdAcquiredEvent` / `JobId`
  - `PrintTicket` 的公开构造、`SaveTo(Stream)`、`Clone()`
- 此前 `PackagingProgressEventArgs`、`PrintingCanceledException`、`System.Printing.Interop`、`XpsDocumentWriter` / `XpsDocumentNotificationLevel` 同名冲突与 `XpsDocument` 缺失成员已不再是首个失败点；当前新的首个错误面转为 `GDIExporter` / ReachFramework 更深层 API 缺口：
  - `System.Windows.Xps.Serialization.GeometryHelper.ArcToBezier`
  - `PrintSystemException` bridge 缺口
  - `Microsoft.Internal.GDIExporter.CNativeMethods.ExtTextOutW`
  - `Microsoft.Internal.AlphaFlattener.Utility.GetFontUri`
- 错误日志：`artifacts/system-printing-errors.log`。

`WindowsFormsIntegration` 已重新验证，当前未能独立构建：

- 命令：`msbuild C:\lindexi\Code\God\WpfReorganize\src\Microsoft.DotNet.Wpf\src\WindowsFormsIntegration\WindowsFormsIntegration.csproj -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal /clp:ErrorsOnly`
- 当前首个错误面：缺少完整 `PresentationFramework` 控件/API 引用，表现为 `FrameworkElement`、`Panel`、`Thickness`、`Window`、`AdornerDecorator` 等类型缺失，以及 `WindowsFormsHost` 对 `IKeyboardInputSink` 的接口签名不匹配。
- 错误日志：`artifacts/windowsformsintegration-errors.log`。

### 当前可直接确认的含义

1. 当前解决方案已在纳入 `ReachFramework`、`PresentationFramework`、`PresentationUI`、`PresentationFramework.Classic` 与 `System.Windows.Controls.Ribbon` 后恢复可构建基线。
2. 打印相关 `cycle-breakers` 已纳入解决方案，命令行 `msbuild -restore` 不再依赖旧 `artifacts` 产物才能通过。
3. 解决方案级构建报告仍保留 `DirectWriteForwarder` 的 `/Zc:forScope-` native 警告。
4. 多个主链项目已可独立构建，但仍存在 cycle-breaker、显式 HintPath 与同名程序集 API 暴露边界，后续仍需继续收敛。
5. `System.Printing` C++/CLI 项目已继续推进到 `GDIExporter` / ReachFramework 更深层源码编译阶段，但尚未构建成功；`PresentationUI` 当前通过 `System.Printing-ref` 绕过实现程序集阻塞。
6. Visual Studio 工作区“生成解决方案”当前仍存在单独阻塞：`PresentationBuildTasks.dll` 在 `net472` 输出复制阶段被多个 `MSBuild.exe` 进程锁定。该问题不会阻止命令行 `msbuild -restore` 成功，但仍属于 IDE 构建链待继续收敛的真实阻塞。
7. WpfDemo 已能作为仓库 WPF 的 app-local x64 开发测试宿主；命令行构建、自动 ref 新 API 传播、XAML、deps/runtimeconfig 和真实加载来源均已验证。

## 当前主要缺口

1. 当前解决方案已纳入项目可构建，但尚未纳管的主链项目仍需继续打通。
2. 解决方案纳管仍滞后于磁盘现状，至少以下主项目仍未进入 `Microsoft.Dotnet.Wpf.slnx`：
   - `System.Printing`（C++/CLI 实现项目仍失败，ref 项目可用）
3. `PenImc` 和 `WpfGfx` 不再走源码迁移路线，后续通过 NuGet 二进制 DLL 接入（详见 `Docs/04-NuGet-Binary.md`）。这两个模块的源码目录位于 `origin`，但不会复制到当前仓库 `src` 下。
4. `PresentationFramework` 依赖链虽然目录与项目文件已存在，但 cycle-breaker 的同名 API 暴露仍需要继续收敛：
   - `ReachFramework-ref` 所需的 `System.Windows.Xps.XpsDocumentWriter`、`System.Windows.Documents.Serialization.ISerializerFactory` 已由 `PresentationFramework-System.Printing-api-cycle` 暴露，参考程序集项目可独立构建。
   - `ReachFramework` 实现项目已通过动态调用边界越过 `XpsSerializerWriter` 与 `XpsDocument` 调用 `XpsDocumentWriter` 时的 `PrintTicket` / `XpsDocument` 类型身份不一致。
   - `PresentationFramework` 实现项目已通过动态调用边界越过打印相关 `XpsDocumentWriter`、`SerializerWriter`、`ISerializerFactory` 与 `PresentationUI` 中 `FindToolBar` 的迁移边界阻塞。
    - `PresentationUI`、`PresentationFramework.Classic` 与 `System.Windows.Controls.Ribbon` 已纳入解决方案，但当前依赖 XAML partial 占位、显式完整 `PresentationFramework` 引用与友元公钥调整，后续需恢复真实标记编译链路并收敛同名程序集解析。
   - `AvTrace` 代码生成目标在 `PresentationFramework` 主项目独立构建中未形成当前阻塞。
5. 迁移妥协代码清单（详见 `Docs/03-origin-diff-audit.md`）：
   - `ReachFramework` 内的 bridge 文件（`SafeMemoryHandle.cs`、`PrintQueueBridge.cs`、`DocumentReferenceBridge.cs`）
   - `WindowsBase` 内的 `CaseInsensitiveOrdinalStringComparer.cs`
   - `System.Xaml` 内的 `StaticExtensionConverter.cs`
   - `PresentationUI` 的 XAML partial 占位（`InstallationError`、`TenFeetInstallationError`、`TenFeetInstallationProgress`、`FindToolBar`）
   - `PresentationFramework` 打印链路的动态调用边界
   - 主题项目 / Ribbon / `PresentationUI` / `WindowsFormsIntegration` 对完整 `PresentationFramework` 输出的显式 HintPath 引用
6. 当前仓库仍保留个人机器本地引用路径，影响可移植性与构建复现性。

## 建议的当前优先级

1. 在 Visual Studio 中完成 WpfDemo 的 F5 验收，确认 `Any CPU -> x64` 映射、依赖重建、仓库 PresentationBuildTasks 与调试断点均正常。
2. 保持当前 `Microsoft.Dotnet.Wpf.slnx` 和 WpfDemo 命令行构建基线，不要为扩大纳管范围破坏现有项目。
3. 清理迁移妥协代码：
   - 优先恢复 `PresentationUI` 的真实标记编译生成链路，替换当前 XAML partial 占位成员。
   - 收敛 `ReachFramework` / `PresentationFramework` / `PresentationUI` 的动态边界和同名程序集解析。
   - 逐项评估 bridge 文件是否可替换为更接近 origin 的方案或更稳定的项目引用。
4. 继续处理 `System.Printing` C++/CLI 的 bridge 边界问题，当前优先补齐 `System.Windows.Xps.Serialization.GeometryHelper`、`PrintSystemException`、`Microsoft.Internal.GDIExporter.CNativeMethods.ExtTextOutW`、`Microsoft.Internal.AlphaFlattener.Utility.GetFontUri` 等更深层 API。
5. 继续收敛 Ribbon、主题项目、`PresentationUI` 与 `WindowsFormsIntegration` 对完整 `PresentationFramework` 显式 HintPath 的依赖。
6. 在 x64 验收稳定后，再参数化扩展 WpfDemo 的 x86/arm64 资产与解决方案映射。

## 当前执行约束

1. 若仓库中已存在 `WindowsBase.csproj`、`PresentationCore.csproj`、`PresentationFramework.csproj` 等同名项目，排查类型解析错误时必须先检查是否引入了第二份 inbox 程序集。
2. 禁止再次通过修改 `eng/Versions.props` 中的 `AssemblyVersion` 来掩盖同名程序集冲突。
3. 迁移工作中尽量把真实项目加入解决方案，并保持可构建；不要通过移除项目来规避构建问题。




`PresentationBuildTasks` 已完成 SDK 目标框架调整并加入解决方案：

- 原始问题:项目面向 `net9.0`,但当前 SDK 为 `8.0.206`,导致 `NETSDK1045` 错误。
- 解决方法:将目标框架从 `net472;net9.0` 改为 `net472;net8.0`,匹配当前 SDK 版本。
- 验证命令:`msbuild C:\lindexi\Code\God\WpfReorganize\src\Microsoft.DotNet.Wpf\src\PresentationBuildTasks\PresentationBuildTasks.csproj -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /clp:ErrorsOnly`
- 结果:构建成功。

`mcwpf` (事件跟踪代码生成工具) 已完成现代化改造并加入解决方案：

- 原始问题:项目使用旧的非 SDK 风格格式,导入不存在的内部构建系统 targets (`Microsoft.DevDiv.Settings.targets`、`Microsoft.DevDiv.targets`)。
- 解决方法:将项目改写为 SDK 风格 (`<Project Sdk="Microsoft.NET.Sdk">`),目标框架设为 `net8.0`,禁用自动生成 AssemblyInfo (`<GenerateAssemblyInfo>false</GenerateAssemblyInfo>`),并将模板文件 `wpf_template.cs` 排除在编译之外。
- 验证命令:`msbuild C:\lindexi\Code\God\WpfReorganize\src\Microsoft.DotNet.Wpf\src\Shared\Tracing\mcwpf\mcwpf.csproj -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /clp:ErrorsOnly`
- 结果:构建成功。

`WindowsFormsIntegration` 已重新验证可独立构建：

- 验证命令:`msbuild C:\lindexi\Code\God\WpfReorganize\src\Microsoft.DotNet.Wpf\src\WindowsFormsIntegration\WindowsFormsIntegration.csproj -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /clp:ErrorsOnly`
- 结果:构建成功。
- 当前含义:该项目已不再是当前首要阻塞点，后续重点转为收敛它对完整 `PresentationFramework` 输出的显式 HintPath 依赖。

`PresentationUI` 干净构建阻塞已修复（`Microsoft.WinFX.targets` 评估阶段 `_PresentationBuildTasksAssembly` 为空）：

- 原始问题:干净状态（无 `artifacts/`）下，`PresentationUI.csproj` 导入的 `Microsoft.WinFX.targets` 在评估阶段执行 `UsingTask`，此时 `PresentationBuildTasks.dll` 尚未构建，所有候选路径的 `Exists()` 检查返回 false，导致 `_PresentationBuildTasksAssembly` 属性求值为空字符串，触发 `MSB4022: AssemblyFile 特性值计算结果""无效`。
- 根本原因:MSBuild 的评估阶段（Evaluation）先于构建阶段（Build），`UsingTask` 在评估时注册程序集路径，但程序集仅在任务实际调用时才加载。`ProjectReference` 能保证构建顺序，但无法保证评估阶段 DLL 已存在。
- 解决方法:在 `Microsoft.WinFX.targets` 的 `_PresentationBuildTasksAssembly` 属性组末尾添加最终 fallback——当所有 `Exists()` 检查都失败时，直接使用预期构建输出路径 `$(_PresentationBuildTasksAssemblyWithPlatform)`。`UsingTask` 注册时不需要文件立即存在，程序集在任务调用时才加载，此时 `ProjectReference` 已保证 `PresentationBuildTasks` 先构建完成。
- 额外修复:`Directory.Build.props` 中 `WpfWindowsDesktopReferencePath` 原先硬编码 `8.0.6` 和 `8.0.26` 版本，不同机器安装的版本不同会导致干净构建失败。已改为使用 inline task（`RoslynCodeTaskFactory`）动态发现 `Microsoft.WindowsDesktop.App.Ref` 目录下最高版本的 `8.0.x` pack，无需硬编码版本号。对应地，`Directory.Build.targets` 中原先在顶层 `ItemGroup` 引用 `WpfWindowsDesktopReferencePath` 的逻辑已移入 `AddWindowsDesktopReferences` target（`BeforeTargets="ResolveAssemblyReferences"`），确保 inline task 先设置属性后再添加 Reference。
- 验证命令:`msbuild D:\lindexi\Code\WpfReorganize\Microsoft.Dotnet.Wpf.sln -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal /clp:ErrorsOnly`
- 结果:`MSB4022` 和 `CS0234` 错误均已消除。剩余唯一错误为 `DirectWriteForwarder.vcxproj` 的 `MSB4057`（目标"Build"不存在），原因是当前机器未安装 C++ 工作负载，属于环境依赖问题，非代码问题。

Builder 清理工具已新增（详见 `Docs/05-builder-clean.md`）：

- 用途:在干净状态下验证构建时，替代 `git clean -xdf`（会因 Visual Studio 锁定 `.vs/` 文件而失败）。
- 用法:`dotnet run --project eng\Builder\Builder.csproj -- clean`
- 清理范围:`artifacts/`、`src/**/bin/`、`src/**/obj/`、`Demo/**/bin/`、`Demo/**/obj/`、`cycle-breakers/**/bin/`、`cycle-breakers/**/obj/`、`.vs/`、仓库根目录 `*.log`。
- 锁定文件处理:对 `UnauthorizedAccessException` 和 `IOException` 进行捕获，跳过锁定文件并继续清理，不中断。
- 后续 AI 对话使用指引:需要干净状态验证时，不要使用 `git clean -xdf`，而是使用 `dotnet run --project eng\Builder\Builder.csproj -- clean`。

