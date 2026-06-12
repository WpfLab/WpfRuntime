# Windows Presentation Foundation (WPF)

这是重新组织的 WPF 仓库，方便打开 Sln 文件即可完成构建。

只包含托管（C#）部分，native 模块（`PenImc`、`WpfGfx` 等）通过 NuGet 二进制 DLL 接入，不在本仓库中做源码构建。详见 [Docs/04-NuGet-Binary.md](Docs/04-NuGet-Binary.md)。

## 快速开始

```bash
msbuild Microsoft.Dotnet.Wpf.sln -restore /p:Configuration=Debug /p:Platform=x64 /m:1
```

## 文档

完整文档见 [Docs/README.md](Docs/README.md)。