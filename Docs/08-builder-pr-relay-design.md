# Builder GitHub PR 搬运与 NuGet 产物回写设计

## 文档定位

本文定义并说明 `eng/Builder` GitHub PR 搬运命令的行为契约，以及与本地搬运命令解耦的 GitHub Actions 产物回写流程。对应 C#、项目文件和两个 workflow 已落地；真实 GitHub push、PR 创建、fork 权限和评论矩阵仍需在专用低权限环境中验收。

现有 Builder 的构建、打包与包验证能力见 [05-builder-plan.md](05-builder-plan.md)，仓库整体状态仍以 [00-overview.md](00-overview.md) 为准。

设计参考了 Octokit.NET 的 PR、Actions、artifact 和 Issue Comment API。职责边界保持为：

- Octokit.NET 负责读取 PR 元数据、查询或创建目标 PR，以及本地命令需要的其他 GitHub REST API 调用。
- `git` 负责 clone、fetch、merge 和 push。
- Visual Studio MSBuild 与现有 Builder 负责本地构建、NuGet 组包和隔离消费验证。
- GitHub Actions 负责对目标仓库中的任意贡献者 PR 执行受限权限构建，并在受信任的独立工作流中回写结果。

## 目标

### 本地命令

输入一个 GitHub PR 链接，例如：

```text
https://github.com/dotnet/wpf/pull/11781
```

Builder 应完成以下动作：

1. 解析 PR 所属仓库和编号，并通过 GitHub API 读取 PR 的真实来源仓库、来源分支和固定 head SHA。
2. 在独立临时 clone 中，以自己的目标仓库 base 分支为起点创建 `t/bot/PR_<number>` 分支；示例分支为 `t/bot/PR_11781`。
3. 拉取并校验原 PR 的固定 head SHA，将该提交合并到目标分支。
4. 在合并后的提交上执行完整本地构建、打包和包验证门禁。
5. 只有全部本地门禁成功后，才把已验证的精确提交推送到自己的目标仓库。
6. 在自己的目标仓库内创建或复用 PR，并输出 PR 链接。

### GitHub Actions

目标仓库中的任意贡献者，包括来自 fork 的贡献者，向受支持的 base 分支提交 PR 时：

1. 使用只读权限构建 PR 测试合并提交。
2. 构建根解决方案。
3. 使用 Builder 生成并验证 WPF NuGet 包。
4. 上传 NuGet 包 artifact。
5. 在另一个受信任的工作流中，把 Actions run 和 artifact 下载页面链接幂等地写入该 PR。

## 非目标

首版不承担以下能力：

- 不自动解决 merge 冲突。
- 不使用 `--allow-unrelated-histories` 强行合并无共同历史的仓库。
- 不把当前调用者工作区中的未提交修改带入自动分支。
- 不在本地命令中等待 GitHub Actions 完成；PR 创建与 CI 结果回写是两个独立阶段。
- 不把 Actions artifact 当成永久、匿名下载服务。
- 不自动发布 Release、NuGet.org 包或外部对象存储。
- 不支持 GitHub Enterprise Server；首版只接受 `https://github.com/.../pull/<number>`。
- 不自动合并目标 PR。

## 已确认的当前基础

- `eng/Builder/Builder.csproj` 是 `net8.0` 控制台项目，当前注册默认构建、`clean`、`compare`、`test-package` 和 `relay-pr` 命令。
- 默认 Builder 构建会构建 x64/x86、收集程序集与 native 资产、生成 `DotNetCampus.WpfLib` NuGet 包，并在项目构建不完整时返回非零退出码。
- `test-package` 会对 net8.0/net9.0 与 win-x86/win-x64 组合执行隔离 Publish、哈希校验和运行探针。
- `.github/workflows/build.yml` 使用只读 `pull_request_target` 受信任定义，将 `github.sha` 的 Builder 与 PR merge ref 分别 checkout 到 `trusted/` 和 `tested/`，再由受信任 C# 命令校验并构建 tested checkout；`.github/workflows/comment-pr-build-artifacts.yml` 在 `workflow_run` 受信任上下文中只 checkout `github.sha` 的 Builder、读取元数据并回写 PR 评论。
- 当前仓库有多个远端，因此不能用“列表中的第一个 GitHub 远端”猜测目标仓库。命令使用显式目标 remote，默认值为 `origin`。
- 示例 PR 页面显示其 base 为 `dotnet/wpf:main`，head 为 `TFGSUMIT/wpf:fix/issue-11774`，页面展示的缩写提交为 `4ce6f21`。实现不得硬编码这些值，必须在每次执行时通过 API 获取完整 head SHA。

## 命令行契约

独立命令 `relay-pr` 避免默认构建命令意外执行远端写操作：

