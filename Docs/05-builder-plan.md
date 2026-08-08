# Builder 构建、打包与包验证

## 文档定位

本文说明 `eng/Builder` 当前已经落地的命令、服务边界、逐项目构建、NuGet 组包和隔离消费验证。仓库整体项目清单与根解决方案构建状态仍以 [00-overview.md](00-overview.md) 为准，本文不重复或外推根 `Microsoft.Dotnet.Wpf.slnx` 的整体状态。

Builder 是 `net8.0` 控制台项目，自身输出固定在 `eng/Builder/bin/`，避免清理 `artifacts/` 时删除正在运行的工具。它只实现 Windows 上的 x64 和 x86 构建、打包与验证路径；arm64 尚未实现。

## 命令入口

先还原并构建 Builder，使 `PackageDownload` 资产完成还原，并由 MSBuild 生成 `eng/Builder/bin/PackagePaths.txt`：

```powershell
dotnet restore eng/Builder/Builder.csproj
dotnet build eng/Builder/Builder.csproj --no-restore
```

可用命令如下：

| 命令 | 作用 |
|---|---|
| `dotnet run --project eng/Builder/Builder.csproj --no-build -- --version 1.0.0` | 执行默认构建命令：清理 `artifacts/bin`、`artifacts/obj` 与 staging，逐项目构建 x64/x86、收集资产、打包并生成比较报告 |
| `dotnet run --project eng/Builder/Builder.csproj --no-build -- clean` | 清理已知构建输出；详细边界见 [05-builder-clean.md](05-builder-clean.md) |
| `dotnet run --project eng/Builder/Builder.csproj --no-build -- compare` | 将 staging 中已收集的参考程序集与官方 `Microsoft.WindowsDesktop.App.Ref` 做缺失项和大小差异比较；应先完成默认 Builder 构建 |
| `dotnet run --project eng/Builder/Builder.csproj --no-build -- test-package` | 选择 `eng/Builder/bin/nupkg/` 中最新的包并执行隔离消费矩阵 |
| `dotnet run --project eng/Builder/Builder.csproj --no-build -- test-package --package <nupkg-path>` | 验证显式指定的包 |
| `dotnet eng/Builder/bin/Builder.dll ci-build --repository <tested-repository> --target solution` | GitHub Actions 受信任入口：复核事件与 checkout 身份、检查凭据残留，并在脱敏环境中重建根解决方案 |
| `dotnet eng/Builder/bin/Builder.dll ci-build --repository <tested-repository> --target package` | GitHub Actions 受信任入口：计算版本与 artifact 身份，完整构建/打包/验证，并安全写入 `GITHUB_OUTPUT` |
| `dotnet eng/Builder/bin/Builder.dll comment-pr-artifacts` | `workflow_run` 受信任入口：通过 Octokit 复核 run/PR/artifact 元数据并幂等创建或更新 bot 评论 |

命令从 Builder 输出目录向上查找 `.git` 来定位仓库根。若直接使用 `--no-build`，调用方必须保证 Builder 已构建且 `PackagePaths.txt` 与当前还原结果一致。

`ci-build` 与 `comment-pr-artifacts` 默认读取 GitHub Actions 提供的 `GITHUB_EVENT_PATH`、`GITHUB_EVENT_NAME`、`GITHUB_SHA`、`GITHUB_RUN_ID`、`GITHUB_RUN_ATTEMPT`、`GITHUB_REPOSITORY`、`GITHUB_OUTPUT` 和 `GITHUB_STEP_SUMMARY`；评论命令另从环境变量读取 `GITHUB_TOKEN`，不接受把 token 放入命令行。只执行元数据或受信任编排命令时，可以用 `-p:RestoreWpfRuntimePackages=false` 还原/构建 Builder，跳过默认构建才需要的 WindowsDesktop `PackageDownload`；默认值仍为 `true`。

## 服务拆分

