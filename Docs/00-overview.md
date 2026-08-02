# 当前状态概览

> 审计基准：以当前工作区验证为准。该文件是当前状态的唯一事实源；后续动作见 [01-phase-plan.md](01-phase-plan.md)。

## 仓库与构建入口

| 项目 | 当前事实 |
|---|---|
| 根解决方案入口 | [`../Microsoft.Dotnet.Wpf.slnx`](../Microsoft.Dotnet.Wpf.slnx) |
| 根传统解决方案 | 根目录不存在同名传统解决方案文件；仓库其他模块内部可以存在独立 `.sln` |
| `origin/` | 当前非空；若后续发现为空，必须立即停止迁移 |
| `origin/src` 保护边界 | 被 `origin/.gitignore` 排除，不受外层 Git 状态保护；禁止使用 `git clean -xdf` |
| .NET SDK | `global.json` 指定 `8.0.101`，`rollForward` 为 `latestFeature` |
| MSBuild | 当前工作区验证使用 `18.7.8.30822` |

## 项目清单

排除 `origin/`、`artifacts/`、`bin/`、`obj/` 后，磁盘共有 **70** 个项目文件。

根 `slnx` 声明 **59** 个唯一项目，且 59 条声明路径当前均存在：

| 所在区域 | 数量 |
|---|---:|
| `src/Microsoft.DotNet.Wpf/src/` | 46 |
| `src/Microsoft.DotNet.Wpf/cycle-breakers/` | 8 |
| `Demo/` | 1 |
| `Docs/` | 1 |
| `eng/` | 3 |
| 合计 | 59 |

IDE 接口对新增 `eng/Builder.Tests` 和 `eng/Builder.ProcessTestHelper` 尚未重新枚举；只能确认根 `slnx` 的 59 条声明路径均存在，实际加载成功状态 **待 Visual Studio 验证**，不得表述为 59 个项目均已加载。

### 未直接纳入根 `slnx` 的 11 个项目

| 分类 | 数量 | 当前定位 |
|---|---:|---|
| `System.Printing.vcxproj` | 1 | 真实实现缺口；当前尚未构建，也未纳入根 `slnx` |
| `Extensions` 下的项目 | 5 | 是否保留取决于目标版本和兼容范围，待确认 |
| `OSVersionHelper.vcxproj` | 1 | 可能已由 binary 方案替代，待确认 |
| `eng/Builder/PackageTestApp/PackageTestApp.csproj` | 1 | Builder 使用的模板项目，不等同于待迁入主链实现 |
| `ThemeGenerator.proj` | 2 | 生成工具项目 |
| `wpf-etw.proj` | 1 | 生成工具项目 |
| 合计 | 11 | 不能把全部 11 个项目统一视为遗漏的主链实现 |

## 当前构建状态

以下结果来自当前工作区验证。最终双平台强制重建日志保存在 `artifacts/logs/Microsoft.Dotnet.Wpf-Debug-x64-final.{log,binlog}` 和 `artifacts/logs/Microsoft.Dotnet.Wpf-Debug-AnyCPU-final.{log,binlog}`；`artifacts/` 属于可再生成输出，不作为源码事实源。

| 范围 | 配置 | 结果 | 结论边界 |
|---|---|---|---|
| `ValidateSolutionConfiguration` | `Debug|x64` | 成功 | 只证明解决方案配置映射可解析 |
| `ValidateSolutionConfiguration` | `Debug|Any CPU` | 成功 | 只证明解决方案配置映射可解析 |
| 根 `slnx` Restore + Rebuild | `Debug|x64` | 成功：3445 个警告，0 个错误 | 完整强制重建，退出码为 0 |
| 根 `slnx` Restore + Rebuild | `Debug|Any CPU` | 成功：3445 个警告，0 个错误 | 完整强制重建，退出码为 0；解决方案项目映射和 native 资产使用 x64 |
| `WindowsBase` | `Debug|x64` | Restore/Build 成功 | `Accessibility.dll` 从已安装的 `Microsoft.WindowsDesktop.App.Ref/8.0.1/ref/net8.0` 解析；此前的 `CS0234` 当前不可复现 |
| `PresentationFramework` | `Debug|x64` | Restore/Build 成功 | 独立项目验证 |
| `PresentationUI` | `Debug|x64` | Restore/Build 成功 | 独立项目验证 |
| `WindowsFormsIntegration` | `Debug|x64` | Restore/Build 成功 | 独立项目验证 |
| `DirectWriteForwarder` | `Debug|x64` | 根解决方案强制重建覆盖 | 最终日志仍包含 `D9035`，但没有导致失败 |
| `System.Printing` | 未执行 | 未构建 | 旧错误只能作为历史线索，不能当作当前首个错误 |