```powershell
dotnet restore eng/Builder/Builder.csproj
dotnet build eng/Builder/Builder.csproj --no-restore
dotnet run --project eng/Builder/Builder.csproj --no-build -- relay-pr `
  --pull-request https://github.com/dotnet/wpf/pull/11781 `
  --target-remote origin `
  --base WpfReorganize `
  --github-token "<fine-grained PAT or GitHub App token>" `
  --allow-untrusted-build
```

建议参数如下：

| 参数 | 必需 | 默认值 | 说明 |
|---|---:|---|---|
| `--pull-request` | 是 | 无 | 标准 GitHub.com PR 页面链接；允许 `/files`、`/commits` 等子页面，解析后统一为 canonical PR URL |
| `--target-remote` | 否 | `origin` | 调用者仓库中代表“自己的 GitHub 仓库”的 remote；fetch URL 用于取得 base，push URL 用于发布分支 |
| `--base` | 否 | `<target-remote>/main`（默认 `origin/main`） | 新 PR 的 base 分支；同时接受 `main` 与 `origin/main` 形式 |
| `--github-token` | 条件必需 | `GITHUB_TOKEN` | GitHub API Token；命令行值优先，未提供或为空时回退到环境变量 |
| `--allow-untrusted-build` | 是 | `false` | 明确确认将执行外部 PR 中的 MSBuild、C# 和构建脚本；缺少该开关时只输出风险并退出 |
| `--keep-workspace` | 否 | `on-failure` | `always`、`on-failure` 或 `never`；控制独立临时 clone 的保留策略 |

自动分支名不提供覆盖参数，固定为：

```text
t/bot/PR_<number>
```

固定命名满足使用约定，并让重试可以查找同一目标分支。由于名称只包含 PR 编号，来自不同源仓库的同号 PR 会冲突；实现必须校验来源标记，不能覆盖不属于当前来源 PR 的同名分支。

## 凭据与前置条件

### GitHub API 凭据

本地命令优先从 `--github-token` 读取 Octokit 凭据，未提供或为空时回退到 `GITHUB_TOKEN`。实现不把 Token 写入配置、Git URL、日志、状态文件或 PR 正文。命令行参数可能被终端历史或系统进程列表记录；共享环境中应优先使用环境变量或其他受控的凭据注入方式。

Token 至少需要：

- 读取来源 PR；私有来源仓库还需要对应读取权限。
- 读取目标仓库和已有 PR。
- 在目标仓库创建 PR。

### Git 凭据

Octokit 凭据不会自动传给 `git`。push 需要由以下任一种机制单独提供：

- Git Credential Manager。
- SSH key 或受控 SSH agent。
- GitHub App installation token 对应的受控 credential helper。

clone/fetch 能匿名完成时应使用只读 HTTPS URL。不得把 PAT 拼入 URL。

### 构建环境

本地门禁需要：

- `global.json` 可选择的 .NET 8 SDK。
- 包验证使用的 .NET 9 SDK。
- Visual Studio MSBuild。
- WPF/C++/CLI 所需的 Visual Studio C++ 工具链。
- 可访问配置的 NuGet 源。

## PR 解析与来源固定

### URL 解析

只接受 HTTPS GitHub.com URL，路径至少符合：

```text
/{owner}/{repository}/pull/{positive-number}
```

解析时应：

- 拒绝非 HTTPS、非 `github.com`、缺失 owner/repository、非 `pull` 路径和无效编号。
- 对 owner/repository 做 URL 解码并拒绝无效转义。
- 忽略编号后的 `/files`、`/commits` 等页面路径。
- 生成 canonical URL：`https://github.com/{owner}/{repository}/pull/{number}`。

### Octokit 元数据

调用 `client.PullRequest.Get(owner, repository, number)` 后至少保存：

- PR number、title、state、draft 和 canonical HTML URL。
- `Head.Repository.FullName`、`Head.Repository.CloneUrl`、`Head.Ref`、`Head.Sha`。
- `Base.Repository.FullName`、`Base.Ref` 和 `Base.Sha`。

URL 中的仓库是原 PR 的 base repository，不一定是提交代码的来源仓库。fetch 来源必须以 `Head.Repository`、`Head.Ref` 和 `Head.Sha` 为准。

### 固定到 head SHA

来源分支在命令执行期间可能继续更新。自动化必须以 API 返回的完整 `Head.Sha` 作为构建输入身份，不能只按可移动分支名合并。

建议获取顺序：

1. 从 `Head.Repository.CloneUrl` fetch `Head.Ref` 到私有临时 ref。
2. 校验 fetch 后提交等于 API 返回的 `Head.Sha`。
3. 如果来源仓库或分支已删除、不可读或 SHA 不一致，则从原 PR base repository fetch `refs/pull/<number>/head`。
4. 再次校验该 ref 等于 API 返回的 `Head.Sha`。
5. 两条路径均无法取得精确 SHA 时停止，不退化为“合并当前分支最新提交”。

