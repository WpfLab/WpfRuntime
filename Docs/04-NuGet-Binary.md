# Native 模块的 NuGet 二进制接入方案

## 目标

`PenImc` 和 `WpfGfx` 等 native 模块不再走源码迁移路线，而是通过 NuGet 获取已构建好的 DLL，用于满足 `DllImport` 依赖和主链编译/运行需求。

## NuGet 包来源

这些 DLL 可以从 `Microsoft.WindowsDesktop.App.Runtime` 系列 NuGet 包找到：

- `Microsoft.WindowsDesktop.App.Runtime.win-x64`
- `Microsoft.WindowsDesktop.App.Runtime.win-x86`
- `Microsoft.WindowsDesktop.App.Runtime.win-arm64`

在开发机上的典型还原路径：

```
C:\Users\{user}\.nuget\packages\microsoft.windowsdesktop.app.runtime.win-x64\{version}\runtimes\win-x64\native\
C:\Users\{user}\.nuget\packages\microsoft.windowsdesktop.app.runtime.win-arm64\{version}\runtimes\win-arm64\native\
```

可以尝试用 PowerShell 枚举路径了解具体 DLL 清单：

```powershell
Get-ChildItem $env:USERPROFILE\.nuget\packages\microsoft.windowsdesktop.app.runtime.win-x64 -Recurse -Filter wpfgfx*.dll
Get-ChildItem $env:USERPROFILE\.nuget\packages\microsoft.windowsdesktop.app.runtime.win-x64 -Recurse -Filter PenImc*.dll
```

> 注意：以上路径耦合了特定用户的路径，不应硬编码到项目文件中。

## 推荐接入方式：GeneratePathProperty

在需要获取 NuGet 包路径的 `PackageReference` 上标记 `GeneratePathProperty="true"`，让 MSBuild 自动生成包路径属性：

```xml
<PackageReference Include="Microsoft.WindowsDesktop.App.Runtime.win-x64"
                  Version="8.0.6"
                  GeneratePathProperty="true" />
```

之后可通过 `$(PkgMicrosoft_WindowsDesktop_App_Runtime_win_x64)` 获取包根目录路径（包名中的 `.` 替换为 `_`）。

示例：

```xml
<Warning Text="Runtime path: $(PkgMicrosoft_WindowsDesktop_App_Runtime_win_x64)\runtimes\win-x64\native\" />
```

输出警告内容大概如下：

```
Runtime path: C:\Users\lindexi\.nuget\packages\microsoft.windowsdesktop.app.runtime.win-x64\8.0.6\runtimes\win-x64\native\
```

## 后续工作

- 确认 `PenImc` 和 `WpfGfx` 在各个目标平台（x64/arm64）上需要的具体 DLL 清单
- 在 `Directory.Build.props` 或相关项目文件中添加 `GeneratePathProperty` 引用
- 将 native DLL 复制到托管项目输出目录或通过 `Reference`/`Content` 项接入构建