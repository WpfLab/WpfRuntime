# PresentationBuildTasks 按需构建机制

## 适用范围

`PresentationBuildTasks.dll` 是仓库内部 XAML/BAML 与资源生成使用的 MSBuild 任务程序集。本文件只规范它的宿主匹配、定位、自举和锁定输出处理；仓库整体构建状态以 [00-overview.md](00-overview.md) 为准。

实现依据：

- [`Microsoft.WinFX.targets`](../src/Microsoft.DotNet.Wpf/src/PresentationBuildTasks/Microsoft.WinFX.targets)
- [`PresentationBuildTasks.csproj`](../src/Microsoft.DotNet.Wpf/src/PresentationBuildTasks/PresentationBuildTasks.csproj)
- 根 [`Directory.Build.targets`](../Directory.Build.targets)

## 宿主 TFM 与唯一 DLL 路径

`Microsoft.WinFX.targets` 按当前 MSBuild 宿主选择任务程序集 TFM：

- `$(MSBuildRuntimeType) == Core`：`net8.0`
- 其他宿主：`net472`

任务加载与项目输出共用唯一仓库自产路径：

`artifacts\bin\PresentationBuildTasks\$(WpfNativePlatform)\$(Configuration)\$(_PresentationBuildTasksTfm)\PresentationBuildTasks.dll`

不从 GAC、Visual Studio 安装目录、WindowsDesktop SDK tools、其他平台目录或历史输出中探测备用 DLL，也不使用 `artifacts\ide-bin` 或额外 Copy 路径。

## 缺失时的自举与失败行为

`BuildPresentationBuildTasksOnDemand` 未赋值时默认为 `true`。唯一 DLL 缺失且仓库内 `PresentationBuildTasks.csproj` 存在时，`Microsoft.WinFX.targets` 会在验证任务程序集之前嵌套执行该项目的 `Build`，并传入：

- 当前 `Configuration`
- `Platform=$(WpfNativePlatform)`
- `TargetFramework=$(_PresentationBuildTasksTfm)`
- `BuildPresentationBuildTasksOnDemand=false`

最后一项阻止嵌套构建再次触发同一自举链。嵌套构建失败会直接传播失败；项目不存在、按需构建关闭或构建后唯一 DLL 仍不存在时，`ValidatePresentationBuildTasksAssembly` 会在标记编译或主资源生成前报告明确错误，不静默回退到其他任务程序集。

## 开关职责

| 开关 | 默认/当前设置方式 | 职责 | 不负责 |
|---|---|---|---|
| `BuildPresentationBuildTasksOnDemand` | 在仓库 `Microsoft.WinFX.targets` 中默认为 `true` | 唯一 DLL 缺失时构建仓库内 `PresentationBuildTasks.csproj`；嵌套构建时传入 `false` 防止递归 | 不改变 DLL 路径，不选择 SDK 或外部任务程序集 |
| `ImportFrameworkWinFXTargets` | 通常未设置；WpfDemo 的仓库消费模式将其设为 `true` | 这是反向开关：设为 `true` 时，阻止 WindowsDesktop SDK 自动导入 SDK 自带的 `Microsoft.WinFX.targets`，以便项目显式导入仓库版本 | 不会自行导入仓库 targets，也不构建或定位 `PresentationBuildTasks.dll` |
| `UsePrebuiltPresentationBuildTasks` | 默认未启用；Builder 在预先生成任务 DLL 后构建其他项目时设为 `true` | 根 `Directory.Build.targets` 据此移除产品项目到 `PresentationBuildTasks.csproj` 的 `ProjectReference`，避免每个项目通过项目引用重复调度任务项目 | 不代表使用外部 DLL，不改变唯一加载路径，也不替代缺失 DLL 校验；若同时关闭按需构建而 DLL 不存在，构建会失败 |

## 锁定输出处理

`PresentationBuildTasks.csproj` 在 `CoreCompile` 前扫描当前 `OutDir` 及其子目录中的 DLL：

1. 先尝试删除旧 DLL。
2. 删除因 `IOException` 或 `UnauthorizedAccessException` 失败时，将原文件改名为 `<name>.locked.<process-id>.<guid>.dll`。
3. 该范围也覆盖语言资源子目录中的卫星 DLL；后续编译仍写入同一个规范输出目录。

该策略不终止 Visual Studio 或 MSBuild 进程，也没有 IDE 专用输出分支。若改名本身失败且不属于文件或目录已消失，错误会继续暴露，不能以跳过清理掩盖问题。

## 验证边界

- 锁定 DLL 的删除失败后改名逻辑已存在于项目文件；Visual Studio 内完整构建是否不再受任务 DLL 锁影响，待 Visual Studio 验证。
- WpfDemo F5 是否在设计时构建、增量构建和调试启动全过程稳定使用仓库 targets 与唯一任务 DLL，待 Visual Studio 验证。