如果第一次 fetch 后发现来源分支已移动，可以重新查询一次 PR 元数据并重新开始解析阶段；不能在已完成本地验证后静默替换 source SHA。

## 目标仓库与 base 解析

1. 在调用者仓库中读取 `git remote get-url <target-remote>` 和 `git remote get-url --push <target-remote>`。
2. 同时支持 GitHub HTTPS 与 SSH remote 形式，但两者必须可唯一解析为同一个 `owner/repository`。
3. 目标 remote 不是 GitHub.com、fetch/push 指向不同仓库或 URL 无法解析时停止。
4. `--base` 未提供时使用 `<target-remote>/main`；显式参数同时接受远端限定形式（如 `origin/main`）和分支名形式（如 `main`）。
5. 临时 clone 必须从目标 remote 的远端 base 分支建立，确保最终 PR 的 base 与 GitHub 远端事实一致。
6. 目标远端不存在该 base 分支时停止。

## 独立工作区策略

### 为什么不用当前工作区

本地仓库可能存在未提交修改、Visual Studio 锁定文件和正在进行的迁移工作。自动命令不能执行 checkout、reset、stash 或 merge 来改变调用者工作区。

### 为什么首版不用 linked worktree

linked worktree 与主工作区共享 refs 和对象数据库，而且现有 `RepositoryLocator` 只识别 `.git` 目录，不识别 worktree 中的 `.git` 文件。外部 PR 构建也需要更明确的 Git 元数据隔离。因此首版使用独立 clone。

### 临时目录布局

建议使用系统临时目录下的专用根：

```text
%TEMP%/WpfReorganize.Builder/pr-relay/
└─ dotnet-wpf-11781-<timestamp>-<random>/
   ├─ repository/
   ├─ logs/
   └─ state.json
```

`state.json` 只保存非敏感诊断信息：

- canonical source PR URL 与 source SHA。
- target repository、base 和自动分支名。
- 合并后待验证提交 SHA。
- 每个本地门禁的命令名称、退出码、开始/结束时间和日志路径。
- push 前观察到的远端分支 SHA。
- 创建或复用的目标 PR URL。

清理必须验证目标路径位于已知临时根下，并带有命令创建的状态文件；不能对任意用户路径执行递归删除。

## Git 合并流程

所有 Git 参数必须通过 `ProcessStartInfo.ArgumentList` 分项传递。来源 URL、ref、标题或分支名不得拼成 shell 命令字符串。

建议流程：

1. 独立 clone 自己的目标仓库，不 checkout 默认分支。
2. fetch 目标 `refs/heads/<base>` 到受控本地 ref。
3. 从远端 base 创建 `t/bot/PR_<number>`。
4. 按“PR 解析与来源固定”取得精确 source SHA。
5. 取得 API 返回的原 PR base SHA，并确认它是 target base 的祖先；这证明目标分支已经包含原 PR 的基线，直接 merge source head 不会顺带引入目标分支尚未拥有的大批上游提交。
6. 查询或计算原 PR commit 列表，确认 `target base..source head` 的待引入提交不超出原 PR 范围；出现额外提交时停止并输出差异。
7. 确认 source SHA 与 target base 具有共同历史；没有共同历史时停止。
8. 使用 `--no-ff` 创建明确的 merge commit，不使用 rebase 或 squash。
9. merge commit message 写入稳定来源信息，例如 canonical PR URL 和完整 source SHA。
10. 出现冲突时停止，输出 `git diff --name-only --diff-filter=U` 的冲突文件，并按保留策略留下临时目录。
11. 合并后如果 target base 与新分支没有有效差异，则报告“无需创建 PR”，不调用 GitHub 创建 API。

如果原 PR base SHA 不是 target base 的祖先，直接 merge head 可能把原 PR 之外的上游提交一起带入自己的仓库。首版必须停止并报告该边界，不自动退化为 cherry-pick、patch 或文件复制；这些策略需要单独设计并由调用者明确选择。

禁止以下行为：

- 自动选择 `ours` 或 `theirs` 隐藏冲突。
- 使用 `--allow-unrelated-histories`。
- 在失败后提交半完成 merge。
- 根据分支名猜测 source SHA。

## 本地构建门禁

### 成功定义

“本地构建成功”定义为合并后的独立 clone 中以下门禁全部返回退出码 `0`，并且预期 nupkg 存在且非空：

1. 还原 Builder。
2. 构建 Builder。
3. 执行 Builder 默认 x64/x86 构建与打包。
4. 对本次生成的精确 nupkg 执行 `test-package`。
5. 对根 `Microsoft.Dotnet.Wpf.slnx` 执行 `Debug|x64` Restore + Rebuild。

建议命令顺序为：