| 组件 | 当前职责 |
|---|---|
| `Program.cs` | 注册默认构建、`clean`、`compare`、`test-package`、`relay-pr`、`ci-build` 和 `comment-pr-artifacts` 命令 |
| `BuildService` | 编排清理、逐项目构建、资产收集、包校验、打包和报告比较 |
| `MsBuildService` | 通过 `vswhere`、`PATH` 和 Visual Studio 常见安装目录查找 `MSBuild.exe`，并管理逐项目诊断日志 |
| `CleanService` | 清理已知输出，并对锁定文件或目录进行跳过和统计 |
| `AssemblyCollector` | 从每个项目自己的输出目录收集参考程序集和实现程序集；实现收集优先使用目标平台目录，最后允许通用 Debug 输出回退 |
| `WpfRuntimeDefinition` | 读取 `eng/WpfRuntimeDependencies.props` 与 `eng/Versions.props` 中的托管程序集和运行时 NuGet 依赖定义 |
| `NuGetPackageService` | 解析还原包路径、收集 native 资产、生成 `buildTransitive` targets 与 nuspec、校验包资产并执行打包 |
| `CompareService` | 与官方参考程序集做清单和尺寸级报告比较；不进行 API 二进制兼容性证明 |
| `PackageTestService` | 动态创建隔离消费项目，发布、校验包资产哈希并运行 WPF 探针 |
| `ProcessRunner` | 运行外部进程、合并标准输出和错误输出，并对探针执行超时终止 |
| `GitHubActionsBuildService` | 由受信任 Builder 校验 tested checkout 的凭据、事件 SHA/merge 双亲与 Git 状态，并在脱敏隔离环境中执行 solution 或 package 门禁 |
| `GitHubArtifactCommentService` | 通过 Octokit 分页读取 workflow run、PR、artifact 与评论元数据，执行最新运行判定、artifact 身份筛选和 bot 评论幂等回写 |
| `GitHubWorkflowRunEvent` / `GitHubArtifactCommentFormatter` | 严格解析事件 JSON，并集中生成 marker、Markdown 安全文本、大小与链接展示 |

## Visual Studio MSBuild 依赖

Builder 当前明确依赖 Visual Studio MSBuild。构建清单包含 C++/CLI 项目 `DirectWriteForwarder.vcxproj`，因此需要带 MSBuild 和相应 C++ 工具链的 Visual Studio 或 Visual Studio Build Tools 环境。

`MsBuildService` 优先调用 Visual Studio Installer 提供的 `vswhere.exe` 查找 MSBuild，随后才检查 `PATH` 和常见安装目录。CI 也通过 `microsoft/setup-msbuild` 配置 Visual Studio MSBuild。仅安装 .NET SDK 不能保证 C++/CLI 路径可用。

## 逐项目 x64/x86 构建

默认构建命令不调用根 `Microsoft.Dotnet.Wpf.slnx`，而是在 `BuildService` 中按硬编码依赖顺序逐个调用 MSBuild：

1. 先分别为 x64 和 x86 构建 `PresentationBuildTasks.csproj`，目标框架为 `net472`。
2. 再分别遍历 x64 和 x86，构建核心 WPF、UIAutomation、`DirectWriteForwarder`、扩展程序集和七个主题项目。
3. 所有项目使用 `Debug`、`-restore`、`/m:1`、`/nr:false`；除 `PresentationBuildTasks` 自身外，后续项目要求使用预构建的任务程序集。
4. C# 项目使用 `x64` 或 `x86` 平台；C++ 项目在 x86 路径中映射为 `Win32`，因此当前 `DirectWriteForwarder` 的两条 native 平台路径是 `x64` 和 `Win32`。
5. 每个项目的诊断日志写入 `artifacts/log/Builder/<Project>-<Platform>.log`。

项目构建失败后，Builder 会记录失败项并继续尝试其余项目，以便保留可诊断的部分结果。若后续仍能完成打包，最终退出码为 `2`；任一 RID 的实现程序集收集结果为空、参考程序集收集结果为空，或硬编码的关键包资产缺失时会直接失败。该行为不能把部分打包解释为全部项目构建成功，也不能证明所有非关键资产均已收齐。

