# WpfDemo 仓库 WPF 消费实现设计

## 文档定位

本文记录 `Demo/WpfDemo` 已落地的实现结构、MSBuild 数据流和后续扩展约束。实施顺序、验收标准和风险矩阵见 [06-wpfdemo-build-run-plan.md](06-wpfdemo-build-run-plan.md)。

本文中的 XML 仍用于解释设计意图；实际状态以 `Demo/WpfDemo`、`eng/WpfDemo` 和 `eng/WpfRuntimeDependencies.props` 中的源文件为准。

## 当前实施状态

- Debug|x64 消费链路已实现并通过干净构建、增量构建、自动 ref 新 API、XAML、deps/runtimeconfig、静态闭包和真实启动验证。
- Builder 与 WpfDemo 已共用 `eng/WpfRuntimeDependencies.props` 中的 runtime 版本、包和资产清单。
- `WpfRuntimeProbe` 已实现托管与 native app-local 来源断言，并支持 `--verify-repo-wpf` 自动退出模式。
- Visual Studio F5 尚待人工验收；x86/arm64 尚未实现。

## 目标构建模型

```text
WpfDemo
├─ 构建依赖：PresentationFramework.csproj (ReferenceOutputAssembly=false)
│  ├─ PresentationCore.csproj
│  │  ├─ WindowsBase.csproj
│  │  ├─ System.Xaml.csproj
│  │  ├─ System.Windows.Input.Manipulations.csproj
│  │  ├─ UIAutomationTypes.csproj
│  │  ├─ UIAutomationProvider.csproj
│  │  ├─ DirectWriteForwarder.vcxproj
│  │  └─ PresentationBuildTasks.csproj
│  ├─ ReachFramework.csproj
│  ├─ System.Printing-ref.csproj
│  └─ cycle-breakers
├─ 编译/XAML 引用：artifacts/obj/<project>/x64/Debug/net8.0/ref/*.dll
├─ 托管运行时：artifacts/bin/<project>/x64/Debug/net8.0/*.dll
├─ C++/CLI：DirectWriteForwarder.dll + ijwhost.dll
└─ native：Microsoft.WindowsDesktop.App.Runtime.win-x64/runtimes/win-x64/native/*.dll
```

核心原则：

```text
ProjectReference = 构建顺序
obj/.../ref       = C# 与 XAML 编译契约
bin/...           = 运行时实现
```

## 当前文件布局与后续扩展

### WpfDemo 目录

```text
Demo/WpfDemo/
├─ WpfDemo.csproj
├─ Directory.Build.props
├─ Directory.Build.targets
├─ App.xaml
├─ App.xaml.cs
├─ MainWindow.xaml
├─ MainWindow.xaml.cs
├─ Diagnostics/
│  └─ WpfRuntimeProbe.cs
└─ Resources/
   └─ Resource1.resx
```

删除：

```text
Demo/WpfDemo/global.json
```

### 已新增共享构建文件

为避免把几十个程序集、包和路径都堆在 WpfDemo.csproj 中，当前已新增：

```text
eng/WpfDemo/RepoWpfConsumer.props
eng/WpfDemo/RepoWpfConsumer.targets
eng/WpfRuntimeDependencies.props
```

职责划分：

- `RepoWpfConsumer.props`
  - 平台、TFM、运行时包版本和项目根路径。
  - 运行时 NuGet PackageReference/PackageDownload。
  - `ImportFrameworkWinFXTargets` 等评估期属性。
- `RepoWpfConsumer.targets`
  - 顶层构建依赖和隐式 WPF FrameworkReference 移除。
  - 引用替换。
  - ref/implementation/native 资产收集。
  - deps.json 登记和复制。
  - 构建前后校验。
  - 诊断输出。

`RepoWpfConsumer.props/targets` 负责消费管线，`WpfRuntimeDependencies.props` 由 Builder 与 WpfDemo 共用，避免版本和资产清单分叉。

## Directory.Build.props 继承方式