```powershell
dotnet restore eng/Builder/Builder.csproj
dotnet build eng/Builder/Builder.csproj --no-restore
dotnet run --project eng/Builder/Builder.csproj --no-build -- --version <relay-version>
dotnet run --project eng/Builder/Builder.csproj --no-build -- test-package --package <exact-nupkg-path>
msbuild Microsoft.Dotnet.Wpf.slnx -restore /t:Rebuild /p:Configuration=Debug /p:Platform=x64 /m:1 /nr:false /v:minimal
```

`<relay-version>` 使用可追踪且合法的 SemVer 预发布版本，例如：

```text
0.0.0-pr.11781.sha<source-sha-prefix>
```

包验证必须先清空该临时 clone 的 `eng/Builder/bin/nupkg/`，再显式传入根据版本计算出的 nupkg 路径，不能依赖 `test-package` 的“最后写入包”回退。SHA 前增加 `sha` 前缀，避免纯数字缩写带前导零时违反 SemVer 数字标识符规则。

Builder 默认构建当前可能在部分项目失败后继续生成诊断或部分包，但最终会返回非零退出码。relay 门禁必须把任何非零退出码视为失败，不能因为目录中存在 nupkg 就继续 push。

### 构建前后提交校验

外部 PR 的构建脚本可能修改工作树、索引、分支或 Git 配置。构建前记录：

- 合并提交 SHA。
- 合并提交 tree SHA。
- 目标 push URL。
- 远端自动分支的旧 SHA；不存在时记录为空。

构建后必须确认：

- `HEAD` 仍等于构建前合并提交 SHA。
- tracked working tree 和 index 均无修改。
- tree SHA 未变化。
- 不使用构建后可能被修改的 remote 配置决定发布地址。

push 时显式推送已验证 SHA，而不是可移动的当前分支名：

```text
<validated-commit-sha>:refs/heads/t/bot/PR_<number>
```

## 外部代码执行安全边界

构建外部 PR 等价于在本机执行不可信代码。独立 clone 只保护当前工作树和 Git refs，不是安全沙箱，不能阻止恶意 MSBuild task、编译器插件、脚本或测试访问当前用户可访问的文件、网络和凭据。

实施时必须做到：

- 要求显式 `--allow-untrusted-build`。
- 启动构建子进程时移除 `GITHUB_TOKEN`、`GH_TOKEN`、Actions/OIDC 变量和其他已知写凭据。
- 为构建子进程使用隔离的 HOME/USERPROFILE 与 Git 配置，禁用交互式 Git 凭据提示，并尽可能移除 SSH agent 环境。
- 不把 Token、credential helper 输出或完整敏感环境写入日志。
- 为外部进程设置可取消的超时，并在取消时终止进程树。
- 本地命令使用绝对、预先解析的 `git`、`dotnet` 和 MSBuild 路径，避免构建过程修改 PATH 后影响发布阶段。
- 构建成功后仍按精确 SHA 发布，并重新检查工作树和索引。

即使完成以上限制，同一 Windows 用户下的构建仍不能视为强隔离。推荐在无个人凭据、无生产密钥、可销毁的专用 VM 或专用低权限账户中运行。需要强安全边界时，应把“不带写凭据的构建”和“受信任发布”拆到不同机器或不同安全主体。

## push、分支冲突与幂等规则

### 来源标记

merge commit message 和目标 PR body 都应包含机器可读标记，例如：

```text
Source-PR: https://github.com/dotnet/wpf/pull/11781
Source-Head-SHA: <full-sha>
```

PR body 另加隐藏标记：

```html
<!-- builder-pr-relay source=dotnet/wpf#11781 -->
```

### 同名远端分支不存在

- 使用普通 push 创建分支。
- push 成功后创建目标 PR。

### 同名远端分支已存在

继续前必须同时验证：

- 已有 open PR 的 head/base 与目标分支一致，且 PR body 中来源标记匹配；或
- 远端分支 head 的 merge commit 来源 trailer 与当前 canonical source PR 匹配。

来源不匹配时立即停止，不能覆盖。

来源匹配时：

- 重新从最新 target base 和当前 source SHA 构建候选分支。
- 本地门禁通过后使用 `--force-with-lease`，lease 值为构建前读取的远端旧 SHA。
- lease 失败表示其他进程或用户更新了分支，应停止并重新执行完整流程，不能改为无条件 force push。

### PR 创建与复用

在创建前按 `targetOwner:t/bot/PR_<number>` 与 base 查询 open PR：

- 找到来源标记匹配的 PR时复用。
- 找到同 head/base 但来源标记不匹配的 PR 时停止。
- 未找到时调用 Octokit 创建同仓库 PR。
- 创建返回 422 时再次查询，处理“并发任务已创建相同 PR”的竞态。

建议标题：

