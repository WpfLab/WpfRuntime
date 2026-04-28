# 下一次 AI 对话交接

## 本次工作产出

本次尚未继续做大规模代码迁移，先完成了文档基线建设，目的是为后续长周期重组提供稳定交接点。

已完成：

- 创建 `Docs/` 文档目录。
- 建立总览、阶段计划、交接文档。
- 盘点当前重组仓库的主要顶层目录和项目文件。
- 对照 `origin/src/src/` 梳理出尚未进入当前重组目录的关键模块。

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

1. 当前仓库中尚未通过文件扫描找到 `Microsoft.DotNet.Wpf.sln`。
2. 尚未验证当前已有项目是否可直接整体构建。
3. 本地引用路径 `C:\lindexi\Lib\Microsoft.WindowsDesktop.App\` 可能影响可移植性和构建重现性。

## 下一次对话建议起手顺序

1. 先确认解决方案文件是否存在：
   - 如果存在，记录路径并梳理已纳入项目。
   - 如果不存在，决定是迁入原始解决方案还是新建适配当前结构的解决方案。
2. 验证当前核心托管项目的可构建性：
   - `WindowsBase`
   - `System.Xaml`
   - `PresentationCore`
   - `UIAutomation` 系列
3. 把构建失败按“缺文件 / 缺引用 / 缺项目 / 路径不匹配 / 生成步骤缺失”分类记录。
4. 在进入 `PresentationFramework` 迁移前，先补齐最小依赖链说明。

## 下一次对话建议直接复制的提示词

你正在接手 `WpfReorganize` 仓库的 WPF 重组工作。请先阅读：

1. `Docs/README.md`
2. `Docs/00-overview.md`
3. `Docs/01-phase-plan.md`
4. `Docs/02-next-session-handoff.md`

然后优先完成以下事项：

- 确认 `Microsoft.DotNet.Wpf.sln` 的现状。
- 梳理当前仓库项目与解决方案入口的对应关系。
- 验证 `WindowsBase`、`System.Xaml`、`PresentationCore` 的构建状态。
- 将发现的阻塞点回写到 Docs 文档中。

## 每次结束前必须回写的信息

- 当前已迁移模块列表
- 本轮新增项目或目录
- 本轮验证过的构建入口
- 当前阻塞点
- 下一轮第一步该做什么
