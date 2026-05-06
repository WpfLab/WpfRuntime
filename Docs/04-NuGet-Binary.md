WpfGFX 和 PenIMC 等 dll 都可以从 NuGet 找到，在开发机的路径是：

c:\Users\lindexi\.nuget\packages\microsoft.windowsdesktop.app.runtime.win-x86\ c:\Users\lindexi\.nuget\packages\microsoft.windowsdesktop.app.runtime.win-arm64\

可以尝试用 powershell 枚举路径了解详情

以上路径耦合了某个用户的路径，这是不正确的，应该采用 GeneratePathProperty 的方式，细节如下

获取 NuGet 包还原到的本地路径，如以下示例代码

```xml
<PackageReference Include="Lindexi.Package" Version="1.2.3" GeneratePathProperty="true"/>

<Warning Text="Lindexi.Package Path=$(PkgLindexi_Package)" />
```

输出警告内容大概如下

```
Lindexi.Package Path=C:\Users\lindexi\.nuget\packages\lindexi.package\1.2.3
```

具体写法是在需要获取 NuGet 包路径的 PackageReference 标记 `GeneratePathProperty="true"` 属性

获取时的格式是 `$(Pkg包名)` 包名需要将 `.` 替换为下划线