最终强制重建命令为：

- `msbuild Microsoft.Dotnet.Wpf.slnx -restore /t:Rebuild /p:Configuration=Debug /p:Platform=x64 /m:1 /nr:false /v:minimal`
- `msbuild Microsoft.Dotnet.Wpf.slnx -restore /t:Rebuild /p:Configuration=Debug "/p:Platform=Any CPU" /m:1 /nr:false /v:minimal`

强制重建曾暴露 7 个主题引用程序集依赖预存 `PresentationFramework.dll` 的问题：打印相关 cycle-breaker 与完整引用程序集同名，`PresentationFramework.Royale-ref` 因选中不完整 bridge 而出现 53 个 `CS0234`。当前 7 个主题 ref 项目使用 ref-to-ref 项目依赖，并显式编译引用 `$(ArtifactsObjDir)PresentationFramework-ref\$(WpfNativePlatform)\$(Configuration)\$(TargetFramework)\ref\PresentationFramework.dll`。Trusted Builder 的干净构建随后暴露运行时主题项目存在同类问题：条件式实现程序集引用在清理 `artifacts` 后不会进入项目项列表，MarkupCompile 可能加载同名 cycle-breaker，并以 `LostFocusEventManager` 无法解析为 known type 380 失败；当前 7 个运行时主题项目已将 `PresentationFramework.csproj` 降为仅构建排序，并无条件显式引用完整实现输出，等待 Trusted Builder 复验。

当前可能出现但不导致最终双平台强制重建失败的警告包括：

- `NU1603`。
- `MSB3243`。
- 缺少 Perl 时跳过相关脚本的警告。
- `D9035`。

此前观察到的 WpfDemo 输出文件锁在最新复验中未再出现；它保留为历史环境线索，不再是当前首个阻塞。

## 已落地能力

### Native 与 Builder

- 已实现共享 native 资产清单。
- Builder 已实现 x64 和 x86 路径。
- Builder 已注册独立 `relay-pr` 命令，使用 Octokit 14.0.0 读取来源 PR，并在独立 clone 中执行固定 base/head SHA fetch、纯 Patch 应用、本地门禁、精确 SHA push 和目标 PR 创建/复用；命令缺少 `GITHUB_TOKEN` 时会在 clone 和远端写入前退出；`--allow-untrusted-build` 仅控制是否在 Temp workspace 执行本地构建验证，默认跳过并依赖 GitHub Actions。
- 本地门禁在隔离 HOME/NuGet/AppData/TEMP 环境中依次执行 Builder Restore/Build、x64/x86 构建打包、精确 nupkg `test-package` 和根 `Debug|x64` Rebuild，并校验构建前后 HEAD、tree、index 和 tracked working tree。
- `eng/Builder.Tests` 与 `eng/Builder.ProcessTestHelper` 已纳入根 `slnx`；单元、进程和本地 bare repository 集成测试覆盖 URL/remote 解析、敏感环境、取消/超时、PR ref fallback、纯 Patch 应用与冲突、精确 SHA push、lease 竞争、GitHub Actions 事件/身份、artifact 评论格式、workflow 安全契约和 checkout 换行保持契约。
- Builder 已注册 `ci-build` 与 `comment-pr-artifacts` 命令，将 GitHub Actions 中的 checkout 凭据检查、事件/merge 身份校验、版本与 artifact 计算、构建/打包门禁、workflow run/artifact 查询及幂等评论迁入 C#。
- `.github/workflows/build.yml` 使用 `pull_request_target`，分别 checkout `github.sha` 的受信任 Builder 与 PR merge ref，再由仅具只读权限的 Job 构建 tested checkout；成功生成的 `-test.*` NuGet 包由不 checkout、也不执行 PR 内容的独立 Job 下载并推送到 NuGet.org 与 GitHub Packages。Tag 包保留 Tag 的完整语义版本（可选移除 `v` 前缀）。`.github/workflows/comment-pr-build-artifacts.yml` 只 checkout `github.sha` 的受信任 Builder，不 checkout PR 或下载 artifact，并通过 Octokit 回写 bot 评论。
- arm64 尚未实现。

