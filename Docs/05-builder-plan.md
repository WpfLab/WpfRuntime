# Builder 项目完善计划：构建驱动 + NuGet 打包

## 技术背景

### 当前状态

- ✅ `eng\Builder\Builder.csproj` 已实现（net8.0 控制台，LangVersion 12，独立 OutputPath）
- ✅ `eng\Builder\Program.cs` 已实现完整编排逻辑（~280 行）
- ✅ Builder 不构建 sln，而是按依赖顺序逐项目调用 msbuild 构建具体 csproj（避免 Builder 自锁）
- Builder 分别构建 x64 与 x86 运行时程序集；C# 项目的 x86 平台映射到 C++/CLI 项目的 Win32 平台
- 产物输出到 `artifacts\bin\<ProjectName>\x64\Debug\net8.0\`、`artifacts\bin\<ProjectName>\x86\Debug\net8.0\`，C++/CLI x86 产物位于 `Win32\Debug\`
- Native 模块（PenImc、WpfGfx）不再走源码迁移，改为 NuGet 二进制接入
- 托管项目均 target `net8.0`，native 项目（`DirectWriteForwarder`、`System.Printing`）为 C++/CLI
- `Directory.Build.props` 定义了统一的 `WpfSourceDir`、`WpfSharedDir` 等宏

### 工具限制（重要，后续对话需注意）

- `origin\NuGetPackage\` 被 `origin\.gitignore` 忽略，**只能用命令行（dir / Get-ChildItem / type）查询**，不能用工具调用访问
- `Documentation\packaging.md`、`eng\copy-wpf.ps1` 等文件同样受 gitignore 影响，`get_file` 无法直接读取，但 `code_search` 可匹配到片段
- 后续对话中如需查看 `origin\NuGetPackage\` 内容，必须通过 `run_command_in_terminal` 执行命令行

### 设计决策

| 项 | 值 | 说明 |
|---|---|---|
| 包 ID | `DotNetCampus.WpfLib` | 与旧 `DotnetCampus.CustomWpf` 区分 |
| 版本 | `1.0.x` | 若多 TFM 不可行则改为 `8.0.x.xx` |
| 作者 | `dotnet campus` | |
| TFM | `net8.0` | 优先，后续评估多 TFM 兼容 |
| 产物路径 | 沿用 `artifacts\bin\` | 构建完成后从此拷贝，不改变原有结构 |
| 架构 | 单包含 win-x64 + win-x86 | 通过 NuGet RID 自动选择实现程序集与 native DLL |
| ref 文件夹 | `ref/net8.0` | 为两个架构提供统一编译引用 |

---

## NuGet 包目标结构

```
DotNetCampus.WpfLib.1.0.0.nupkg
├── ref/
│   └── net8.0/
│       ├── WindowsBase.dll
│       ├── System.Xaml.dll
│       ├── PresentationCore.dll
│       ├── PresentationFramework.dll
│       ├── PresentationUI.dll
│       ├── ReachFramework.dll
│       ├── System.Windows.Presentation.dll
│       ├── System.Windows.Controls.Ribbon.dll
│       ├── System.Windows.Input.Manipulations.dll
│       ├── WindowsFormsIntegration.dll
│       ├── UIAutomationTypes.dll
│       ├── UIAutomationProvider.dll
│       ├── UIAutomationClient.dll
│       ├── UIAutomationClientSideProviders.dll
│       ├── PresentationFramework.Aero.dll
│       ├── PresentationFramework.Aero2.dll
│       ├── PresentationFramework.AeroLite.dll
│       ├── PresentationFramework.Classic.dll
│       ├── PresentationFramework.Fluent.dll
│       ├── PresentationFramework.Luna.dll
│       └── PresentationFramework.Royale.dll
├── runtimes/
│   ├── win-x64/
│   │   ├── lib/net8.0/       ← x64 WPF 实现程序集，含 DirectWriteForwarder.dll
│   │   └── native/           ← x64 PenImc、WpfGfx 等 native DLL
│   └── win-x86/
│       ├── lib/net8.0/       ← x86 WPF 实现程序集，含 DirectWriteForwarder.dll
│       └── native/           ← x86 PenImc、WpfGfx 等 native DLL
├── DotNetCampus.WpfLib.nuspec
└── [Content_Types].xml
```

---

## 步骤

### 步骤 1：用命令行探索 `origin\NuGetPackage\` 目录结构

使用 `dir` / `Get-ChildItem -Recurse` 了解原始 NuGet 包的组织方式：
- 有哪些 `.nuspec` 模板
- `Directory.Build.props` / `Directory.Build.targets` 的内容
- 文件夹布局（`lib/`、`runtimes/`、`ref/` 等）
- 打包脚本或 MSBuild 目标

作为 Builder 设计依据。

### 步骤 2：用命令行探索 NuGet 缓存中的 DLL 清单

枚举以下路径的完整 DLL 列表：
- `C:\Users\{user}\.nuget\packages\microsoft.windowsdesktop.app.runtime.win-x64\{version}\runtimes\win-x64\lib\net8.0\`
- `C:\Users\{user}\.nuget\packages\microsoft.windowsdesktop.app.runtime.win-x64\{version}\runtimes\win-x64\native\`
- `C:\Users\{user}\.nuget\packages\microsoft.windowsdesktop.app.runtime.win-x86\{version}\runtimes\win-x86\native\`

确定：
- 需要打包的托管 DLL 完整清单
- 需要打包的 native DLL 完整清单（PenImc、WpfGfx 等）
- 各平台 native DLL 的差异

### 步骤 3：改造 `Builder.csproj`

- 添加 `Microsoft.WindowsDesktop.App.Runtime.win-x64` 和 `win-x86` 的 `PackageReference`，带 `GeneratePathProperty="true"`
- 添加 `System.Text.Json` 源生成支持（AOT 兼容）
- 配置输出目录不落在 `artifacts\` 内（避免清空时自毁）
- 设置 `OutputPath` 为 `eng\Builder\bin\` 或类似独立路径

### 步骤 4：实现清理逻辑

每次构建前：
- 删除 `artifacts\bin\` 和 `artifacts\obj\`（或整个 `artifacts\`）
- 确保工作干净，避免旧产物污染
- 注意：Builder 自身输出不能落在 `artifacts\` 内

### 步骤 5：实现构建驱动逻辑

调用 `msbuild` 构建解决方案：
- 命令：`msbuild Microsoft.Dotnet.Wpf.sln -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal`
- 捕获构建输出和退出码
- 构建失败时输出错误信息并中止

### 步骤 6：实现托管 DLL 收集逻辑

从 `artifacts\bin\` 收集产物到 staging 目录：
- 遍历 `artifacts\bin\*\Debug\net8.0\*.dll`
- 按项目名映射到托管 DLL 清单
- 拷贝到 staging `lib\net8.0\` 目录
- 排除 ref 项目、测试项目、工具项目（如 `PresentationBuildTasks`、`mcwpf`）
- 输出收集到的文件列表

### 步骤 7：实现 Native DLL 收集逻辑

从 NuGet 包路径拷贝 native DLL：
- 源路径：`$(PkgMicrosoft_WindowsDesktop_App_Runtime_win_x64)\runtimes\win-x64\native\`
- 源路径：`$(PkgMicrosoft_WindowsDesktop_App_Runtime_win_x86)\runtimes\win-x86\native\`
- 目标：staging `runtimes\win-x64\native\` 和 `runtimes\win-x86\native\`
- 通过 MSBuild 属性 `PkgMicrosoft_WindowsDesktop_App_Runtime_win_x64` 获取路径
- 或直接在 C# 代码中通过 `Environment.GetFolderPath` 拼接 NuGet 缓存路径

### 步骤 8：实现 `.nuspec` 生成与打包

动态生成 `.nuspec` 文件：
- 包 ID：`DotNetCampus.WpfLib`
- 版本：从配置或命令行参数读取
- 作者：`dotnet campus`
- 描述：WPF 自定义构建的托管程序集与 native 运行时
- 文件列表：根据 staging 目录动态生成

使用 `dotnet pack` 或 `nuget.exe pack` 生成最终 `.nupkg`。

### 步骤 9：添加控制台输出与调试支持

每个步骤输出清晰的状态信息：
- 构建进度（msbuild 输出摘要）
- 收集的文件列表（托管 DLL + native DLL）
- 打包结果（`.nupkg` 路径和大小）
- 耗时统计

确保 `dotnet run --project eng\Builder\Builder.csproj` 可直接用于调试。

### 步骤 10：验证 CI 流水线兼容性

- 确保 Builder 可在无交互环境下运行
- 所有路径使用相对路径或 MSBuild 属性解析
- 不依赖 Visual Studio 安装
- 退出码正确反映构建结果（成功 0，失败非 0）

---

## 托管 DLL 完整清单（待步骤 2 验证）

### 核心托管程序集

| 程序集 | 项目路径 |
|--------|----------|
| `WindowsBase.dll` | `src\Microsoft.DotNet.Wpf\src\WindowsBase\` |
| `System.Xaml.dll` | `src\Microsoft.DotNet.Wpf\src\System.Xaml\` |
| `PresentationCore.dll` | `src\Microsoft.DotNet.Wpf\src\PresentationCore\` |
| `PresentationFramework.dll` | `src\Microsoft.DotNet.Wpf\src\PresentationFramework\` |
| `PresentationUI.dll` | `src\Microsoft.DotNet.Wpf\src\PresentationUI\` |
| `ReachFramework.dll` | `src\Microsoft.DotNet.Wpf\src\ReachFramework\` |
| `System.Windows.Presentation.dll` | `src\Microsoft.DotNet.Wpf\src\System.Windows.Presentation\` |
| `System.Windows.Controls.Ribbon.dll` | `src\Microsoft.DotNet.Wpf\src\System.Windows.Controls.Ribbon\` |
| `System.Windows.Input.Manipulations.dll` | `src\Microsoft.DotNet.Wpf\src\System.Windows.Input.Manipulations\` |
| `WindowsFormsIntegration.dll` | `src\Microsoft.DotNet.Wpf\src\WindowsFormsIntegration\` |

### UIAutomation 系列

| 程序集 | 项目路径 |
|--------|----------|
| `UIAutomationTypes.dll` | `src\Microsoft.DotNet.Wpf\src\UIAutomation\UIAutomationTypes\` |
| `UIAutomationProvider.dll` | `src\Microsoft.DotNet.Wpf\src\UIAutomation\UIAutomationProvider\` |
| `UIAutomationClient.dll` | `src\Microsoft.DotNet.Wpf\src\UIAutomation\UIAutomationClient\` |
| `UIAutomationClientSideProviders.dll` | `src\Microsoft.DotNet.Wpf\src\UIAutomation\UIAutomationClientSideProviders\` |

### 主题程序集

| 程序集 | 项目路径 |
|--------|----------|
| `PresentationFramework.Aero.dll` | `src\Microsoft.DotNet.Wpf\src\Themes\PresentationFramework.Aero\` |
| `PresentationFramework.Aero2.dll` | `src\Microsoft.DotNet.Wpf\src\Themes\PresentationFramework.Aero2\` |
| `PresentationFramework.AeroLite.dll` | `src\Microsoft.DotNet.Wpf\src\Themes\PresentationFramework.AeroLite\` |
| `PresentationFramework.Classic.dll` | `src\Microsoft.DotNet.Wpf\src\Themes\PresentationFramework.Classic\` |
| `PresentationFramework.Fluent.dll` | `src\Microsoft.DotNet.Wpf\src\Themes\PresentationFramework.Fluent\` |
| `PresentationFramework.Luna.dll` | `src\Microsoft.DotNet.Wpf\src\Themes\PresentationFramework.Luna\` |
| `PresentationFramework.Royale.dll` | `src\Microsoft.DotNet.Wpf\src\Themes\PresentationFramework.Royale\` |

### 混合程序集

| 程序集 | 项目路径 | 说明 |
|--------|----------|------|
| `DirectWriteForwarder.dll` | `src\Microsoft.DotNet.Wpf\src\DirectWriteForwarder\` | C++/CLI 混合 |

### 排除项

以下项目不打包到 NuGet 中：

| 项目 | 原因 |
|------|------|
| `PresentationBuildTasks` | MSBuild 任务程序集，非运行时 |
| `mcwpf` | 追踪工具，非运行时 |
| `OSVersionHelper` | Native helper，非运行时 |
| `System.Printing` | C++/CLI 实现，当前未独立构建成功 |
| 所有 `*-ref` 项目 | 参考程序集，不打包 |
| `cycle-breakers` | 桥接项目，不打包 |

---

## Native DLL 清单（已通过步骤 2 验证）

从 `Microsoft.WindowsDesktop.App.Runtime.win-x64@8.0.6` 与 `Microsoft.NETCore.App.Host.win-x64@8.0.6` 包中确认的 native DLL：

| DLL | win-x64 | win-x86 | win-arm64 |
|-----|---------|---------|-----------|
| `D3DCompiler_47_cor3.dll` | ✅ | ✅ | ❌ |
| `PenImc_cor3.dll` | ✅ | ✅ | ✅ |
| `PresentationNative_cor3.dll` | ✅ | ✅ | ✅ |
| `vcruntime140_cor3.dll` | ✅ | ✅ | ✅ |
| `wpfgfx_cor3.dll` | ✅ | ✅ | ✅ |
| `ijwhost.dll` | ✅ | ✅ | ✅ |

> 注意：win-arm64 缺少 `D3DCompiler_47_cor3.dll`。

## 步骤 1 & 2 探索结论

### `origin\NuGetPackage\` 结构

```
origin\NuGetPackage\
├── lib\net6.0\          ← 托管 DLL（含 facade）+ 本地化资源子目录
├── ref\net6.0\          ← 参考程序集（不处理）
├── runtimes\win-x86\native\  ← 仅 win-x86 native DLL
├── LICENSE.TXT
├── THIRD-PARTY-NOTICES.TXT
├── runtime.json
└── version.txt
```

关键发现：
- **无 `.nuspec`、`.targets`、`.props` 文件** — 原始包是直接按目录结构组织的
- 包含 facade 程序集：`PresentationFramework-SystemCore.dll`、`-SystemData.dll`、`-SystemDrawing.dll`、`-SystemXml.dll`、`-SystemXmlLinq.dll`
- 包含 `System.Printing.dll`（当前仓库未构建成功，暂不打包）
- 包含本地化资源子目录（`cs/`、`de/`、`ja/`、`zh-Hans/` 等）
- `runtime.json` 引用包 ID `Microsoft.DotNet.Wpf.GitHub`，版本 `6.0.26-ci`

### NuGet 缓存中的托管 DLL 清单（`lib\net8.0\`）

官方 `Microsoft.WindowsDesktop.App.Runtime.win-x64@8.0.6` 的 `lib\net8.0\` 包含大量非 WPF 专属的 WindowsDesktop 程序集（如 `System.Windows.Forms.dll`、`System.Drawing.dll` 等），这些不应打包到 `DotNetCampus.WpfLib` 中。我们只打包当前仓库构建的 WPF 程序集。

---

## 风险与待确认

1. **`origin\NuGetPackage\` 无法通过工具访问**：已记录，步骤 1 用命令行探索
2. **`System.Printing` 未独立构建成功**：当前不打包，后续构建恢复后加入
3. **`DirectWriteForwarder` 是 C++/CLI 项目**：需确认其输出 DLL 是否应放入 `lib/net8.0/` 还是 `runtimes/`
4. **多 TFM 兼容性**：当前仅 `net8.0`，后续评估是否需要 `net6.0`、`net5.0` 等
5. **强签名**：当前仓库使用 WCP 公钥，需确认 NuGet 包中的 DLL 签名是否与官方一致
6. **`PresentationBuildTasks.dll` 锁文件问题**：IDE 构建时可能锁定，命令行构建不受影响
7. **打包步骤阻塞**（详见下文）

---

## 当前实现进度

| 步骤 | 状态 | 说明 |
|------|------|------|
| 步骤 1：探索 origin\NuGetPackage\ | ✅ 完成 | 通过命令行完成探索，结构已记录 |
| 步骤 2：探索 NuGet 缓存 DLL 清单 | ✅ 完成 | win-x64/win-x86 native DLL 清单已确定 |
| 步骤 3：改造 Builder.csproj | ✅ 完成 | net8.0、LangVersion 12、独立 OutputPath |
| 步骤 4：清理逻辑 | ✅ 完成 | 逐目录清理，跳过锁定文件 |
| 步骤 5：构建驱动逻辑 | ✅ 完成 | 按依赖顺序分别构建 x64 与 x86；VC++ x86 使用 Win32 平台 |
| 步骤 6：托管 DLL 收集 | ✅ 完成 | 参考程序集进入 ref/net8.0，x64/x86 实现程序集进入对应 RID 目录 |
| 步骤 7：Native DLL 收集 | ✅ 完成 | 从 NuGet 缓存拷贝 win-x64 + win-x86 native DLL |
| 步骤 8：.nuspec 生成与打包 | ✅ 完成 | 临时 pack 项目位于系统临时目录，并在打包前校验两个 RID 的关键资产 |
| 步骤 9：控制台输出与调试 | ✅ 完成 | 每个步骤有清晰的状态输出 |
| 步骤 10：CI 流水线兼容性 | ✅ 完成 | GitHub Actions 在 Windows runner 上执行完整构建、打包和比较 |

## 架构打包决策

采用单个 `DotNetCampus.WpfLib` 包同时承载 win-x64 与 win-x86，而不是拆成两个包：

1. NuGet 原生支持 `runtimes/<rid>/lib/<tfm>` 与 `runtimes/<rid>/native`，会依据项目 RID 选择正确资产。
2. 使用方只需引用一个稳定的包 ID，不需要根据目标架构切换依赖。
3. x64 与 x86 的 API 面由同一组 `ref/net8.0` 参考程序集定义，可避免两个包出现版本或 API 漂移。
4. 只有在单个包体积显著影响分发，或两个架构需要独立版本生命周期时，才值得拆分架构包；当前不具备这些条件。

实现程序集不能放入公共 `lib/net8.0`。经 `CorFlags` 验证，x64 构建的 WPF 托管程序集为 PE32+，x86 构建为 PE32 且设置 32BITREQ，因此全部实现程序集必须按 RID 分开放置。

## NuGet 产物端到端验证

Builder 提供以下命令验证最终生成的包：

```powershell
dotnet run --project eng\Builder\Builder.csproj --no-build -- test-package
```

默认选择 `eng\Builder\bin\nupkg\` 中最新的 `DotNetCampus.WpfLib.*.nupkg`。也可显式指定包：

```powershell
dotnet run --project eng\Builder\Builder.csproj --no-build -- test-package --package <nupkg-path>
```

验证逻辑在仓库配置作用域之外生成隔离的临时消费项目，覆盖：

- 单目标框架项目：`net8.0-windows`、`net9.0-windows`
- 多目标框架项目：`net8.0-windows;net9.0-windows`
- 每个目标框架分别发布 `win-x86` 与 `win-x64`
- 将发布目录中的包内托管 DLL 和 native DLL 与对应 RID 的 nupkg 资产逐文件进行 SHA-256 比对
- 实际运行每个发布后的 WPF 探针程序，启动 `Application` 和 Dispatcher 后正常退出
- 单个探针执行超过 30 秒、退出码非零、DLL 缺失或哈希不匹配均视为失败

测试输出保存在 `eng\Builder\bin\package-tests\`。GitHub Actions 在打包后自动执行该命令，并在失败时上传此目录作为诊断产物。

### 2026-07-14 本地验证记录

- 修复 WPF 实现程序集仍使用 `6.0.2.0`、参考程序集使用 `8.0.0.0` 的身份不一致问题。
- 修复 DLL 收集器遍历项目输出目录时，被复制依赖覆盖正确主输出的问题；现在每个项目只收集自身程序集。
- 为 C++/CLI `DirectWriteForwarder.dll` 增加对应 RID 的 `ijwhost.dll`，解决运行时模块加载失败。
- 隔离消费项目使用独立 `global.json`，避免继承仓库固定的 .NET 8 SDK。
- `net8.0-windows` 的 win-x86 与 win-x64 已完成发布资产 SHA-256 比对和实际启动测试。
- `net9.0-windows` 已暴露并修复 SDK inbox WPF 引用与包内 WPF 引用的版本冲突；最终矩阵需由 CI 或未被取消的本地长任务再次确认。
