# PresentationBuildTasks Bootstrap 机制

## 目标

`PresentationBuildTasks.dll` 是 WPF XAML 标记编译所需的 MSBuild 任务程序集。仓库重组过程中，内部标记编译项目会在 `MarkupCompilePass1`、`MarkupCompilePass2` 或资源生成阶段加载该任务程序集。

当仓库自产的 `PresentationBuildTasks.dll` 尚未生成时，标记编译无法继续。为避免重新引入 IDE 专用输出路径、复制 DLL 补产物或多路径猜测，构建链提供一个明确的 bootstrap 机制。

## 加载优先级

`Microsoft.WinFX.targets` 按以下顺序选择任务程序集：

1. 仓库自产 DLL：
   - `artifacts\bin\PresentationBuildTasks\$(WpfNativePlatform)\$(Configuration)\$(_PresentationBuildTasksTfm)\PresentationBuildTasks.dll`
2. bootstrap DLL：
   - 默认来自当前 MSBuild SDK resolver 选中的 WindowsDesktop SDK：
   - `$(MSBuildSDKsPath)Microsoft.NET.Sdk.WindowsDesktop\tools\$(_PresentationBuildTasksTfm)\PresentationBuildTasks.dll`
3. 如果两个路径都不存在，则构建失败并输出明确错误。

该机制不是无边界 fallback。只有仓库自产路径和明确的 bootstrap 路径两个来源。

## TFM 选择

`_PresentationBuildTasksTfm` 根据当前 MSBuild 宿主运行时确定：

- `$(MSBuildRuntimeType) == Core` 时使用 `net8.0`
- 其他情况使用 `net472`

这保证 .NET MSBuild 使用 `net8.0` 任务程序集，Visual Studio / .NET Framework MSBuild 使用 `net472` 任务程序集。

## 开关

默认启用 bootstrap：

```text
/p:UseBootstrapPresentationBuildTasks=true
```

可以关闭 bootstrap 来验证仓库是否已经具备完全自举能力：

```text
/p:UseBootstrapPresentationBuildTasks=false
```

关闭后，如果仓库自产 `PresentationBuildTasks.dll` 不存在，构建会直接失败。

## 显式指定 bootstrap DLL

如果当前 SDK 中没有匹配的 WindowsDesktop SDK tools，或需要使用指定版本的任务程序集，可以显式设置：

```text
/p:BootstrapPresentationBuildTasksAssembly=C:\path\to\PresentationBuildTasks.dll
```

显式路径必须与当前 MSBuild 宿主需要的 TFM 匹配。

## 构建输出提示

当实际使用 bootstrap DLL 时，构建输出会显示高重要性消息，说明使用的 bootstrap 路径以及仓库自产路径不存在。

这用于避免 bootstrap 长期掩盖 `PresentationBuildTasks.csproj` 自身构建失败。

## 设计约束

- 不使用 `artifacts\ide-bin`。
- 不通过 Copy 复制 DLL 到目标路径。
- 不搜索 GAC、Visual Studio 安装目录或历史构建产物。
- 不恢复 `$(Platform)`、无平台路径、IDE 路径等多级探测。
- `PresentationBuildTasks` 仍应作为仓库内项目逐步恢复自举构建能力。

## 与 native 二进制接入的区别

`PenImc`、`WpfGfx` 等 native 模块可使用外部二进制满足运行时或链接需求。`PresentationBuildTasks.dll` 是构建时任务程序集，会直接影响 XAML/BAML 和资源生成结果，因此 bootstrap 只作为过渡机制。

长期目标仍是优先使用仓库自产 `PresentationBuildTasks.dll`。