当前项目构建顺序仍由 `BuildService` 的数组维护，尚未迁入共享项目图或 `eng/WpfRuntimeDependencies.props`。新增、删除或重命名打包项目时，必须同时检查该硬编码清单。

## ref、RID 实现与 native 资产收集

### 参考程序集

`AssemblyCollector` 根据 `eng/WpfRuntimeDependencies.props` 的 `RepoWpfRuntimeAssembly` 清单筛选 `artifacts/bin/*-ref/`，优先查找 x64、AnyCPU 和通用 Debug 输出中的项目主 DLL，写入：

```text
eng/Builder/bin/staging/ref/net8.0/
```

标记为 `PackReference="false"` 的程序集不会进入 `ref/net8.0`。

### RID 实现程序集

实现程序集同样按共享的 `RepoWpfRuntimeAssembly` 名称筛选，并只取与项目目录同名的主 DLL，避免项目目录中的传递副本覆盖正确产物。搜索顺序优先使用目标平台目录，随后允许通用 Debug 输出回退：

```text
artifacts/bin/<Project>/x64/Debug/net8.0/<Project>.dll
artifacts/bin/<Project>/x64/Debug/<Project>.dll
artifacts/bin/<Project>/x86/Debug/net8.0/<Project>.dll
artifacts/bin/<Project>/x86/Debug/<Project>.dll
artifacts/bin/<Project>/Win32/Debug/<Project>.dll
artifacts/bin/<Project>/Debug/net8.0/<Project>.dll
artifacts/bin/<Project>/Debug/<Project>.dll
```

收集结果分别进入：

```text
eng/Builder/bin/staging/runtimes/win-x64/lib/net8.0/
eng/Builder/bin/staging/runtimes/win-x86/lib/net8.0/
```

x86 收集会依次接受 `x86` 和 `Win32` 输出，以覆盖托管项目与 C++/CLI 项目的平台命名差异。由于 x64 与 x86 最后都允许通用输出回退，资产被放入不同 RID 目录并不单独证明其二进制按架构隔离；需要结合产物架构或运行验证判断。

### Native 资产

当前 WindowsDesktop native 版本由 `eng/WpfRuntimeDependencies.props` 固定为 `8.0.6`。`Builder.csproj` 使用以下 `PackageDownload`，而不是 `PackageReference GeneratePathProperty`：

- `Microsoft.WindowsDesktop.App.Runtime.win-x64`
- `Microsoft.WindowsDesktop.App.Runtime.win-x86`
- `Microsoft.WindowsDesktop.App.Ref`
- `Microsoft.NETCore.App.Host.win-x64`
- `Microsoft.NETCore.App.Host.win-x86`

Builder 构建时使用 `$(NuGetPackageRoot)` 和共享版本写出 `PackagePaths.txt`。打包阶段从两个 WindowsDesktop runtime 包的 `runtimes/<rid>/native/` 复制 DLL，并从对应 host 包补充 `ijwhost.dll`。

共享 props 已定义 `RepoWpfNativeRuntimeFile`，但 Builder 当前仍会复制 runtime 包 native 目录中的全部 DLL，并在 `ValidatePackageAssets` 中硬编码检查 `ijwhost.dll`、`PenImc_cor3.dll`、`PresentationNative_cor3.dll` 和 `wpfgfx_cor3.dll`；此外还会检查 `lib/net8.0` 中与 `DirectWriteForwarder.dll` 同目录的 `ijwhost.dll`。因此 Builder 的 native 必需文件规则与共享清单尚未完全统一。

### C++/CLI 宿主依赖的部署约定

`ijwhost.dll` 必须同时写入 `runtimes/<rid>/native/` 和 `runtimes/<rid>/lib/net8.0/`。`DirectWriteForwarder.dll` 是从 RID 专属 `lib/net8.0` 目录加载的 C++/CLI 程序集；NuGet 的 `native` 资产分类只影响资产选择与复制，不会让 Windows Loader 自动搜索相邻的 `native` 目录。若只保留 native 目录中的副本，消费应用可能在 WPF 模块初始化期间因无法解析该间接依赖而退出。