### WpfDemo

- 已实现 WpfDemo x64 消费与部署路径。
- WpfDemo x64 命令行构建和运行探针已有历史/现存验证报告。
- 文化资源程序集的当前复制模式尚未证明会保留文化子目录，存在同名卫星程序集被扁平复制或覆盖的风险。
- Visual Studio F5 实现尚未复验。
- WpfDemo x86、arm64 和 Publish 尚未实现。

### PresentationUI

- `PresentationUI` 已配置 `InternalMarkupCompilation`。
- 当前 `artifacts` 中存在 4 个 `.g.cs` 文件。
- 对应的 4 个 `.xaml.cs` 仍显式声明基类。
- 现有产物不能证明干净状态下会稳定生成；干净生成和显式基类回退均待验证。

### Cycle-breaker

- 根 `slnx` 已纳入 8 个 cycle-breaker 项目。
- 其中 7 个存在直接消费者。
- `PresentationFramework-System.Printing-impl-cycle` 当前没有直接消费者，去留待确认。

## 当前未决项

1. 在 Visual Studio 中确认 59 个项目的实际加载状态，并复验 WpfDemo F5。
2. 验证并修正 WpfDemo 文化资源程序集的子目录部署，避免不同文化的同名资源被覆盖。
3. 在干净输出下验证 `PresentationUI` 的 4 个 `.g.cs` 可重复生成，再决定是否回退 4 个 `.xaml.cs` 的显式基类。
4. 构建 `System.Printing.vcxproj`，依据当前首个真实错误决定修复与纳管方式。
5. 确定目标版本边界：
   - `System.Windows.Primitives` 只存在于 `origin`，当前仓库缺失；是否迁入取决于目标版本边界，不能直接认定为必迁模块。
   - `Extensions` 下 5 个项目的去留取决于兼容目标。
6. 判断无直接消费者的 `PresentationFramework-System.Printing-impl-cycle` 是否仍有保留价值，并继续隔离同名 cycle-breaker 对非打印 ref 消费者的影响。
7. 补齐产品测试迁移、WpfDemo x86、arm64、Publish 和旧生成工具的目标范围；Builder PR relay 自身测试已落地，但不代表 WPF 产品测试迁移完成。
8. 在专用低权限/可销毁环境中对 `relay-pr` 执行真实 GitHub 示例 PR 端到端验收，并按专题文档矩阵验证同仓库 PR、fork PR、失败、rerun、新提交和 artifact 评论场景。

## 验证边界

- 根 `slnx` 的 `Debug|x64` 和 `Debug|Any CPU` Restore + Rebuild 已在当前工作区成功；该结论不外推到其他配置、平台或 Visual Studio 设计时/F5 行为。
- Builder PR relay 的本地自动化验证不包含真实 GitHub push、PR 创建或不可信外部 PR 构建；Actions 权限、fork 与评论行为仍需真实 PR 验收。
- 主题 ref 与运行时主题的成功依赖当前显式完整 `PresentationFramework` 引用边界；在同名打印 cycle-breaker 收敛前，不应移除该隔离，也不应使用会在干净项目求值时消失的条件式输出引用。
- 独立项目成功不能替代根 `slnx` 成功；增量成功不能替代强制重建成功。
- IDE 枚举项目路径不能证明项目已加载；必须由 Visual Studio 的加载状态和实际构建验证。
- `artifacts` 中已有生成文件不能证明从干净状态可重现。
- 没有重新执行的历史命令、旧错误和专题文档不得提升为当前结论。