# Native NuGet 二进制接入历史方案摘要

> 历史材料，不代表当前状态。

## 旧方案

早期提案计划用 `Microsoft.WindowsDesktop.App.Runtime` 系列包替代 `PenImc`、`WpfGfx` 等 native 模块的源码迁移，并建议在 `PackageReference` 上启用 `GeneratePathProperty`，再通过生成的 `Pkg*` 属性定位 NuGet 缓存中的 native DLL。

该提案只描述了候选包和路径获取方式，没有形成当前的共享资产定义、Builder 打包流程或 WpfDemo 消费验证。

## 已被当前实现取代的部分

当前实现不使用 `GeneratePathProperty`：

- `eng/WpfRuntimeDependencies.props` 统一定义 runtime 版本、托管程序集、运行时包和 native 文件清单；WindowsDesktop runtime 版本当前为 `8.0.6`。
- `eng/Builder/Builder.csproj` 使用 `PackageDownload` 还原 win-x64、win-x86、参考包和 host 包，并通过 `$(NuGetPackageRoot)` 写出包路径供 Builder 使用。
- Builder 收集 x64/x86 的 RID 托管实现、WindowsDesktop native DLL 和 `ijwhost.dll`，生成带 `buildTransitive` targets 的 `DotNetCampus.WpfLib` 包。
- `eng/WpfDemo/RepoWpfConsumer.props` 与 `eng/WpfDemo/RepoWpfConsumer.targets` 使用共享定义和 `$(NuGetPackageRoot)`，负责 WpfDemo 的引用替换、资产复制与输出校验。

x64 和 x86 已在 Builder 路径中实现；arm64 尚未实现。WpfDemo 当前实现范围与 Builder 不完全相同，具体边界应以当前专题文档和源文件为准。

## 当前事实入口

- Builder 构建、打包与包验证：[../05-builder-plan.md](../05-builder-plan.md)
- WpfDemo 消费实现：[../07-wpfdemo-implementation.md](../07-wpfdemo-implementation.md)
- 仓库整体状态：[../00-overview.md](../00-overview.md)