因此，Builder 除保留标准 native 资产副本外，还会把 `ijwhost.dll` 放到 `DirectWriteForwarder.dll` 所在目录，并在打包前校验这两个位置。相关 C++/CLI 运行时加载背景见 [dotnet/runtime#38231](https://github.com/dotnet/runtime/issues/38231)。

## NuGet 包结构与消费逻辑

包 ID 为 `DotNetCampus.WpfLib`。当前包布局为：

```text
DotNetCampus.WpfLib.<version>.nupkg
├─ ref/net8.0/*.dll
├─ runtimes/win-x64/lib/net8.0/*.dll（包含 ijwhost.dll）
├─ runtimes/win-x64/native/*.dll（包含 ijwhost.dll）
├─ runtimes/win-x86/lib/net8.0/*.dll（包含 ijwhost.dll）
├─ runtimes/win-x86/native/*.dll（包含 ijwhost.dll）
└─ buildTransitive/DotNetCampus.WpfLib.targets
```

nuspec 为 `net8.0` 和 `net9.0` 写入运行时包依赖组，依赖版本来自 `eng/WpfRuntimeDependencies.props` 和 `eng/Versions.props`。实现程序集仍是 `net8.0` 资产并写入 RID 目录；公共 `lib/net8.0` 不承载这些实现。通用输出回退可能让同一托管 DLL 同时进入两个 RID，不能仅凭目录布局断言二进制架构不同。

`buildTransitive/DotNetCampus.WpfLib.targets` 承担以下消费行为：

- 移除 `Microsoft.WindowsDesktop.App.WPF` FrameworkReference。
- 在解析引用后按文件名移除选定的 WPF 同名引用，并注入包内 `ref/net8.0`；当前实现不区分这些引用来自 inbox、显式引用还是其他包。
- 当 `RuntimeIdentifier` 为 `win-x64` 或 `win-x86` 时，选择对应的托管实现和 native DLL。
- 在普通 Build 与 Publish 后把 RID 资产复制到应用输出目录。

打包前会校验两个 RID 的核心 ref、实现、native 和 `buildTransitive` 文件。实际 `dotnet pack` 使用系统临时目录中的最小 SDK 项目，避免临时 pack 项目继承仓库根构建导入；生成的包写入 `eng/Builder/bin/nupkg/`。

构建末尾还会以报告模式比较官方 `Microsoft.WindowsDesktop.App.Ref`。该比较只检查清单缺失与显著尺寸差异，且报告模式不会让完整构建命令失败，不能替代 API、加载或运行验证。独立运行 `compare` 时应先确保 `staging/ref/net8.0` 已由完整 Builder 构建生成；当前无 staging 的回退只选择收集结果中一个目录，可能产生不完整报告。

## PackageTestApp 隔离消费模板

`eng/Builder/PackageTestApp/PackageTestApp.csproj` 是动态隔离消费模板，不是仓库 WPF 主链实现项目，因此未直接纳入根 `Microsoft.Dotnet.Wpf.slnx`。`Builder.csproj` 只把模板文件作为内容纳入自身项目；`PackageTestService` 在每次验证时将模板复制到新的 `eng/Builder/bin/package-tests/<timestamp>-<id>/` 目录，再动态修改目标框架、程序集名和待测包版本。

隔离目录还会生成独立的：

- `global.json`：选择 .NET 9 SDK，并允许向更高主版本滚动。
- `NuGet.Config`：只显式配置待测包目录和 nuget.org。
- `restore-packages/`：与仓库常规还原目录隔离。
- `extracted-package/`：用于将发布文件与 nupkg 内资产逐文件比较。

模板会创建窗口、加载 XAML 控件和资源、触发路由事件，并确认 `WindowsBase`、`PresentationCore`、`PresentationFramework` 从发布目录加载。探针还要求这些包内 WPF 实现程序集保持 `.NETCoreApp,Version=v8.0`，即使消费应用目标为 .NET 9。

## 包验证矩阵

`test-package` 动态创建三个消费项目：

| 项目 | 目标框架 |
|---|---|
| `SingleNet8` | `net8.0-windows` |
| `SingleNet9` | `net9.0-windows` |
| `MultiTarget` | `net8.0-windows;net9.0-windows` |

验证服务先检查 nuspec 中 net8.0/net9.0 依赖组和共享运行时包版本。随后，每个目标框架都分别执行 `win-x86` 和 `win-x64` 的 self-contained Publish，共形成八个发布与运行组合；每个组合执行：

1. 校验发布目录包含每个运行时 NuGet 依赖的主 DLL。
2. 将包内对应 RID 的实现和 native DLL 与发布目录逐文件做 SHA-256 比较。
3. 启动发布后的 WPF 应用，验证 XAML、资源、控件、事件和程序集加载来源。
4. 对单个探针设置 30 秒超时；超时会终止整个进程树，非零退出码视为失败。

该矩阵验证的是生成包的隔离消费契约，不等同于根解决方案全部项目、Visual Studio F5、非自包含发布或 arm64 验证。

## CI 路径

`.github/workflows/build.yml` 包含两个相互独立的 Windows job：

- `build-solution`：分别 checkout `github.sha` 的受信任 Builder 与待测试 commit/PR merge ref；轻量构建受信任 Builder 后，由 `ci-build --target solution` 复核身份并对 tested checkout 执行根 `Debug|x64` Rebuild。
- `build-package`：使用相同的 trusted/tested 双 checkout，由 `ci-build --target package` 在脱敏环境中还原并构建 tested Builder、运行默认 x64/x86 构建与打包、验证精确 nupkg，并生成 artifact 名称和绝对包路径。

两个 job 的 YAML 只保留固定版本 Action、环境准备和单行 Builder 调用，不再维护 PowerShell 身份计算或构建脚本。`build-package` 中任一此前步骤失败时，带 `failure()` 条件的诊断步骤会尝试上传 tested checkout 的 `eng/Builder/bin/package-tests`；构建与包测试均成功时，后续步骤按 C# 输出的精确路径上传 nupkg。workflow 中配置了这些步骤，不代表任意本地工作区或最新远端运行已经通过，实际结论必须以对应运行日志和产物为准。

`.github/workflows/comment-pr-build-artifacts.yml` 只 checkout `github.sha` 的受信任 Builder，不 checkout PR ref，也不下载 artifact；它在 Ubuntu 上轻量构建 Builder，再以单行命令调用 `comment-pr-artifacts`。只有该最终命令步骤获得 `GITHUB_TOKEN`，Restore/Build 步骤不持有写 token。

## 当前验证边界

- Builder 已实现 x64 和 x86；arm64 没有 PackageDownload、构建循环、包目录或测试矩阵实现。
- C++/CLI 路径依赖 Visual Studio MSBuild、`vswhere` 可发现的安装和对应 C++ 工具链，不能仅以 `dotnet` SDK 可用推断 Builder 可运行。
- 默认 Builder 构建使用自己的逐项目硬编码清单，不代表根 `Microsoft.Dotnet.Wpf.slnx` 的完整构建状态；整体状态只查阅 [00-overview.md](00-overview.md)。
- native runtime 版本当前为 `8.0.6`；路径来自 `PackageDownload`、`$(NuGetPackageRoot)` 和 `PackagePaths.txt`，未使用 `GeneratePathProperty`。
- 托管程序集名和运行时包依赖已读取共享定义；逐项目构建顺序、native 全量复制和关键 native 文件校验仍有 Builder 内部硬编码，尚未完全收敛到共享清单。
- `compare` 是清单/尺寸报告，不证明 API 兼容、强名称一致或运行时行为；没有完整 staging 时的回退结果也不能作为完整清单依据。
- 未显式传入 `--package` 时，`test-package` 只选择输出目录中最后写入的包；应结合包路径和时间戳确认验证对象。
- 当前工作区检查时，`eng/Builder/bin/nupkg/` 与 `eng/Builder/bin/package-tests/` 均不存在，因此本文不宣称刚完成了全矩阵包验证。需要当前证据时，应重新生成包、执行 `test-package` 并保留对应日志与产物。