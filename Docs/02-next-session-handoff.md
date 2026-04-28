# 下一次 AI 对话交接

## 本次工作产出

本次已从“仅做文档基线”推进到“开始解决方案纳管型迁移”，目的是让后续迁移围绕一个真实可用的解决方案入口持续前进。

已完成：

- 确认当前仓库根目录存在 `Microsoft.Dotnet.Wpf.sln`。
- 核对解决方案当前已纳入项目。
- 将 `UIAutomationClient`、`UIAutomationClientSideProviders` 纳入当前解决方案。
- 对照当前仓库与 `origin/src/src/`，确认关键缺失模块仍未迁入。
- 开始在计划文档中列出迁移项目顺序。

## 当前已知事实

### 当前仓库已有的顶层目录

- `Common`
- `DirectWriteForwarder`
- `PresentationCore`
- `Shared`
- `System.Windows.Input.Manipulations`
- `System.Xaml`
- `UIAutomation`
- `WindowsBase`
- `WindowsFormsIntegration`

### 当前仓库已确认存在的主要项目

- `src/Microsoft.DotNet.Wpf/src/WindowsBase/WindowsBase.csproj`
- `src/Microsoft.DotNet.Wpf/src/System.Xaml/System.Xaml.csproj`
- `src/Microsoft.DotNet.Wpf/src/PresentationCore/PresentationCore.csproj`
- `src/Microsoft.DotNet.Wpf/src/UIAutomation/UIAutomationTypes/UIAutomationTypes.csproj`
- `src/Microsoft.DotNet.Wpf/src/UIAutomation/UIAutomationProvider/UIAutomationProvider.csproj`
- `src/Microsoft.DotNet.Wpf/src/UIAutomation/UIAutomationClient/UIAutomationClient.csproj`
- `src/Microsoft.DotNet.Wpf/src/UIAutomation/UIAutomationClientSideProviders/UIAutomationClientSideProviders.csproj`
- `src/Microsoft.DotNet.Wpf/src/WindowsFormsIntegration/WindowsFormsIntegration.csproj`
- `src/Microsoft.DotNet.Wpf/src/System.Windows.Input.Manipulations/System.Windows.Input.Manipulations.csproj`

### 当前解决方案已纳入的项目

- `System.Xaml`
- `WindowsBase`
- `System.Windows.Input.Manipulations`
- `UIAutomationTypes`
- `UIAutomationProvider`
- `UIAutomationClient`
- `UIAutomationClientSideProviders`
- `DirectWriteForwarder`
- `Demo/WpfDemo`

### 当前已列明的迁移顺序

1. 先收敛已存在项目：
   - `UIAutomationClient`
   - `UIAutomationClientSideProviders`
2. 再迁入上层主线：
   - `PresentationFramework`
   - `WindowsFormsIntegration`
   - `ReachFramework`
   - `Themes`
3. 再迁入构建任务和扩展模块：
   - `PresentationBuildTasks`
   - `System.Windows.Controls.Ribbon`
   - `System.Printing`
   - `PresentationUI`
   - `Extensions`
4. 最后处理底层或 native 依赖较重模块：
   - `PenImc`
   - `System.Windows.Presentation`
   - `WpfGfx`

### 尚未进入当前重组顶层目录的关键模块

- `PresentationFramework`
- `ReachFramework`
- `Themes`
- `PresentationBuildTasks`
- `System.Printing`
- `System.Windows.Controls.Ribbon`
- `PresentationUI`
- `Extensions`
- `PenImc`
- `System.Windows.Presentation`
- `WpfGfx`

### 已知不确定点

1. 尚未验证当前解决方案在纳入 `UIAutomationClient`、`UIAutomationClientSideProviders` 后是否可稳定构建。
2. `WindowsFormsIntegration` 仍依赖尚未迁入的 `PresentationFramework`。
3. `PresentationCore` 已在目录中存在，但当前尚未纳入根解决方案。
4. 本地引用路径 `C:\lindexi\Lib\Microsoft.WindowsDesktop.App\` 可能影响可移植性和构建重现性。

## 下一次对话建议起手顺序

1. 先验证当前解决方案或至少新增纳管项目的构建状态：
   - `UIAutomationClient`
   - `UIAutomationClientSideProviders`
2. 再验证当前核心托管项目的可构建性：
   - `WindowsBase`
   - `System.Xaml`
   - `PresentationCore`
   - `UIAutomation` 系列
3. 把构建失败按“缺文件 / 缺引用 / 缺项目 / 路径不匹配 / 生成步骤缺失”分类记录。
4. 开始正式迁入 `PresentationFramework`，并同步判断 `WindowsFormsIntegration` 的阻塞是否解除。

## 下一次对话建议直接复制的提示词

你正在接手 `WpfReorganize` 仓库的 WPF 重组工作。请先阅读：

1. `Docs/README.md`
2. `Docs/00-overview.md`
3. `Docs/01-phase-plan.md`
4. `Docs/02-next-session-handoff.md`

然后优先完成以下事项：

- 验证 `Microsoft.Dotnet.Wpf.sln` 当前纳管项目的构建状态。
- 梳理当前仓库项目与解决方案入口的对应关系，尤其是未纳入的 `PresentationCore`、`WindowsFormsIntegration`。
- 验证 `WindowsBase`、`System.Xaml`、`PresentationCore`、`UIAutomationClient`、`UIAutomationClientSideProviders` 的构建状态。
- 开始迁入 `PresentationFramework`，并把发现的依赖顺序继续写回 Docs。
- 将发现的阻塞点回写到 Docs 文档中。

## 每次结束前必须回写的信息

- 当前已迁移模块列表
- 本轮新增项目或目录
- 本轮验证过的构建入口
- 当前阻塞点
- 下一轮第一步该做什么