```text
[PR relay] dotnet/wpf#11781: <source title>
```

建议正文包含：

- 原 PR 链接。
- 原 PR base、head 仓库、head 分支和固定 source SHA。
- 目标 base 和自动分支。
- 本地验证门禁及成功时间。
- “Actions 构建结果将由独立工作流回写”的说明。
- 机器可读来源标记。

## 本地命令状态机

```text
InputValidated
  -> TargetResolved
  -> SourceResolved
  -> RepositoryCloned
  -> SourceFetched
  -> Merged
  -> LocalValidationSucceeded
  -> BranchPushed
  -> PullRequestCreatedOrReused
```

失败规则：

- 在 `LocalValidationSucceeded` 前失败：不得创建或更新远端分支。
- push 失败：不得创建新 PR；若已有 PR，只输出其现状，不声称已更新。
- PR 创建失败但 push 已成功：保留状态和分支 URL，重试时先复用同一分支，不能重复生成其他命名分支。
- 取消：终止当前子进程树，保留 `state.json` 和日志，返回非零退出码。

## Builder 实现拆分

实现保持内部可见性并避免不必要的接口层：

| 文件 | 职责 |
|---|---|
| `RelayPullRequestCommand.cs` | 声明 `relay-pr` 命令参数、建立取消令牌并调用编排服务 |
| `PullRequestRelayService.cs` | 执行状态机，保证“验证成功后才发布”的顺序 |
| `GitHubPullRequestService.cs` | 创建 Octokit client、解析 PR 元数据、查询/创建/复用目标 PR |
| `GitService.cs` | 使用分项参数执行 clone/fetch/merge/status/push，并实现 SHA 与来源标记校验 |
| `LocalBuildValidationService.cs` | 运行固定本地门禁、计算精确包路径并记录日志 |
| `GitHubActionsBuildCommand.cs` / `GitHubActionsBuildService.cs` | 作为 Actions 的受信任构建入口，复核事件/checkout 身份、凭据与 Git 状态，并编排 solution/package 门禁 |
| `GitHubArtifactCommentCommand.cs` / `GitHubArtifactCommentService.cs` | 作为 `workflow_run` 的受信任回写入口，复核 run、PR、artifact 和评论元数据并幂等回写 |
| `GitHubWorkflowRunEvent.cs` / `GitHubArtifactCommentFormatter.cs` | 严格解析事件文件，筛选 artifact 身份并生成安全评论正文与 marker |
| `PullRequestAddress.cs` | PR URL 与 GitHub remote 地址的不可变解析结果；优先使用 record |
| `ProcessRunner.cs` | 增加异步、`ArgumentList`、取消、超时、进程树终止与受控环境支持；现有同步调用可逐步复用安全内核 |
| `Resources.resx` | 保存新增 CLI 错误、帮助和 PR 模板中的用户可见文本 |

`Builder.csproj` 使用与 net8.0 兼容的稳定版 Octokit.NET 14.0.0；没有为该功能修改 TFM、SDK 或 C# 版本。

网络与进程 I/O 应 async end-to-end。命令取消时不允许遗留继续运行的 build、test 或 Git 子进程。

## GitHub Actions 设计

### 两个工作流的职责

GitHub Actions 部分拆为两个安全主体：

1. **受信任编排的构建工作流**：处理 `pull_request_target`，分别 checkout base `github.sha` 的受信任 Builder 和 PR 测试合并 ref；由受信任 Builder 复核 tested checkout 后再执行不可信代码。工作流只授予只读权限，不持有评论权限或仓库 secrets。
2. **回写工作流**：处理构建工作流的 `workflow_run: completed`，在默认分支的受信任上下文中 checkout `github.sha` 的 Builder，再只读取 run/artifact 元数据并评论 PR；不 checkout PR，不下载 artifact，不执行 artifact 内容。

这种拆分让同仓库和 fork PR 都使用默认分支定义的只读构建编排，同时让构建完成后的评论拥有最小写权限。

### 构建工作流

不能直接依赖普通 `pull_request` 从 PR 测试合并提交加载的 workflow 定义建立权限边界。Builder 搬运命令会把外部代码推到目标仓库内的 `t/bot/*` 分支；GitHub 会把它视为同仓库 PR，而这些不可信提交可以同时修改 `.github/workflows/build.yml`。仅在可被 PR 修改的 YAML 中写 `permissions: contents: read`，不能证明攻击者无法替换该声明或增加其他 job。

首版因此使用 `pull_request_target` 取得 base 分支中的受信任 workflow 定义，但把它严格限制为“只读 Token、零 secrets、GitHub-hosted 临时 runner、显式 checkout 不可信测试合并 ref”的构建入口。若仓库或组织无法强制这些限制，应改用独立构建仓库、GitHub App 或其他隔离安全主体，不执行同仓库搬运 PR。

