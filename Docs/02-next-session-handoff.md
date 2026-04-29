# 下一次 AI 对话交接

## 交接目标

该文档用于让后续 AI 直接接手当前仓库，而不是重复调查仓库结构。以下内容已根据当前工作区重新核对。

## 当前已验证事实

### 解决方案与构建基础设施

- 当前解决方案入口：`Microsoft.Dotnet.Wpf.sln`
- 当前 SDK：`8.0.206`
- 当前统一目标框架：`.NET 8`
- 当前关键属性：
  - `WpfSourceDir=src\Microsoft.DotNet.Wpf\src\`
  - `WpfSharedDir=src\Microsoft.DotNet.Wpf\src\Shared\`
  - `WpfCommonDir=src\Microsoft.DotNet.Wpf\src\Common\`
  - `WpfCycleBreakersDir=cycle-breakers\`
  - `WpfCodeGenDir=eng\WpfArcadeSdk\tools\`
- 当前仓库仍依赖本地 `C:\lindexi\Lib\Microsoft.WindowsDesktop.App\` 路径。

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

### 当前解决方案已纳入的项目

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

### 当前磁盘已存在但尚未纳入解决方案的主要项目

- `WindowsFormsIntegration`
- `PresentationFramework`
- `PresentationUI`
- `ReachFramework`
- `System.Printing`
- `System.Windows.Controls.Ribbon`
- `PresentationBuildTasks`
- `Themes` 相关项目
- 大部分 `ref/*.csproj`
- `cycle-breakers/*.csproj`

### 当前已确认存在的 cycle-breaker 项目

- `PresentationFramework-PresentationUI-api-cycle`
- `PresentationFramework-ReachFramework-impl-cycle`
- `PresentationFramework-System.Printing-api-cycle`
- `PresentationFramework-System.Printing-impl-cycle`
- `PresentationUI-PresentationFramework-impl-cycle`
- `ReachFramework-PresentationFramework-api-cycle`
- `ReachFramework-System.Printing-api-cycle`
- `System.Printing-PresentationFramework-api-cycle`

### 当前尚未进入顶层目录的关键模块

- `PenImc`
- `System.Windows.Presentation`
- `WpfGfx`

## 最新构建结果

### 当前解决方案构建成功

对当前工作区重新执行整体构建后，当前解决方案入口可构建：

- 命令：`msbuild C:\lindexi\Code\God\WpfReorganize\Microsoft.Dotnet.Wpf.sln -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal`
- 结果：构建成功。

### 这意味着什么

1. 当前已纳入解决方案的项目恢复到可构建基线。
2. `UIAutomationClientSideProviders` 下游 `CS0006` 没有复现；`UIAutomationClient` 独立构建可通过。
3. 后续重点应放在 `ReachFramework` / `PresentationFramework` 主链，而不是继续排查已恢复的 `UIAutomationClient` 问题。
4. 当前主链阻塞集中在 cycle-breaker API 边界：`ReachFramework-ref` 需要 `XpsDocumentWriter` / `ISerializerFactory` 等 `PresentationFramework` API，但将相关类型放入 `ReachFramework` bridge 后又会引发与 `PresentationFramework` / `System.Printing` 的同名类型冲突。
5. `ReachFramework-ref` 已通过 `PresentationFramework-System.Printing-api-cycle` 补齐 `XpsDocumentWriter` / `ISerializerFactory` 并可独立构建；`ReachFramework` 实现项目仍失败，首个真实阻塞是 `XpsSerializerWriter` 与 `XpsDocument` 调用 `XpsDocumentWriter` 时的 `PrintTicket` / `XpsDocument` 类型身份不一致。

## 建议起手顺序

1. 先阅读：
   - `Docs/README.md`
   - `Docs/00-overview.md`
   - `Docs/01-phase-plan.md`
   - `Docs/cycle-breaker.md`
   - `Microsoft.Dotnet.Wpf.sln`
   - `Directory.Build.props`
   - `Directory.Build.targets`
2. 先用上述 `msbuild` 命令确认解决方案基线仍可构建。
3. 继续处理：
   - `ReachFramework`
   - `System.Printing-ref`
   - `PresentationFramework`
   - `WindowsFormsIntegration`
4. 排查 `ReachFramework` 时重点检查：
   - `PresentationFramework-ReachFramework-impl-cycle` 与 `PresentationFramework-System.Printing-api-cycle` 的同名 `PresentationFramework.dll` 引用是否被 MSBuild 去重。
   - `XpsDocumentWriter` / `ISerializerFactory` 已由 `PresentationFramework-System.Printing-api-cycle` 暴露给 `ReachFramework-ref`，但实现项目不应同时引用会造成 `XpsDocumentWriter` 或 `PrintTicket` 重复暴露的 bridge 输出。
   - `PrintTicket`、`PrintTicketLevel`、`FixedDocumentSequence`、`SerializerWriter`、`XpsDocument` 是否同时从多个同名程序集暴露。
5. 只有在上层主链重新进入稳定状态后，再继续补缺失顶层模块：
   - `PenImc`
   - `System.Windows.Presentation`
   - `WpfGfx`

## 推荐命令方向

- 解决方案或托管项目：`msbuild <project-or-sln> -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal`
- 涉及 `DirectWriteForwarder` 等 native 项目时，优先使用 `msbuild`，不要默认使用 `dotnet msbuild`。

## 当前需要持续遵守的约束

- 禁止再次通过把 `eng/Versions.props` 中的 `AssemblyVersion` 改成 `4.0.0.0` 来掩盖 `WindowsBase` 或其他同名程序集冲突。
- 若出现 `Rect`、`Point`、`DependencyObject` 等基础类型错误，先检查是否同时引用了仓库项目和 SDK inbox 程序集。
- 不要通过把项目从解决方案中移除来制造“构建通过”。

## 结束前必须回写的信息

- 当前已验证的构建入口与命令
- 当前首个真实失败点
- 新增项目、目录或项目纳管变化
- 当前仍未解决的阻塞
- 后续 AI 开始时应该先做什么


