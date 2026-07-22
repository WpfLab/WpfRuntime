# Windows Presentation Foundation (WPF)

这是重新组织中的 WPF 仓库。当前根解决方案入口为 `Microsoft.Dotnet.Wpf.slnx`。

仓库同时包含 C# 与 C++/CLI 项目。`PenImc`、`WpfGfx` 等模块当前通过 WindowsDesktop runtime NuGet 二进制资产接入，而不是在当前树中迁入对应源码。项目清单、构建状态和验证边界见 [Docs/00-overview.md](Docs/00-overview.md)。

## 快速开始

```bash
msbuild Microsoft.Dotnet.Wpf.slnx -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /nr:false /v:minimal
```

该命令是当前构建入口，不代表任意工作区状态下必然成功；执行前请先阅读当前状态与安全约束。

## 文档

完整文档见 [Docs/README.md](Docs/README.md)。