`Demo/WpfDemo/Directory.Build.props` 已显式导入根 props，再添加 Demo consumer 配置：

```xml
<Project>
	<Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)..\'))" />

  <Import Project="$(RepoRoot)eng\WpfDemo\RepoWpfConsumer.props"
		  Condition="Exists('$(RepoRoot)eng\WpfDemo\RepoWpfConsumer.props')" />
</Project>
```

注意事项：

- `GetPathOfFileAbove` 的起始目录必须是 WpfDemo 目录的父级，避免再次找到自身。
- 起始值显式以 `..\` 结尾，表示父目录路径，而不是依赖字符串拼接后的隐式规范化。
- 根配置已提供 `RepoRoot`、`ArtifactsDir` 和 `ArtifactsBinDir`；实测分别求值为仓库根、`artifacts/` 和 `artifacts/bin/`。实现中统一使用这些属性，不再保留 WpfDemo 当前自定义的 `ArtifactsPath` 别名。
- 必须确认 Arcade SDK props 只导入一次。

`Directory.Build.targets` 同理：

```xml
<Project>
	<Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.targets', '$(MSBuildThisFileDirectory)..\'))" />
  <Import Project="$(RepoRoot)eng\WpfDemo\RepoWpfConsumer.targets"
		  Condition="Exists('$(RepoRoot)eng\WpfDemo\RepoWpfConsumer.targets')" />
</Project>
```

也可删除 Demo 目录级 props/targets，让 MSBuild 直接发现根文件，再从 WpfDemo.csproj 显式导入 consumer props/targets。选择标准是避免双重导入和保持 Visual Studio 设计时构建稳定。

## WpfDemo.csproj 建议结构

项目仍使用 WPF SDK 的默认 XAML item glob，但不使用 SDK 提供的 WPF 框架程序集：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
	<OutputType>WinExe</OutputType>
	<TargetFramework>net8.0-windows</TargetFramework>
	<UseWPF>true</UseWPF>
	<Nullable>enable</Nullable>
	<ImplicitUsings>enable</ImplicitUsings>
	<Platforms>x64</Platforms>
	<PlatformTarget>x64</PlatformTarget>
  </PropertyGroup>

  <PropertyGroup>
	<GenerateResxSource>false</GenerateResxSource>
  </PropertyGroup>
</Project>
```

这里存在两个有意不同的 TFM：

- WpfDemo 是 Windows 应用宿主，使用 `net8.0-windows`，以启用 SDK 的 WPF XAML item、应用入口和 Windows 平台分析。
- 仓库中的 WPF 实现项目实际使用 `net8.0`，因此其自动 ref 和实现输出目录均为 `net8.0`，不是 `net8.0-windows`。

`RepoWpfTargetFramework=net8.0` 只用于定位仓库 WPF 的 `artifacts/obj` 与 `artifacts/bin` 产物，不会改变 WpfDemo 自身的目标框架。

消费逻辑放在 props/targets 中，避免项目文件被基础设施细节淹没。

## RepoWpfConsumer.props 设计

### 基础属性

```xml
<PropertyGroup>
  <RepoWpfConsumerEnabled Condition="'$(RepoWpfConsumerEnabled)' == ''">true</RepoWpfConsumerEnabled>
  <RepoWpfTargetFramework>net8.0</RepoWpfTargetFramework>
  <RepoWpfConfiguration Condition="'$(RepoWpfConfiguration)' == ''">$(Configuration)</RepoWpfConfiguration>
  <RepoWpfPlatform Condition="'$(RepoWpfPlatform)' == ''">$(WpfNativePlatform)</RepoWpfPlatform>
	<ImportFrameworkWinFXTargets Condition="'$(RepoWpfConsumerEnabled)' == 'true'">true</ImportFrameworkWinFXTargets>
</PropertyGroup>
```

`ImportFrameworkWinFXTargets` 的名称容易产生反向理解。它不是“导入 SDK WinFX targets”的开关；SDK 文件 `Microsoft.NET.Sdk.WindowsDesktop.targets` 使用的条件是：