在现有 `.github/workflows/build.yml` 基础上调整，而不是增加一套重复构建：

- 把 PR 入口改为 `pull_request_target`，保留对 `main` 和 `WpfReorganize` 的 base 过滤；这里的“任何人”指任意贡献者或 fork，不改变仓库当前受支持的 base 范围。
- workflow 顶层显式设置 `permissions: contents: read`，其他权限为 `none`，并在仓库或组织设置中把 `GITHUB_TOKEN` 默认权限强制为只读。
- 每个 job 先把 `github.sha` checkout 到 `trusted/`，再把 `refs/pull/<number>/merge` 显式 checkout 到 `tested/`；merge ref 不存在时失败并报告不可合并状态，不退化为只构建 base。
- 两个 `actions/checkout` 都设置 `persist-credentials: false`；受信任 `ci-build` 命令检查 tested checkout 的 `.git/config` 与 remote URL 不含凭据。
- workflow 不引用任何 Actions secret、environment secret 或 OIDC，不把 `github.token` 传入构建环境。
- 不向 `t/bot/*` 等搬运分支的 PR 提供 Actions secrets；同仓库来源不能被视为可信代码。
- 不调用来自 PR checkout 目录的 local action 或安全编排器；第三方 action 固定到经过审计的完整 commit SHA。
- 只使用 GitHub-hosted 临时 runner，不在持久化 self-hosted runner 上执行不可信 PR。
- 不授予 OIDC、packages、contents、actions 或其他写权限。
- 不为不可信 PR 恢复可被其他安全上下文消费的可写缓存。
- 根解决方案 job 和 Builder/package-test job 均由 `trusted/eng/Builder` 中构建出的 `ci-build` 命令驱动，两个 job 均成功时 workflow 才是 success。
- NuGet artifact 只在 Builder 与 `test-package` 成功后上传。
- `if-no-files-found` 从 `warn` 改为 `error`，避免 workflow success 但没有包。
- artifact 名加入 PR 编号、tested SHA、run ID 和 run attempt，避免来源混淆和 rerun 名称冲突。
- artifact 保留期使用仓库策略或显式配置，并由回写工作流读取 API 返回的 `expires_at` 展示。
- 对同一 PR 使用 concurrency，新的提交到达时取消旧构建。

建议 artifact 名：

```text
DotNetCampus.WpfLib-nupkg-pr-<pr-number>-sha-<tested-sha>-run-<run-id>-attempt-<run-attempt>
```

在 `pull_request_target` 中，`github.sha` 是 base 分支提交，不是被构建提交。受信任 `ci-build` 命令从事件文件读取贡献者 head SHA，从 tested checkout 读取 merge SHA，并要求该 merge commit 恰好以 `github.sha` 和贡献者 head SHA 为两个有序双亲；包版本与 artifact 名使用 tested merge SHA，三者不能混用。

push 与 `workflow_dispatch` 仍可构建和上传 artifact，但不会触发 PR 评论。

### 回写工作流

已新增 `.github/workflows/comment-pr-build-artifacts.yml`：

```text
on:
  workflow_run:
	workflows: [Build WPF NuGet Package]
	types: [completed]
```

最小权限：

- `actions: read`：读取触发 run 和 artifacts。
- `contents: read`：checkout `github.sha` 对应的受信任 Builder 源码。
- `pull-requests: write`：创建或更新 PR 评论。
- 如果 GitHub API 对 Issue Comment 路径要求额外权限，只增加必要的 `issues: write`；不授予 `contents: write`。

回写 job 使用 Ubuntu 或其他轻量 hosted runner 即可，不需要 WPF 构建环境。Builder 使用 `-p:RestoreWpfRuntimePackages=false` 轻量还原/构建，避免下载仅默认 WPF 打包命令需要的 WindowsDesktop `PackageDownload`；只有最终 `comment-pr-artifacts` 命令步骤接收 `GITHUB_TOKEN`。

### run 与 PR 关联

回写工作流必须满足全部条件才评论：

1. `workflow_run.event == "pull_request_target"`。
2. run 属于当前目标仓库。
3. `workflow_run.pull_requests` 恰好包含一个目标 PR。
4. PR number、head SHA 和 run 创建时间能够从 API 重新确认。
5. 当前 run 是该 PR 对应构建工作流的最新 run；如果存在更新的 queued、in-progress 或 completed run，则跳过旧 run，避免旧取消结果覆盖新成功结果。

`workflow_run.pull_requests` 为空或关联不唯一时只写 job summary，不按 branch name 猜测 PR。

还必须重新读取目标 PR 并确认它仍为 open、base 与受支持列表一致，而且 run 的关联 SHA 与该 PR 当前 head 或测试合并身份一致；PR 已关闭、已更换 head 或 run 已陈旧时不评论。

