# PresentationBuildTasks 按需构建机制

## 目标

`PresentationBuildTasks.dll` 是 WPF XAML 标记编译所需的 MSBuild 任务程序集。仓库重组过程中，内部标记编译项目会在 `MarkupCompilePass1`、`MarkupCompilePass2` 或资源生成阶段加载该任务程序集。

当仓库自产的 `PresentationBuildTasks.dll` 尚未生成时，标记编译无法继续。构建链会尝试按需构建仓库内的 `PresentationBuildTasks.csproj`，生成匹配当前 MSBuild 宿主的任务程序集。

## 加载优先级

`Microsoft.WinFX.targets` 按以下顺序选择任务程序集：

1. 仓库自产 DLL：
   - `artifacts\bin\PresentationBuildTasks\$(WpfNativePlatform)\$(Configuration)\$(_PresentationBuildTasksTfm)\PresentationBuildTasks.dll`
2. 如果仓库自产 DLL 不存在、启用了按需构建且仓库内项目文件存在，则在首次标记编译前构建：
   - `src\Microsoft.DotNet.Wpf\src\PresentationBuildTasks\PresentationBuildTasks.csproj`
	 - 构建目标为 `Build`，构建属性包含 `TargetFramework=$(_PresentationBuildTasksTfm)` 和 `BuildPresentationBuildTasksOnDemand=false`
3. 如果上述路径都不存在，则构建失败并输出明确错误。

该机制不是无边界 fallback。只有仓库自产路径和仓库内 `PresentationBuildTasks.csproj` 按需构建两个来源。

## TFM 选择

`_PresentationBuildTasksTfm` 根据当前 MSBuild 宿主运行时确定：

- `$(MSBuildRuntimeType) == Core` 时使用 `net8.0`
- 其他情况使用 `net472`

这保证 .NET MSBuild 使用 `net8.0` 任务程序集，Visual Studio / .NET Framework MSBuild 使用 `net472` 任务程序集。

## 开关

默认启用按需构建：

```text
/p:BuildPresentationBuildTasksOnDemand=true
```

可以关闭按需构建来验证仓库是否已经具备完全自举能力，或避免嵌套构建：

```text
/p:BuildPresentationBuildTasksOnDemand=false
```

按需构建会把自身属性设置为 `false` 传给 `PresentationBuildTasks.csproj`，避免递归触发。

关闭按需构建后，如果仓库自产 `PresentationBuildTasks.dll` 不存在，构建会直接失败。

## 构建输出提示

当按需构建触发时，构建输出会显示高重要性消息，说明缺少的仓库自产路径、要构建的项目和目标框架。

## 设计约束

- 不使用 `artifacts\ide-bin`。
- 不通过 Copy 复制 DLL 到目标路径。
- 不搜索 GAC、Visual Studio 安装目录、WindowsDesktop SDK tools 或历史构建产物。
- 不恢复 `$(Platform)`、无平台路径、IDE 路径等多级探测。

## 与 native 二进制接入的区别

`PenImc`、`WpfGfx` 等 native 模块可使用外部二进制满足运行时或链接需求。`PresentationBuildTasks.dll` 是构建时任务程序集，会直接影响 XAML/BAML 和资源生成结果，因此不使用外部 SDK 任务程序集作为回退。

长期目标仍是优先使用仓库自产 `PresentationBuildTasks.dll`。