```xml
<Import Project="Microsoft.WinFX.targets"
		Condition="'$(ImportFrameworkWinFXTargets)' != 'true'" />
```

因此：

- 未设置或设为 `false`：SDK 导入自己的 `Microsoft.WinFX.targets`。
- 设为 `true`：SDK 跳过该默认导入，项目随后必须显式导入仓库的 `PresentationBuildTasks/Microsoft.WinFX.targets`。
- 因此该属性只在 `RepoWpfConsumerEnabled=true` 时设置；关闭 repo-WPF 模式后应保持为空，让 SDK 恢复默认导入。

### 平台保护（定义在 RepoWpfConsumer.targets）

首期只支持 x64。以下 Target 属于 `RepoWpfConsumer.targets`，不要放入早期求值的 `.props`：

```xml
<Target Name="ValidateRepoWpfConsumerPlatform"
		BeforeTargets="PrepareForBuild"
		Condition="'$(RepoWpfConsumerEnabled)' == 'true'">
  <Error Condition="'$(RepoWpfPlatform)' != 'x64'"
		 Text="WpfDemo repository-WPF mode currently supports x64 only. Actual platform: $(RepoWpfPlatform). Build with /p:Platform=x64 or set RepoWpfPlatform=x64." />
</Target>
```

### 顶层构建依赖

该 ItemGroup 必须在 `Microsoft.NET.Sdk.WindowsDesktop.props` 已经根据 `UseWPF=true` 加入隐式 FrameworkReference 之后求值。实测若把 `FrameworkReference Remove` 放在 `Directory.Build.props` 或过早导入的 consumer props 中，WindowsDesktop props 会在后续重新加入它。

推荐把以下 ItemGroup 放在 `WpfDemo.csproj` 的主体中，或放在确定晚于 WindowsDesktop props 求值的导入文件中：

```xml
<ItemGroup Condition="'$(RepoWpfConsumerEnabled)' == 'true'">
  <FrameworkReference Remove="Microsoft.WindowsDesktop.App.WPF" />

  <ProjectReference Include="$(WpfSourceDir)PresentationFramework\PresentationFramework.csproj"
					Targets="Restore;Build"
					ReferenceOutputAssembly="false" />
</ItemGroup>
```

`ReferenceOutputAssembly=false` 是必须项。若省略，ProjectReference 解析结果与后续手动注入的 ref 输出会同时进入 XAML MetadataLoadContext，可能产生：

```text
The assembly 'System.Xaml ...' has already been loaded into this MetadataLoadContext.
```

这里保留 `Targets="Restore;Build"` 是针对当前仓库已观察到的 Visual Studio 项目级构建问题：部分上游 ref 项目若只执行 Build，可能缺少 `project.assets.json`。命令行入口仍应使用顶层 `msbuild ... -restore`；待 IDE restore 顺序彻底收敛后，可评估将该引用缩减为 `Targets="Build"`，但不能在未复验 Visual Studio F5 前提前移除 Restore。

### 运行时包

版本应从共享属性读取，不在 WpfDemo 中散落硬编码。当前 Builder 的已验证集合为：

- `System*Version` 属性来自根构建导入的 `eng/Versions.props`。
- `RepoWpfWindowsDesktopRuntimeVersion` 建议新增到共享 props，并同时由 `eng/Builder/Builder.csproj` 与 WpfDemo 使用；迁移前的已验证值为 `8.0.6`。
- 如果任一版本属性为空，consumer props 应在 restore 前报错，而不是让 NuGet 产生难以定位的版本解析错误。