### artifact 查询与链接

回写工作流通过 Actions API 分页读取当前 run 的 artifacts，并只选择：

- 名称符合 `DotNetCampus.WpfLib-nupkg-` 前缀。
- `Expired == false`。
- 大小大于零。

不读取 artifact 内部文件，也不信任 artifact 名称中的 Markdown。名称输出前必须转义并限制长度和数量。

评论中的下载页面 URL 使用：

```text
https://github.com/<owner>/<repository>/actions/runs/<run-id>/artifacts/<artifact-id>
```

该链接与 `actions/upload-artifact` 的 `artifact-url` 指向同类 GitHub artifact 页面。它不是永久匿名 URL：

- 用户通常需要登录并拥有仓库读取权限。
- artifact 会按保留策略过期。
- 不把 `ArchiveDownloadUrl` 的临时对象存储重定向 URL写入评论。

如果以后需要永久或匿名下载，应增加独立的对象存储或 Release asset 发布设计，而不是延长临时签名 URL。

### 幂等评论

每个 PR 对该构建工作流只维护一条评论，使用稳定隐藏标记：

```html
<!-- wpf-nuget-artifacts workflow=build pr=<number> -->
```

回写逻辑：

1. 分页读取 PR 评论。
2. 只匹配同时包含 marker 且作者为 `github-actions[bot]` 的评论，防止修改用户伪造的同名 marker。
3. 已存在时更新；不存在时创建。
4. rerun 或新提交完成后更新同一条评论。
5. 如果当前 run 早于评论中记录的 run，则跳过更新。

建议成功评论内容：

```markdown
<!-- wpf-nuget-artifacts workflow=build pr=123 -->
## WPF NuGet 构建

- 结果：成功
- PR head：`<source-head-sha>`
- 测试合并提交：`<workflow-run-head-sha>`
- Actions run：<run-link>
- NuGet artifact：<artifact-link>
- 大小：<size>
- 过期时间：<expires-at-utc>

下载需要 GitHub 登录和仓库读取权限；链接随 artifact 保留期失效。
```

workflow 失败、取消或成功但无有效 nupkg artifact 时，也更新同一评论，写入 conclusion 和 run 链接，但不伪造产物地址。

### 为什么不在构建 job 中直接评论

- fork PR 的 `GITHUB_TOKEN` 通常是只读，不能可靠评论。
- 给执行不可信 PR 代码的同一 job 增加写权限会扩大攻击面。
- 使用个人 PAT 作为构建 job secret 不适用于不可信 fork PR。

### 为什么此处受限使用 `pull_request_target`

通常不应在 `pull_request_target` 中 checkout 并执行 PR 代码，因为该事件可访问 base 仓库的受信任上下文。这里使用它的唯一原因，是 `t/bot/*` 属于同仓库但内容不可信，普通 `pull_request` 会允许该提交替换构建 workflow。该例外必须与只读 Token、零 secrets、无 OIDC、无持久化凭据、无可写缓存、无 self-hosted runner 和固定第三方 action SHA 同时成立；任何后续修改若需要写权限或 secret，必须移到独立受信任工作流，不能加入构建 job。

### 为什么回写工作流不下载 artifact

`workflow_run` 具有比触发构建更高的权限。下载并解析不可信 workflow 产生的 artifact、cache 或脚本可能形成权限提升链。回写只使用 GitHub API 返回的数字 ID、大小、过期时间和固定生成的 GitHub URL，不接触 artifact 内容。

## 测试设计

### Builder 单元测试

独立测试项目 `eng/Builder.Tests` 使用 xUnit，并通过 `eng/Builder.ProcessTestHelper` 验证参数和进程树边界。当前 59 项测试通过，覆盖：

- 标准 PR URL、子页面 URL、大小写 host、无效 scheme/host/path/number 和无效转义。
- GitHub HTTPS/SSH remote 解析，以及 fetch/push 指向不同仓库的拒绝逻辑。
- `t/bot/PR_<number>` 分支名生成。
- source branch fetch 后 SHA 一致与不一致。
- 来源仓库删除时 PR ref fallback。
- 同名远端分支来源 marker 匹配和冲突。
- PR body marker 与评论 marker 幂等查找。
- 构建前后 HEAD/tree/status 校验。
- 参数分项传递，包含空格、引号和以 `-` 开头输入时不形成参数注入。
- 取消和超时会终止进程树。
- Token 和敏感环境不会进入构建子进程或日志。
- GitHub Actions 事件 JSON、版本/包路径/artifact 身份和 `GITHUB_OUTPUT` 单行约束。
- trusted/tested checkout 凭据检测、非 PR 精确 SHA 与 PR merge 双亲身份校验。
- `workflow_run` 关联解析、artifact SHA/名称筛选、Markdown 转义、评论 marker 与 run/attempt 排序。

