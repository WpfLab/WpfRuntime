# 本次会话探索记录与交接 (2024-06)

## 本轮已完成的修复

### 修复 1：`Microsoft.WinFX.targets` 添加 `Any CPU` 平台路径回退

**文件**：`src\Microsoft.DotNet.Wpf\src\PresentationBuildTasks\Microsoft.WinFX.targets`

原有逻辑只查找 `$(WpfNativePlatform)`（= `x64`）路径下的 `PresentationBuildTasks.dll`。当构建使用 `Platform=Any CPU` 时，`PresentationBuildTasks` 输出在 `Any CPU\Debug\net472\`，找不到 DLL。

**已添加**：`_PresentationBuildTasksAssemblyWithActualPlatform` 属性，使用 `$(Platform)` 构建路径，在 `$(WpfNativePlatform)` 路径查找失败后作为回退。

### 修复 2：`PresentationUI.csproj` 添加 `PresentationBuildTasks` 构建依赖

**文件**：`src\Microsoft.DotNet.Wpf\src\PresentationUI\PresentationUI.csproj`

`PresentationUI` 导入 `Microsoft.WinFX.targets` 但没有 `ProjectReference` 到 `PresentationBuildTasks`，导致不确定的构建顺序。

**已添加**：`ProjectReference` 到 `PresentationBuildTasks.csproj`，设置 `ReferenceOutputAssembly=false` 和 `SkipGetTargetFrameworkProperties=true`，确保构建顺序而不引入程序集引用。

### 修复 3：`PresentationBuildTasks.csproj` 移除嵌套 MSBuild 目标

**文件**：`src\Microsoft.DotNet.Wpf\src\PresentationBuildTasks\PresentationBuildTasks.csproj`

移除了 `EnsureNet472BuiltForMarkupCompilation` 目标（原目标在 net8.0 内部构建中嵌套调用 MSBuild 构建 net472，导致多进程文件锁冲突）。当前 `Platforms=AnyCPU;x64;arm64` 已确保 x64 内部构建自然产生 net472 DLL。

### 修复 4：`System.Printing-ref.csproj` 统一平台路径

**文件**：`src\Microsoft.DotNet.Wpf\src\System.Printing\ref\System.Printing-ref.csproj`

将 cycle-breaker `ProjectReference` 的 `AdditionalProperties` 和 `Reference` 的 `HintPath` 从 `$(Platform)` 改为 `$(WpfNativePlatform)`，确保 Always 解析到 `x64` 路径。

### 修复 5：添加 `XpsDocumentWriter` cycle-breaker 存根

**文件**：
- `cycle-breakers/ReachFramework/System.Windows.Xps.XpsDocumentWriter.cs`（新建）
- `cycle-breakers/ReachFramework/System.Windows.Xps.Packaging.XpsDocument.cs`（修改）
- `cycle-breakers/ReachFramework/ReachFramework-System.Printing-api-cycle.csproj`（修改）

`ReachFramework-ref` 引用 `XpsDocumentWriter` 类型，但 SDK inbox `ReachFramework` 被 `RemoveInboxWpfReference` 移除后，cycle-breaker 没有提供该类型的存根。

**已添加**：`XpsDocumentWriter` 存根类 + `XpsDocument.CreateXpsDocumentWriter` 静态方法存根。

---

## 当前构建状态

### x64 平台：✅ 构建成功

```bash
Remove-Item -Recurse -Force artifacts
msbuild Microsoft.Dotnet.Wpf.sln -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal /clp:ErrorsOnly
```

### Any CPU 平台：❌ 构建失败

错误分为两类：

#### 错误组 A：`WindowsFormsIntegration` 缺少 WPF 基础类型

```
CS0246: FrameworkElement, HwndHost, Thickness, AdornerDecorator, SizeChangedEventArgs...
CS0234: System.Windows.Controls.Panel/DockPanel, System.Windows.Window
CS0535: WindowsFormsHost 不实现 IKeyboardInputSink 接口成员
```

根因分析：`WindowsFormsIntegration` 依赖 `PresentationFramework` 和 `PresentationCore` 中的类型，但 Any CPU 平台构建时 `RemoveInboxWpfReference` 移除了 SDK inbox 程序集后，仓库自建的 `PresentationFramework` 可能未被正确解析。需检查：
1. `WindowsFormsIntegration` 的 `ProjectReference` 链在 Any CPU 下的解析路径
2. 是否因为平台不匹配导致 `ProjectReference` 输出路径错误

#### 错误组 B：`PresentationUI` XAML 标记编译失败

```
MC1000: 已知类型值 380='LostFocusEventManager' 不是有效的已知类型
```

根因分析：`PresentationUI` 的 XAML 标记编译（`MarkupCompilePass1`）在使用 `PresentationBuildTasks.dll` 解析已知类型时找不到 `LostFocusEventManager`。这个类型定义在 `PresentationFramework` 中。在 x64 构建中正常，Any CPU 构建中失败，说明标记编译 task 加载的程序集路径可能因平台不同而变化。

**联合根因假设**：`Any CPU` 平台（带空格）和项目级 `Platforms=AnyCPU`（无空格）的失配，导致部分 `ProjectReference` 输出的 HintPath 解析到了错误的路径。`Directory.Build.props` 曾尝试 `Platform` 规范化（`Any CPU` → `AnyCPU` / `x64`），但每次尝试都会引入新的问题（文件锁、cycle-breaker 路径不同步等）。

---

## 建议后续修复策略

### 策略 A（推荐）：在 sln 层面统一处理

在 `Microsoft.Dotnet.Wpf.sln` 的解决方案配置中，将 `Any CPU` 映射到 `x64`。这样 `Platform=x64` 和 `Platform="Any CPU"` 等效，用户无论选哪个都能成功构建。

### 策略 B：在 `Directory.Build.props` 中彻底规范化

规范化 `Platform`（`Any CPU` → `AnyCPU` 或 `x64`），但需要同步修复所有硬编码平台路径的 HintPath：

| 文件 | 位置 | 当前 | 应改为 |
|------|------|------|--------|
| `System.Printing-ref.csproj` | cycle-breaker HintPath | `$(WpfNativePlatform)` | 已修复 ✅ |
| `ReachFramework-ref.csproj` | `System.Printing` HintPath | `$(WpfBridgePlatform)` | 保持 `$(WpfBridgePlatform)` |
| `PresentationUI.csproj` | `PresentationFramework` HintPath | `$(WpfNativePlatform)` | 无需修改 |
| `WindowsFormsIntegration.csproj` | `PresentationFramework` HintPath | `$(WpfNativePlatform)` | 无需修改 |

### 策略 C：修复具体错误

逐个修复：
1. `WindowsFormsIntegration` CS0234：检查 ProjectReference 链，可能需要显式添加 PresentationFramework/WindowsBase/DirectWriteForwarder 的项目引用
2. `PresentationUI` MC1000：检查标记编译时 PresentationBuildTasks 加载 PresentationFramework.dll 的路径是否正确

---

## 本次修改的文件清单

| 文件 | 修改类型 | 说明 |
|------|---------|------|
| `src/.../PresentationBuildTasks/Microsoft.WinFX.targets` | 修改 | 添加 `_PresentationBuildTasksAssemblyWithActualPlatform` 路径回退 |
| `src/.../PresentationUI/PresentationUI.csproj` | 修改 | 添加 `PresentationBuildTasks` 构建依赖 |
| `src/.../PresentationBuildTasks/PresentationBuildTasks.csproj` | 修改 | 移除 `EnsureNet472BuiltForMarkupCompilation` 目标 |
| `src/.../System.Printing/ref/System.Printing-ref.csproj` | 修改 | 统一 `$(Platform)` → `$(WpfNativePlatform)` |
| `src/.../ReachFramework/ref/ReachFramework-ref.csproj` | 修改 | 尝试多种路径修复（最终恢复原始 HintPath） |
| `cycle-breakers/ReachFramework/System.Windows.Xps.XpsDocumentWriter.cs` | 新建 | `XpsDocumentWriter` 存根 |
| `cycle-breakers/ReachFramework/System.Windows.Xps.Packaging.XpsDocument.cs` | 修改 | 添加 `CreateXpsDocumentWriter` 方法 |
| `cycle-breakers/ReachFramework/ReachFramework-System.Printing-api-cycle.csproj` | 修改 | 添加 `XpsDocumentWriter.cs` 编译项 |
| `Directory.Build.props` | 修改（已回退） | 曾尝试 Platform 规范化，最终保持原始状态 |

---

## 下一步首选举措

1. **验证 x64 干净构建仍然通过** — 每次开始前必做。
2. **采用策略 A**：在 `Microsoft.Dotnet.Wpf.sln` 中将 `Any CPU` 解决方案平台映射到 `x64`。这是改动最小、风险最低的方案。
3. 验证 Any CPU 构建通过后，继续处理 **Builder 构建器**（`Docs/05-builder-plan.md`）。

---

## 策略 A 实施记录 (后续会话)

### 已完成

在 `Microsoft.Dotnet.Wpf.sln` 的 `GlobalSection(ProjectConfigurationPlatforms)` 中，将除以下 4 个例外项目外的所有托管项目的 `Any CPU` 平台映射到 `x64`：

- `DirectWriteForwarder.vcxproj` — 早已映射到 `x64`，保持不变
- `Builder.csproj` — 始终使用 `Any CPU`
- `WpfDemo.csproj` — 始终使用 `Any CPU`
- `Docs.csproj` — 始终使用 `Any CPU`

修改映射：
- `*.Debug|Any CPU.ActiveCfg = Debug|Any CPU` → `Debug|x64`
- `*.Debug|Any CPU.Build.0 = Debug|Any CPU` → `Debug|x64`
- `*.Release|Any CPU.ActiveCfg = Release|Any CPU` → `Release|x64`
- `*.Release|Any CPU.Build.0 = Release|Any CPU` → `Release|x64`

### 验证结果

- x64 干净构建：✅ 通过
- Any CPU 干净构建：✅ 通过

### 发现的问题

干净构建时 `PresentationBuildTasks` 的 `net472` TFM 不会被 sln 级构建自动触发。当前 `Directory.Build.props` 中 `WpfNativePlatform` 已将 `Any CPU` → `x64` 规范化，但 `.targets` 中的 `_PresentationBuildTasksTfm` 在 .NET Framework MSBuild 下期望 `net472`。需要确保 `PresentationBuildTasks` 先于依赖它的项目构建，并且 `net472` TFM 被产出。

当前的 workaround：在 sln 级干净构建前，先单独构建 `PresentationBuildTasks` 的 `net472` TFM：
```
msbuild ...PresentationBuildTasks.csproj /p:Platform=x64 /p:TargetFramework=net472
```