```xml
<Target Name="ValidateRepoWpfConsumerVersions"
		BeforeTargets="Restore;PrepareForBuild"
		Condition="'$(RepoWpfConsumerEnabled)' == 'true'">
  <Error Condition="'$(RepoWpfWindowsDesktopRuntimeVersion)' == ''"
		 Text="RepoWpfWindowsDesktopRuntimeVersion is required. Define it in the shared WPF runtime versions props used by Builder and WpfDemo." />
</Target>

<ItemGroup Condition="'$(RepoWpfConsumerEnabled)' == 'true'">
  <PackageDownload Include="Microsoft.WindowsDesktop.App.Runtime.win-x64"
				   Version="[$(RepoWpfWindowsDesktopRuntimeVersion)]" />

  <PackageReference Include="System.Configuration.ConfigurationManager"
					Version="$(SystemConfigurationConfigurationManagerPackageVersion)" />
  <PackageReference Include="System.Diagnostics.EventLog"
					Version="$(SystemDiagnosticsEventLogPackageVersion)" />
  <PackageReference Include="System.DirectoryServices"
					Version="$(SystemDirectoryServicesVersion)" />
  <PackageReference Include="System.Drawing.Common"
					Version="$(SystemDrawingCommonVersion)" />
  <PackageReference Include="System.Formats.Nrbf"
					Version="$(SystemFormatsNrbfVersion)" />
  <PackageReference Include="System.IO.Packaging"
					Version="$(SystemIOPackagingVersion)" />
  <PackageReference Include="System.Resources.Extensions"
					Version="$(SystemResourcesExtensionsVersion)" />
  <PackageReference Include="System.Security.Cryptography.Xml"
					Version="$(SystemSecurityCryptographyXmlPackageVersion)" />
  <PackageReference Include="System.Security.Permissions"
					Version="$(SystemSecurityPermissionsPackageVersion)" />
  <PackageReference Include="System.Windows.Extensions"
					Version="$(SystemWindowsExtensionsPackageVersion)" />
</ItemGroup>
```

该校验覆盖推荐的 `msbuild ... -restore`、常规 Build 和 CI 构建入口；Visual Studio 的实际 restore/F5 路径必须列入实施验证。如果发现某个设计时 restore 入口没有执行该 Target，还应给 `PackageDownload` 增加非空版本 Condition，并在 IDE 验证中将空属性视为失败，不能依赖 NuGet 的默认错误信息。

`System.Drawing.Common` 当前仓库版本是预发行字符串，实施时必须直接复用 `eng/Versions.props`，不要假定所有包都为 `8.0.0`。

## 仓库 PresentationBuildTasks 接入

SDK 的 WindowsDesktop targets 默认会导入 SDK 自带 `Microsoft.WinFX.targets`。根据其 `Condition="'$(ImportFrameworkWinFXTargets)' != 'true'"`，WpfDemo 要先设置：

```xml
<ImportFrameworkWinFXTargets>true</ImportFrameworkWinFXTargets>
```

然后在 SDK targets 已导入后加入仓库版本：

```xml
<Import Project="$(WpfSourceDir)PresentationBuildTasks\Microsoft.WinFX.targets"
		Condition="'$(RepoWpfConsumerEnabled)' == 'true'" />
```

若通过 SDK 风格简写无法保证导入位置，可将 WpfDemo 改成显式导入形式：

```xml
<Project>
  <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />
  <!-- 属性、ItemGroup -->
  <Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" />
  <Import Project="$(WpfSourceDir)PresentationBuildTasks\Microsoft.WinFX.targets" />
</Project>
```

隔离原型已验证：仓库 PresentationBuildTasks + 自动 ref 编译 + app-local implementation 可以成功构建并启动。

## 编译引用替换

### 受管程序集清单

首期核心清单：

```xml
<ItemGroup>
  <RepoWpfCoreAssembly Include="WindowsBase" />
  <RepoWpfCoreAssembly Include="System.Xaml" />
  <RepoWpfCoreAssembly Include="System.Windows.Input.Manipulations" />
  <RepoWpfCoreAssembly Include="UIAutomationTypes" />
  <RepoWpfCoreAssembly Include="UIAutomationProvider" />
  <RepoWpfCoreAssembly Include="PresentationCore" />
  <RepoWpfCoreAssembly Include="PresentationFramework" />
</ItemGroup>
```

