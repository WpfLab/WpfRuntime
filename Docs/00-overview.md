# 当前概览

## 项目目标

将当前 WPF 仓库重组为一个更易维护的结构，逐步消除复杂项目引用带来的构建困难，最终达到以下目标：

- 可以直接在 Visual Studio 中打开并成功构建 `Microsoft.DotNet.Wpf.sln`。
- 或者可以使用一条 `msbuild` 命令对该解决方案完成构建。
- 在重组过程中尽量保持与原始 WPF 仓库的源码目录和模块边界一致，降低后续同步与排障成本。

## 当前仓库已确认状态

### 基础构建环境

- 仓库根目录存在 `Directory.Build.props`、`Directory.Build.targets`、`global.json`。
- 当前统一目标框架为 `.NET 8`。
- `global.json` 指定 SDK 为 `8.0.206`。
- 当前仓库根目录已存在解决方案入口：`Microsoft.Dotnet.Wpf.sln`。
- 当前仓库通过 `WpfSourceDir`、`WpfSharedDir`、`WpfCommonDir` 指向 `src/Microsoft.DotNet.Wpf/src/` 下的重组源码。
- 当前仓库已新增 `WpfCycleBreakersDir`、`WpfCodeGenDir`，分别用于指向当前仓库下的 `cycle-breakers/` 与 `eng/WpfArcadeSdk/tools/`。
- 当前仓库依赖本地 `C:\lindexi\Lib\Microsoft.WindowsDesktop.App\` 作为部分引用路径。
- 当前仓库已引入 `eng/WpfArcadeSdk/SystemResources.props` 以支持资源生成相关构建能力。

### 当前已存在的顶层源码目录

当前 `src/Microsoft.DotNet.Wpf/src/` 下已确认存在：

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

### 当前已确认存在的项目文件

当前仓库中已能找到以下项目文件（含部分 ref 项目）：

- `PresentationCore/PresentationCore.csproj`
- `System.Windows.Input.Manipulations/System.Windows.Input.Manipulations.csproj`
- `System.Xaml/System.Xaml.csproj`
- `UIAutomation/UIAutomationTypes/UIAutomationTypes.csproj`
- `UIAutomation/UIAutomationProvider/UIAutomationProvider.csproj`
- `UIAutomation/UIAutomationClient/UIAutomationClient.csproj`
- `UIAutomation/UIAutomationClientSideProviders/UIAutomationClientSideProviders.csproj`
- `WindowsBase/WindowsBase.csproj`
- `WindowsFormsIntegration/WindowsFormsIntegration.csproj`
- 若干 `ref/*.csproj`
- `Shared/Tracing/mcwpf/mcwpf.csproj`

### 当前解决方案已纳入的项目

`Microsoft.Dotnet.Wpf.sln` 当前已纳入：

- `System.Xaml`
- `WindowsBase`
- `System.Windows.Input.Manipulations`
- `UIAutomationTypes`
- `UIAutomationProvider`
- `UIAutomationClient`
- `UIAutomationClientSideProviders`
- `DirectWriteForwarder`
- `Demo/WpfDemo`

### 原始仓库顶层源码目录

`origin/src/src/` 下已确认存在：

- `Common`
- `DirectWriteForwarder`
- `Extensions`
- `PenImc`
- `PresentationBuildTasks`
- `PresentationCore`
- `PresentationFramework`
- `PresentationUI`
- `ReachFramework`
- `Shared`
- `System.Printing`
- `System.Windows.Controls.Ribbon`
- `System.Windows.Input.Manipulations`
- `System.Windows.Presentation`
- `System.Xaml`
- `Themes`
- `UIAutomation`
- `WindowsBase`
- `WindowsFormsIntegration`
- `WpfGfx`

### 对比后确认尚未进入当前重组顶层目录的模块

以下模块目前在 `origin/src/src/` 中存在，但尚未出现在当前重组仓库的对应顶层目录中：

- `Extensions`
- `PenImc`
- `PresentationBuildTasks`
- `PresentationFramework`
- `PresentationUI`
- `ReachFramework`
- `System.Printing`
- `System.Windows.Controls.Ribbon`
- `System.Windows.Presentation`
- `Themes`
- `WpfGfx`

## 当前进度判断

从目录和项目文件情况看，当前工作已经完成了以下基础迁移：

1. 已建立新的根级构建配置。
2. 已迁入一批核心托管项目，至少包括：`WindowsBase`、`System.Xaml`、`PresentationCore`、`UIAutomation` 系列、`WindowsFormsIntegration`、`System.Windows.Input.Manipulations`。
3. 已确认并使用根目录 `Microsoft.Dotnet.Wpf.sln` 作为当前解决方案级入口。
4. 已将 `PresentationCore`、`UIAutomationClient`、`UIAutomationClientSideProviders`、`WindowsFormsIntegration` 纳入当前磁盘上的 `Microsoft.Dotnet.Wpf.sln`。
5. 已开始处理共享源码目录和公共构建属性，使部分项目可以通过共享文件方式编译。
6. 已确认当前解决方案本身可以成功构建，但这并不代表目录中所有现存项目都已具备独立构建能力。
7. 已开始针对 `PresentationCore` 进行“缺失源码补齐型迁移”，从 `origin/src/src/PresentationCore/` 拷贝首批 `TextInterface`、`Interop/DWrite`、`BinaryFormat`、`UISettings` 相关源码，并同步补充 `PresentationCore.csproj` 编译项。
8. 仓库中已经存在对原始 WPF 结构的明显映射关系，说明当前不是从零开始，而是处于“持续搬迁与校正引用”的阶段。
9. 已从 `origin/src/src/` 批量拷贝 `PresentationFramework`、`ReachFramework`、`Themes`、`PresentationBuildTasks`、`PresentationUI`、`System.Printing`、`System.Windows.Controls.Ribbon`、`Extensions` 到当前重组目录，用于先闭合上层目录结构和项目引用入口。
10. 已验证 `PresentationFramework` 项目文件可开始进入真实构建诊断，但当前会先被缺失的循环桥接项目和 `$(WpfCodeGenDir)AvTrace\GenAvMessages.targets` 阻塞。
11. 已在仓库根目录补齐首批 `cycle-breakers` 项目，至少包括 `PresentationUI-PresentationFramework-impl-cycle`、`PresentationFramework-ReachFramework-impl-cycle`、`PresentationFramework-System.Printing-api-cycle`、`PresentationFramework-PresentationUI-api-cycle`、`ReachFramework-PresentationFramework-api-cycle`、`ReachFramework-System.Printing-api-cycle`、`System.Printing-PresentationFramework-api-cycle`。
12. 已将 `PresentationFramework.csproj` 中的 `GenAvMessages.targets` 改为条件导入，使其不再因目标文件缺失而在项目评估阶段直接失败。
13. 重新验证后，`PresentationFramework` 已越过 cycle-breaker/AvTrace 层面的首批阻塞，依赖重新收敛到 `PresentationCore` 与 `DirectWriteForwarder` 的 DirectWrite 构建链。
14. 已将 `PresentationCore.csproj` 恢复为引用仓库内 `DirectWriteForwarder.vcxproj`，不再依赖外部 `DirectWriteForwarder.dll` 文件引用。
15. 已验证根解决方案在纳入 `PresentationCore`、`UIAutomationClient`、`UIAutomationClientSideProviders`、`WindowsFormsIntegration` 后仍可成功构建。

## 当前主要缺口

1. 缺少多个关键顶层模块，尤其是：
   - `WpfGfx`
   - `PenImc`
   - `System.Windows.Presentation`
2. 解决方案入口虽已存在，但项目纳管仍不完整，至少还有 `PresentationFramework`、`ReachFramework`、`Themes`、`PresentationBuildTasks`、`System.Printing`、`PresentationUI`、`System.Windows.Controls.Ribbon` 等现存项目未纳入当前磁盘上的解决方案文件。
3. `WindowsFormsIntegration` 当前仍直接依赖缺失的 `PresentationFramework`，说明上层托管链尚未闭合。
4. `PresentationCore` 当前迁移并不完整，虽然 `TextInterface` 相关缺口已重新收敛到真实的 `DirectWriteForwarder` 依赖，但独立构建仍会被本地 VC++ 构建任务失败阻塞。
5. 新迁入的上层项目虽然已进入当前目录，但仍未完成构建前置补齐，当前已确认存在以下阻塞：
   - `PresentationCore` 已恢复到真实的 `DirectWriteForwarder` 依赖链，但当前独立构建会被本地 VC++ 构建任务 `GetOutOfDateItems` 的 `TypeLoadException` 阻塞
   - `WpfCodeGenDir` 虽已定义且 `PresentationFramework` 已改为条件导入，但 `AvTrace\GenAvMessages.targets` 本体仍未接入当前仓库
6. 部分项目虽然已存在，但是否已经全部可构建仍未在本轮完成闭环验证。
7. 当前文档体系刚建立，后续需要把每次迁移结果持续补充进来。

## 已明确的迁移顺序

当前先按“先纳管已存在项目，再引入缺失模块”的顺序推进：

1. 先收敛当前仓库中已存在但未充分纳入解决方案的项目：
   - `UIAutomationClient`
   - `UIAutomationClientSideProviders`
2. 再进入上层托管主线模块：
   - `PresentationFramework`
   - `WindowsFormsIntegration`（在 `PresentationFramework` 进入后再验证并纳管）
   - `ReachFramework`
   - `Themes`
3. 再处理构建任务与功能扩展模块：
   - `PresentationBuildTasks`
   - `System.Windows.Controls.Ribbon`
   - `System.Printing`
   - `PresentationUI`
   - `Extensions`
4. 最后处理底层或 native 依赖更重的模块：
   - `PenImc`
   - `System.Windows.Presentation`
   - `WpfGfx`

## 建议的当前优先级

1. 先解决 `DirectWriteForwarder.vcxproj` 在当前环境下的 VC++ 构建任务失败，使 `PresentationCore` 可以走通真实依赖链独立构建。
2. 在 `PresentationCore` 能独立构建后，再验证 `UIAutomationClient`、`UIAutomationClientSideProviders` 的构建状态。
3. 继续补齐 `AvTrace` 代码生成目标来源，使 `PresentationFramework` 不仅能跳过导入失败，还能恢复预期生成链路。
4. 以 `PresentationFramework` 为前置条件，重新评估 `WindowsFormsIntegration` 的独立构建状态。
5. 同步梳理原始仓库到当前仓库的目录映射和引用改写规则。
6. 每完成一个模块迁移，就记录：
   - 新增项目
   - 新增目录
   - 调整过的项目引用
   - 当前可构建状态
   - 尚未解决的阻塞点
