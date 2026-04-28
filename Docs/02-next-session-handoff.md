# 下一次 AI 对话交接

## 本次工作产出

本次已从“仅做文档基线”推进到“开始真实构建验证与缺失源码补齐”，目的是让后续迁移围绕一个真实可用的解决方案入口和可独立构建的项目链持续前进。

已完成：

- 确认当前仓库根目录存在 `Microsoft.Dotnet.Wpf.sln`。
- 核对了磁盘上解决方案当前已纳入项目。
- 验证当前 `Microsoft.Dotnet.Wpf.sln` 可以成功构建。
- 验证 `UIAutomationClient` 独立构建时会被 `PresentationCore` 阻塞。
- 对照当前仓库与 `origin/src/src/`，确认关键缺失模块仍未迁入。
- 对照 `origin/src/src/PresentationCore/PresentationCore.csproj` 与当前 `PresentationCore.csproj`，确认当前 `PresentationCore` 缺失一批源码与编译项。
- 已从 `origin/src/src/PresentationCore/` 拷贝首批 `TextInterface`、`Interop/DWrite`、`BinaryFormat`、`UISettings` 相关源码，并同步更新 `PresentationCore.csproj`。
- 已从 `origin/src/src/` 批量拷贝 `PresentationFramework`、`ReachFramework`、`Themes`、`PresentationBuildTasks`、`PresentationUI`、`System.Printing`、`System.Windows.Controls.Ribbon`、`Extensions` 到当前重组目录。
- 已验证 `PresentationFramework` 当前可以开始进入真实构建诊断，但先被缺失的 cycle-breaker 项目与 `AvTrace\GenAvMessages.targets` 阻塞。

## 当前已知事实

### 当前仓库已有的顶层目录

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
- `DirectWriteForwarder`
- `Docs`
- `Demo/WpfDemo`

### 当前已确认未纳入解决方案但已存在于目录中的主要项目

- `PresentationCore`
- `WindowsFormsIntegration`
- `UIAutomationClient`
- `UIAutomationClientSideProviders`

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

- `PenImc`
- `System.Windows.Presentation`
- `WpfGfx`

### 已知不确定点

1. 当前解决方案本身可以构建，但这不代表目录内未纳管项目也可独立构建。
2. `WindowsFormsIntegration` 仍依赖尚未迁入的 `PresentationFramework`。
3. `PresentationCore` 已在目录中存在，但当前既未纳入根解决方案，也尚未完成源码补齐。
4. `PresentationCore` 当前仍缺少更多 DirectWrite/TextInterface 相关托管类型，例如 `IFontSourceCollection`、`IFontSource`、`GlyphOffset`、`DWriteFontFeature` 等。
5. 本地引用路径 `C:\lindexi\Lib\Microsoft.WindowsDesktop.App\` 可能影响可移植性和构建重现性。
6. `PresentationFramework` 当前还缺少 `PresentationUI-PresentationFramework-impl-cycle.csproj`、`PresentationFramework-ReachFramework-impl-cycle.csproj`、`PresentationFramework-System.Printing-api-cycle.csproj`、`ReachFramework-System.Printing-api-cycle.csproj` 等桥接项目。
7. `PresentationFramework` 还依赖 `$(WpfCodeGenDir)AvTrace\GenAvMessages.targets`，当前仓库尚未接入这一代码生成目标。

## 下一次对话建议起手顺序

1. 先继续对照 `origin/src/src/PresentationCore/PresentationCore.csproj`，批量补齐当前 `PresentationCore.csproj` 缺失的源码文件与 `<Compile Include>` 项。
2. 优先处理当前已暴露的缺口类型：`GlyphOffset`、`DWriteFontFeature`、`ItemProps` 及其相关 DirectWrite/TextInterface 托管包装源码来源。
3. 对照文档 `Documentation/cycle-breakers.md`、原始仓库项目结构，补齐 `PresentationFramework` / `ReachFramework` / `System.Printing` 所需的 cycle-breaker 项目。
4. 梳理并接入 `WpfCodeGenDir` 与 `AvTrace\GenAvMessages.targets` 的当前仓库位置，使 `PresentationFramework` 至少能越过 import 阻塞。
5. 重新验证 `PresentationCore` 与 `PresentationFramework` 的独立构建状态。
6. 在 `PresentationCore` 能独立构建后，再验证：
   - `UIAutomationClient`
   - `UIAutomationClientSideProviders`
7. 把构建失败按“缺文件 / 缺引用 / 缺项目 / 路径不匹配 / 生成步骤缺失”分类记录。
8. 再同步判断 `WindowsFormsIntegration` 的阻塞是否解除。

## 下一次对话建议直接复制的提示词

你正在接手 `WpfReorganize` 仓库的 WPF 重组工作。请先阅读：

1. `Docs/README.md`
2. `Docs/00-overview.md`
3. `Docs/01-phase-plan.md`
4. `Docs/02-next-session-handoff.md`

然后优先完成以下事项：

- 验证 `Microsoft.Dotnet.Wpf.sln` 当前纳管项目的构建状态。
- 继续按 `origin/src/src/PresentationCore/PresentationCore.csproj` 补齐 `PresentationCore` 缺失源码与编译项。
- 先补齐 `PresentationFramework` 当前缺失的 cycle-breaker 项目和 `AvTrace` 代码生成目标。
- 先打通 `PresentationCore` 的独立构建，再验证 `UIAutomationClient`、`UIAutomationClientSideProviders`。
- 梳理当前仓库项目与解决方案入口的对应关系，尤其是未纳入的 `PresentationCore`、`PresentationFramework`、`WindowsFormsIntegration`。
- 将发现的阻塞点回写到 Docs 文档中。

## 每次结束前必须回写的信息

- 当前已迁移模块列表
- 本轮新增项目或目录
- 本轮验证过的构建入口
- 当前阻塞点
- 下一轮第一步该做什么