根据 Demo 功能扩展清单：

```text
ReachFramework
PresentationUI
System.Windows.Presentation
System.Windows.Controls.Ribbon
WindowsFormsIntegration
UIAutomationClient
UIAutomationClientSideProviders
PresentationFramework.Aero/Aero2/AeroLite/Classic/Fluent/Luna/Royale
```

### 自动 ref 路径

```text
$(ArtifactsDir)obj\%(RepoWpfCoreAssembly.Identity)\$(RepoWpfPlatform)\$(RepoWpfConfiguration)\$(RepoWpfTargetFramework)\ref\%(RepoWpfCoreAssembly.Identity).dll
```

不能误写成 `$(ArtifactsBinDir)obj\...`；`ArtifactsBinDir` 已包含 `artifacts/bin`。

### 替换 target

建议在 `ResolveReferences` 后执行：

```xml
<Target Name="UseRepoWpfCompileReferences"
		AfterTargets="ResolveReferences"
		Condition="'$(RepoWpfConsumerEnabled)' == 'true'">
  <ItemGroup>
	<_RepoWpfCompileReference
		Include="$(ArtifactsDir)obj\%(RepoWpfCoreAssembly.Identity)\$(RepoWpfPlatform)\$(RepoWpfConfiguration)\$(RepoWpfTargetFramework)\ref\%(RepoWpfCoreAssembly.Identity).dll" />
  </ItemGroup>

  <Error Condition="!Exists('%(_RepoWpfCompileReference.Identity)')"
		 Text="Repository WPF reference assembly is missing: %(_RepoWpfCompileReference.Identity). Verify RepoWpfPlatform=$(RepoWpfPlatform), RepoWpfConfiguration=$(RepoWpfConfiguration), RepoWpfTargetFramework=$(RepoWpfTargetFramework), and confirm the owning implementation project generated its automatic ref output." />

  <ItemGroup>
	<ReferencePath Remove="@(ReferencePath)"
				   Condition="'%(ReferencePath.Filename)' == 'WindowsBase' Or
							  '%(ReferencePath.Filename)' == 'System.Xaml' Or
							  '%(ReferencePath.Filename)' == 'System.Windows.Input.Manipulations' Or
							  '%(ReferencePath.Filename)' == 'UIAutomationTypes' Or
							  '%(ReferencePath.Filename)' == 'UIAutomationProvider' Or
							  '%(ReferencePath.Filename)' == 'PresentationCore' Or
							  '%(ReferencePath.Filename)' == 'PresentationFramework'" />
	<ReferencePathWithRefAssemblies Remove="@(ReferencePathWithRefAssemblies)"
				   Condition="'%(ReferencePathWithRefAssemblies.Filename)' == 'WindowsBase' Or
							  '%(ReferencePathWithRefAssemblies.Filename)' == 'System.Xaml' Or
							  '%(ReferencePathWithRefAssemblies.Filename)' == 'System.Windows.Input.Manipulations' Or
							  '%(ReferencePathWithRefAssemblies.Filename)' == 'UIAutomationTypes' Or
							  '%(ReferencePathWithRefAssemblies.Filename)' == 'UIAutomationProvider' Or
							  '%(ReferencePathWithRefAssemblies.Filename)' == 'PresentationCore' Or
							  '%(ReferencePathWithRefAssemblies.Filename)' == 'PresentationFramework'" />
	<ReferenceDependencyPaths Remove="@(ReferenceDependencyPaths)"
				   Condition="'%(ReferenceDependencyPaths.Filename)' == 'WindowsBase' Or
							  '%(ReferenceDependencyPaths.Filename)' == 'System.Xaml' Or
							  '%(ReferenceDependencyPaths.Filename)' == 'System.Windows.Input.Manipulations' Or
							  '%(ReferenceDependencyPaths.Filename)' == 'UIAutomationTypes' Or
							  '%(ReferenceDependencyPaths.Filename)' == 'UIAutomationProvider' Or
							  '%(ReferenceDependencyPaths.Filename)' == 'PresentationCore' Or
							  '%(ReferenceDependencyPaths.Filename)' == 'PresentationFramework'" />

	<ReferencePath Include="@(_RepoWpfCompileReference)"
				   Private="false"
				   IncludeRuntimeDependency="false" />
	<ReferencePathWithRefAssemblies Include="@(_RepoWpfCompileReference)"
				   Private="false"
				   IncludeRuntimeDependency="false" />
  </ItemGroup>
</Target>
```