### Git 集成测试

使用随机临时目录和本地 bare repository 验证：

- 从 base 创建自动分支并生成 `--no-ff` merge commit。
- 冲突时不提交、不 push，并报告冲突文件。
- 无共同历史时拒绝。
- normal push、匹配来源后的 `--force-with-lease` 更新和 lease 竞争失败。
- push 精确验证 SHA，而不是被构建过程移动后的 branch ref。

GitHub 网络 API 不应成为普通单元测试前置条件。Octokit 外部调用通过最小适配边界测试请求与结果映射，真实仓库验证放在显式集成测试或人工验收中。

### Actions 验收矩阵

至少执行以下真实 PR 场景：

| 场景 | 预期 |
|---|---|
| 同仓库分支 PR | 构建成功、上传 nupkg、创建一条 artifact 评论 |
| fork PR | 无 secrets、只读构建成功、受信任 workflow 回写评论 |
| PR 新增提交 | 旧 run 被取消或忽略，评论更新为最新 run |
| rerun 成功 | 更新原评论，不新增重复评论 |
| 构建失败 | 评论写入失败 conclusion 和 run 链接，无 artifact 链接 |
| workflow success 但无 nupkg | 上传步骤或回写契约失败，不宣称有产物 |
| artifact 已过期 | 后续更新时不继续展示为有效下载项 |
| PR 修改 artifact 名称为恶意 Markdown | 评论内容被转义，不产生任意链接或 mention 注入 |
| `workflow_run.pull_requests` 为空 | 安全跳过，只记录诊断 |

## 分阶段实施顺序

### 阶段 A：本地 PR 搬运

1. 增加安全参数化的异步进程执行能力。
2. 增加 PR URL、GitHub remote 和来源元数据解析。
3. 增加独立 clone、固定 SHA fetch、merge 与冲突报告。
4. 增加完整本地构建门禁和构建后提交校验。
5. 增加安全 push、来源冲突检查与 Octokit PR 创建/复用。
6. 增加单元测试和本地 bare repository 集成测试。
7. 在示例 PR 上执行端到端人工验收。前 6 项已实现并通过本地自动化验证；该项仍待专用环境执行。

### 阶段 B：通用 PR 构建与产物回写

1. 收紧现有 build workflow 的权限、checkout 凭据和 artifact 缺失行为。
2. 让 artifact 名稳定包含 PR/run/attempt 身份。
3. 新增只处理元数据的 `workflow_run` 回写工作流。
4. 实现最新 run 判定、artifact 过滤和幂等评论。
5. 依次验证同仓库 PR、fork PR、失败、rerun 和新提交场景。前 4 项已实现并通过静态契约测试；该项仍待真实 PR 矩阵执行。

两个阶段独立交付。阶段 A 创建的 PR 只要进入目标仓库，就与其他人创建的 PR 一样由阶段 B 处理；阶段 B 不依赖 PR 是否由 Builder 创建。

## 验收标准

### 本地命令

- 输入 `https://github.com/dotnet/wpf/pull/11781` 时，运行时能够解析当前真实 head repository、head ref 和完整 head SHA。
- 调用者当前工作区的分支和未提交修改保持不变。
- 自动分支精确命名为 `t/bot/PR_11781`。
- merge 冲突、来源 SHA 变化、无共同历史、本地构建失败或包验证失败时，不产生新的远端分支更新。
- 所有本地门禁成功后，只把记录过的已验证提交 SHA 推送到目标仓库。
- 目标 PR 正确指向自己的仓库、指定 base 和自动分支，并包含原 PR 与 source SHA。
- 重试不会覆盖来源不匹配的同名分支，也不会重复创建同一 PR。
- 日志和状态文件不包含 GitHub Token 或 Git 凭据。

### GitHub Actions

- 同仓库和 fork PR 都在只读、无 secrets 的构建上下文中完成根解决方案与 Builder 包验证。
- workflow success 时必有非空 nupkg artifact。
- 回写工作流只 checkout `github.sha` 的受信任 Builder，不 checkout PR，不下载、不执行 PR 代码或 artifact。
- PR 中存在一条可更新的构建结果评论，包含最新 run 和未过期 artifact 页面链接。
- 评论明确说明登录权限与过期限制。
- 失败、取消、rerun 和新提交不会产生误导性成功链接或重复评论。

## 后续可选扩展

- 支持 GitHub Enterprise Server 的主机和 API base 配置。
- 使用 GitHub App installation token 替代长期 PAT。
- 将构建阶段放入专用临时 VM，并把发布阶段放入独立安全主体。
- 将 artifact 持久化到对象存储并提供可控生命周期 URL。
- 发布为预发布 Release asset，并增加 tag/Release 清理策略。
- 支持显式选择 merge、cherry-pick 或提交子集；首版固定 merge PR head。
