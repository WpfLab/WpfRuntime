# WPF 强名称身份

本仓库生成的程序集必须保持与 `dotnet/wpf` 原始仓库相同的强名称身份。程序集身份映射集中维护在 [`eng/WpfStrongName.props`](../eng/WpfStrongName.props)，不要将项目名单散落到 `Directory.Build.props` 或各个项目文件中。

## 密钥来源

仓库通过 `Microsoft.DotNet.Arcade.Sdk` 提供的公开 SNK 执行 public signing：

- `MicrosoftShared` 使用 `tools/snk/35MSSharedLib1024.snk`，公钥标记为 `31bf3856ad364e35`。
- `ECMA` 使用 `tools/snk/ECMA.snk`，公钥标记为 `b77a5c561934e089`。

公开 SNK 只包含完成 public signing 所需的公钥材料。正式发布构建仍由 Microsoft 内部签名系统完成最终签名；本仓库不保存 Microsoft 私钥。

## 身份映射

映射以 `origin/wpf/eng/WpfArcadeSdk/tools/ShippingProjects.props` 和 `Signing.props` 为基线：

- 原始 WPF `UseMicrosoftSharedKeyId` 名单中的程序集保持 `MicrosoftShared`。
- shipping/helper 项目中不在该名单内的程序集使用 `ECMA`。
- 当前解决方案内的 ECMA 项目名单维护在 `eng/WpfStrongName.props`，其中包括 `System.Xaml`、`System.Windows.Input.Manipulations`、`System.Windows.Controls.Ribbon` 和 `System.Windows.Presentation` 及其相应引用程序集。
- WPF 生成的 `_wpftmp` 临时项目按原项目名称选择相同密钥。

关键身份示例：

| 程序集 | StrongNameKeyId | PublicKeyToken |
| --- | --- | --- |
| `System.Xaml` | `ECMA` | `b77a5c561934e089` |
| `PresentationFramework` | `MicrosoftShared` | `31bf3856ad364e35` |
| `PresentationCore` | `MicrosoftShared` | `31bf3856ad364e35` |
| `WindowsBase` | `MicrosoftShared` | `31bf3856ad364e35` |

## 修改约束

- 新增或恢复 WPF 产品项目时，必须先对照 `origin/wpf` 确认其原始签名身份。
- 不允许因为构建冲突而任意统一为 ECMA 或 MicrosoftShared。
- 引用程序集与对应运行时程序集必须使用相同身份。
- 修改映射后应构建受影响的实现项目和 `-ref` 项目，并运行 Builder 测试。
- 若消费项目同时看到 inbox 和包内同名程序集，应修复引用替换逻辑，不能通过改变程序集身份掩盖重复引用。
