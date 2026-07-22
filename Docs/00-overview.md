# 当前状态概览

> 审计基准：以当前工作区验证为准。该文件是当前状态的唯一事实源；后续动作见 [01-phase-plan.md](01-phase-plan.md)。

## 仓库与构建入口

| 项目 | 当前事实 |
|---|---|
| 根解决方案入口 | [`../Microsoft.Dotnet.Wpf.slnx`](../Microsoft.Dotnet.Wpf.slnx) |
| 根传统解决方案 | 根目录不存在同名传统解决方案文件；仓库其他模块内部可以存在独立 `.sln` |
| `origin/` | 当前非空；若后续发现为空，必须立即停止迁移 |
| `origin/src` 保护边界 | 被 `origin/.gitignore` 排除，不受外层 Git 状态保护；禁止使用 `git clean -xdf` |
| .NET SDK | `global.json` 指定 `8.0.101`，`rollForward` 为 `latestFeature` |
| MSBuild | 当前工作区验证使用 `18.7.8.30822` |

## 项目清单

排除 `origin/`、`artifacts/`、`bin/`、`obj/` 后，磁盘共有 **68** 个项目文件。

根 `slnx` 声明 **57** 个唯一项目，且 57 条声明路径当前均存在：

| 所在区域 | 数量 |
|---|---:|
| `src/` | 46 |
| `cycle-breakers/` | 8 |
| `Demo/` | 1 |
| `Docs/` | 1 |
| `eng/` | 1 |
| 合计 | 57 |

IDE 接口只枚举出与根 `slnx` 对应的 57 条项目路径，没有提供 `Loaded` 或 `LoadFailed` 状态。因此只能确认项目被枚举，实际加载成功状态 **待 Visual Studio 验证**，不得表述为 57 个项目均已加载。

### 未直接纳入根 `slnx` 的 11 个项目

| 分类 | 数量 | 当前定位 |
|---|---:|---|
| `System.Printing.vcxproj` | 1 | 真实实现缺口；当前尚未构建，也未纳入根 `slnx` |
| `Extensions` 下的项目 | 5 | 是否保留取决于目标版本和兼容范围，待确认 |
| `OSVersionHelper.vcxproj` | 1 | 可能已由 binary 方案替代，待确认 |
| `eng/Builder/PackageTestApp/PackageTestApp.csproj` | 1 | Builder 使用的模板项目，不等同于待迁入主链实现 |
| `ThemeGenerator.proj` | 2 | 生成工具项目 |
| `wpf-etw.proj` | 1 | 生成工具项目 |
| 合计 | 11 | 不能把全部 11 个项目统一视为遗漏的主链实现 |

## 当前构建状态

以下结果来自当前工作区验证，但没有持久化独立日志文件；需要长期引用时应重新执行并保存日志。

| 范围 | 配置 | 结果 | 结论边界 |
|---|---|---|---|
| `ValidateSolutionConfiguration` | `Debug|x64` | 成功 | 只证明解决方案配置映射可解析 |
| `ValidateSolutionConfiguration` | `Debug|Any CPU` | 成功 | 只证明解决方案配置映射可解析 |
| 根 `slnx` Restore | `Debug|x64` | 成功 | 只证明完整 x64 还原成功 |
| 根 `slnx` Restore + Build | `Debug|x64` | 失败：`CS0234` | 当前首错位于 `src/Microsoft.DotNet.Wpf/src/Shared/MS/Win32/UnsafeNativeMethodsCLR.cs(310,127)`：`Accessibility.IAccessible` 不存在，所属项目为 `WindowsBase.csproj` |
| 根 `slnx` Restore + Build | `Debug|Any CPU` | 未验证 | 不得从配置验证或 x64 结果外推 |
| `PresentationFramework` | `Debug|x64` | Restore/Build 成功 | 独立项目验证 |
| `PresentationUI` | `Debug|x64` | Restore/Build 成功 | 独立项目验证 |
| `WindowsFormsIntegration` | `Debug|x64` | Restore/Build 成功 | 独立项目验证 |
| `DirectWriteForwarder` | `Debug|x64` | 仅增量 Build 成功 | 未证明干净构建或强制重编译成功 |
| `System.Printing` | 未执行 | 未构建 | 旧错误只能作为历史线索，不能当作当前首个错误 |