上面的显式 `%(Filename)` 条件已经在当前仓库的 MSBuild 18/.NET 8 隔离原型中验证可用。正式实现可先沿用这份明确清单；若后续要减少重复，应先生成一个可预测的移除清单并新增构建测试，不能直接改成跨 item list 的 `AnyHaveMetadataValue` 表达式。

必须同时清理三类 item：

- `ReferencePath`
- `ReferencePathWithRefAssemblies`
- `ReferenceDependencyPaths`

否则可能同时加载实现、自动 ref、手工 ref 或 cycle-breaker 同名程序集。

## 托管运行时资产登记

### 运行时来源

首期可以从 `PresentationFramework` 的输出目录收集传递复制的实现闭包：

```text
artifacts/bin/PresentationFramework/x64/Debug/net8.0/
```

该目录当前已包含：

- `WindowsBase.dll`
- `System.Xaml.dll`
- `PresentationCore.dll`
- `PresentationFramework.dll`
- `PresentationUI.dll`
- `ReachFramework.dll`
- `System.Printing.dll`
- `System.Windows.Input.Manipulations.dll`
- `UIAutomationTypes.dll`
- `UIAutomationProvider.dll`
- `DirectWriteForwarder.dll`
- `ijwhost.dll`

长期更稳妥的方式是按 Builder 的 `AssemblyCollector` 清单逐项目取“项目自身主输出”，避免某个项目目录中的传递副本覆盖正确主输出。

### 登记到 deps.json

只执行 Copy 不够。必须把实现 DLL 作为用户运行时程序集参与 `GenerateDepsFile`：

```xml
<ItemGroup>
  <ReferenceDependencyPaths Include="@(_RepoWpfRuntimeAssembly)"
							CopyLocal="true"
							IncludeRuntimeDependency="true" />
  <ReferenceCopyLocalPaths Include="@(_RepoWpfRuntimeAssembly)"
						   CopyLocal="true" />
</ItemGroup>
```

已用最小探针验证：`IncludeRuntimeDependency=true` 会把 app-local `WindowsBase.dll` 写入 `.deps.json`，应用可从输出目录加载它。

### 本地化资源

当前 WPF 输出包含：

```text
cs, de, es, fr, it, ja, ko, pl, pt-BR, ru, tr, zh-Hans, zh-Hant
```

实现 target 应将每个程序集输出下的 `*.resources.dll` 保持文化目录结构复制到 WpfDemo 输出，否则本地化环境可能回退或运行时缺失资源。

## native 资产部署

### 官方 runtime 包

首期 x64 路径：

```text
$(NuGetPackageRoot)microsoft.windowsdesktop.app.runtime.win-x64\$(RepoWpfWindowsDesktopRuntimeVersion)\runtimes\win-x64\native\
```

至少包含：

```text
D3DCompiler_47_cor3.dll
PenImc_cor3.dll
PresentationNative_cor3.dll
vcruntime140_cor3.dll
wpfgfx_cor3.dll
```

### C++/CLI host

`DirectWriteForwarder.dll` 还需要：

```text
artifacts/bin/DirectWriteForwarder/x64/Debug/ijwhost.dll
```

注意现有输出文件名可能显示为 `Ijwhost.dll`，Windows 文件系统不区分大小写；文档和清单应统一为官方名称 `ijwhost.dll`。

### 复制时机

对普通 Build：

```xml
<Target Name="CopyRepoWpfRuntimeAssets"
		AfterTargets="Build"
		Condition="'$(RepoWpfConsumerEnabled)' == 'true' And '$(DesignTimeBuild)' != 'true'">
  <Copy SourceFiles="@(_RepoWpfNativeRuntime)"
		DestinationFolder="$(TargetDir)"
		SkipUnchangedFiles="true" />
</Target>
```

若后续支持 Publish，还要对 `PublishDir` 增加对应 target，并验证自包含发布。

## 运行时 NuGet 依赖

设置顶层 ProjectReference 为 `ReferenceOutputAssembly=false` 后，其 PackageReference 不会自然成为 WpfDemo 的运行时依赖。已验证在未显式加入包时，应用启动首错为：

```text
FileNotFoundException: System.IO.Packaging, Version=8.0.0.0
```

因此 WpfDemo 必须显式引用 Builder 的 runtime package dependency 清单。建议将 `NuGetPackageService.ReadRuntimePackageDependencies` 对应列表抽取为一个共享 props，例如：

```text
eng/WpfRuntimeDependencies.props
```

Builder 读取该文件，WpfDemo 直接导入该文件。这样版本和包 ID 只有一个事实来源。

## 解决方案平台映射

当前 `Microsoft.Dotnet.Wpf.slnx` 对多数 WPF 项目将 `Any CPU` 映射到 x64，但 WpfDemo 没有映射。

建议：

```xml
<Project Path="Demo/WpfDemo/WpfDemo.csproj">
  <Platform Solution="*|Any CPU" Project="x64" />
  <Platform Solution="*|x64" Project="x64" />
</Project>
```

首期对 x86/arm64 不配置 Build 映射，避免错误地将 x64 native 资产用于其他架构。

## 加载来源探针

建议新增 `Diagnostics/WpfRuntimeProbe.cs`：

```csharp
internal static class WpfRuntimeProbe
{
	internal static WpfAssemblyInfo[] Capture()
	{
		return new[]
		{
			CaptureAssembly(typeof(DependencyObject).Assembly),
			CaptureAssembly(typeof(Visual).Assembly),
			CaptureAssembly(typeof(Application).Assembly),
		};
	}

	private static WpfAssemblyInfo CaptureAssembly(Assembly assembly)
	{
		AssemblyName name = assembly.GetName();
		return new WpfAssemblyInfo(name.Name ?? string.Empty, name.Version, assembly.Location);
	}
}

internal sealed record WpfAssemblyInfo(string Name, Version? Version, string Location);
```

实际实现应验证：

- `assembly.Location` 位于 `AppContext.BaseDirectory`。
- 目标框架是 `.NETCoreApp,Version=v8.0`。
- 程序集版本符合仓库版本。
- 进程架构与 WpfDemo 平台一致。

还应检查 native 模块：

- `DirectWriteForwarder.dll`
- `PenImc_cor3.dll`
- `PresentationNative_cor3.dll`
- `wpfgfx_cor3.dll`

可复用 `eng/Builder/PackageTestApp/MainWindow.xaml.cs` 的 `ValidateWpfAssembly` 逻辑，但不应复制后长期分叉；优先抽取共享源码或小型共享项目。

## 新增 API 验证机制

为了防止以后误切回手工 ref 或系统 WPF，建议增加一个只在仓库中存在的公开探针 API，或在测试中临时生成 API 后验证。

更低侵入的自动测试流程：

1. 读取 `PresentationCore.dll` 自动 ref 输出哈希。
2. 在测试分支为 PresentationCore 添加一个临时 public API。
3. 构建 WpfDemo。
4. 编译 WpfDemo 中对该 API 的调用。
5. 验证自动 ref 输出更新。
6. 启动 WpfDemo 并调用该 API。
7. 回退临时测试改动。

正式仓库不必长期保留无业务意义的公开 API；但 CI 应至少有一个机制验证“实现项目新增 API可传到 WpfDemo”。

## 构建诊断 target

建议增加高重要性输出：