完整 x64 验证命令为：

`msbuild Microsoft.Dotnet.Wpf.slnx -restore /p:Configuration=Debug /p:Platform=x64 /m:1 /nr:false /v:minimal`

当前可能出现但不一定导致失败的警告包括：

- `NU1603`。
- `MSB3243`。
- 缺少 Perl 时跳过相关脚本的警告。
- `D9035` 尚未通过 `DirectWriteForwarder` 强制重编译复验，不能根据增量构建结果判断其当前状态。

此前观察到的 WpfDemo 输出文件锁在最新复验中未再出现；它保留为历史环境线索，不再是当前首个阻塞。

## 已落地能力

### Native 与 Builder

- 已实现共享 native 资产清单。
- Builder 已实现 x64 和 x86 路径。
- arm64 尚未实现。

### WpfDemo

- 已实现 WpfDemo x64 消费与部署路径。
- WpfDemo x64 命令行构建和运行探针已有历史/现存验证报告。
- 文化资源程序集的当前复制模式尚未证明会保留文化子目录，存在同名卫星程序集被扁平复制或覆盖的风险。
- Visual Studio F5 实现尚未复验。
- WpfDemo x86、arm64 和 Publish 尚未实现。

### PresentationUI

- `PresentationUI` 已配置 `InternalMarkupCompilation`。
- 当前 `artifacts` 中存在 4 个 `.g.cs` 文件。
- 对应的 4 个 `.xaml.cs` 仍显式声明基类。
- 现有产物不能证明干净状态下会稳定生成；干净生成和显式基类回退均待验证。

### Cycle-breaker

- 根 `slnx` 已纳入 8 个 cycle-breaker 项目。
- 其中 7 个存在直接消费者。
- `PresentationFramework-System.Printing-impl-cycle` 当前没有直接消费者，去留待确认。

## 当前未决项

1. 修复 `WindowsBase` 对 `Accessibility.IAccessible` 的编译引用边界，并重新验证完整 `Debug|x64` 构建。
2. x64 成功后验证完整 `Debug|Any CPU` 构建；当前只有解决方案配置映射验证成功。
3. 在 Visual Studio 中确认 57 个项目的实际加载状态，并复验 WpfDemo F5。
4. 验证并修正 WpfDemo 文化资源程序集的子目录部署，避免不同文化的同名资源被覆盖。
5. 在干净输出下验证 `PresentationUI` 的 4 个 `.g.cs` 可重复生成，再决定是否回退 4 个 `.xaml.cs` 的显式基类。
6. 构建 `System.Printing.vcxproj`，依据当前首个真实错误决定修复与纳管方式。
7. 确定目标版本边界：
   - `System.Windows.Primitives` 只存在于 `origin`，当前仓库缺失；是否迁入取决于目标版本边界，不能直接认定为必迁模块。
   - `Extensions` 下 5 个项目的去留取决于兼容目标。
8. 判断无直接消费者的 `PresentationFramework-System.Printing-impl-cycle` 是否仍有保留价值。
9. 补齐测试迁移、WpfDemo x86、arm64、Publish 和旧生成工具的目标范围；这些能力当前均未完成。

## 验证边界

- 根 `slnx` 的 x64 Restore 成功不等于完整 x64 Build 成功。
- 最新复验中的失败是当前源码编译图错误；在修复并重跑成功前，完整解决方案仍处于未通过状态。
- 独立项目成功不能替代根 `slnx` 成功；增量成功不能替代干净构建成功。
- IDE 枚举项目路径不能证明项目已加载；必须由 Visual Studio 的加载状态和实际构建验证。
- `artifacts` 中已有生成文件不能证明从干净状态可重现。
- 没有重新执行的历史命令、旧错误和专题文档不得提升为当前结论。