```text
Repo WPF compile reference: <path>
Repo WPF runtime assembly: <path>
Repo WPF native runtime: <path>
Repo PresentationBuildTasks: <path>
```

并在构建结束检查：

- `WpfDemo.runtimeconfig.json` 不含 `Microsoft.WindowsDesktop.App`。
- `WpfDemo.deps.json` 含核心 WPF 实现 DLL。
- 输出目录存在 required DLL。
- 没有同名 DLL来自多个架构或多个目录。

## 推荐命令

### 构建

```powershell
msbuild Demo\WpfDemo\WpfDemo.csproj -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /nr:false /v:minimal
```

### 查看关键求值

```powershell
msbuild Demo\WpfDemo\WpfDemo.csproj -nologo /p:Configuration=Debug /p:Platform=x64 -getProperty:NETCoreSdkVersion,WpfSourceDir,WpfNativePlatform,TargetPath,_PresentationBuildTasksAssembly
```

### 启动

```powershell
artifacts\bin\WpfDemo\x64\Debug\net8.0-windows\WpfDemo.exe
```

实际输出路径应以最终 `OutputPath` 求值为准，避免在脚本中重复拼接。

## 自动化测试建议

### 构建测试

- 干净构建 WpfDemo。
- 连续构建两次，第二次应为稳定增量构建。
- 修改 PresentationCore 单个源文件后只构建 WpfDemo。
- 验证 PresentationCore bin/ref 时间戳或哈希更新。

### 静态产物测试

- 解析 runtimeconfig，断言没有 `Microsoft.WindowsDesktop.App`。
- 解析 deps.json，断言有核心 WPF runtime 条目。
- 校验 required managed/native 文件存在。
- 检测相同文件名是否出现多个候选源。

### 运行测试

- 启动窗口并等待 Dispatcher 到 ApplicationIdle。
- 创建基础 XAML 控件并触发 RoutedEvent。
- 输出加载来源。
- 30 秒超时。
- 任一模块来自共享框架则失败。

### Visual Studio 测试

- 打开 `Microsoft.Dotnet.Wpf.slnx`。
- 选择 Debug/x64 或 Any CPU 映射后的 x64。
- 将 WpfDemo 设为启动项目。
- F5。
- 修改 PresentationCore 后再次 F5，确认依赖重建和断点命中。

## 实施时不要采用的方案

### 不要只添加 ProjectReference

原因：XAML 已验证会触发 `LostFocusEventManager` 已知类型错误，且可能混入多个同名程序集。

### 不要只复制实现 DLL

原因：如果 runtimeconfig 仍声明 WindowsDesktop，共享框架优先；如果移除共享框架但 deps.json 未登记，宿主会报找不到程序集。

### 不要以手工 ref 项目作为新增 API唯一来源

原因：实现项目新增 API 不会自动同步到手工 ref 源文件。

### 不要从 PresentationFramework 输出目录无筛选复制所有文件作为长期方案

原因：其中包含传递副本，可能掩盖项目主输出或复制错误版本。原型可以使用，正式实现应沿用 Builder 的“每个项目只收集自身主程序集”规则。

### 不要首期同时支持 Any CPU、x86、x64、arm64

原因：DirectWriteForwarder、ijwhost 和 native WPF DLL 都是架构相关资产。先保证 x64 契约正确，再参数化扩展。

## 与 Builder 的后续收敛

WpfDemo 和 `DotNetCampus.WpfLib` 包测试本质上是同一消费问题的两个入口：

- WpfDemo：直接消费当前工作树输出，优化本地开发迭代。
- PackageTestApp：消费打包后的资产，验证分发契约。

建议共享以下定义：

- managed runtime assembly names
- compile reference assembly names
- native required file names
- runtime PackageReference IDs/versions
- inbox WPF assembly names
- assembly-load probe

最终应只有“资产来源”不同：

```text
WpfDemo        -> artifacts/obj + artifacts/bin
PackageTestApp -> nupkg/ref + nupkg/runtimes
```

其余引用替换、deps 登记和加载验证应尽量一致。
