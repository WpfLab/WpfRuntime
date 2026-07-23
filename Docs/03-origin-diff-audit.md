# 当前树与 origin 快照的结构差异审计

## 文档职责

该文档只记录当前工作树与本地 `origin` 快照之间的结构差异、统计方法和后续复核方法。完整仓库状态以 [00-overview.md](00-overview.md) 为准，后续实施顺序以 [01-phase-plan.md](01-phase-plan.md) 为准；这里不复制整体构建状态，也不把历史构建结果提升为当前结论。

## 来源与保护边界

- 来源树按仓库相对路径记为 `origin/src/`，当前主源码树按 `src/` 统计；一级 WPF 模块对比使用 `origin/src/` 与 `src/Microsoft.DotNet.Wpf/src/`。
- `origin/.gitignore` 明确忽略 `src/`。因此 `origin/src/` 不受外层 Git 状态保护，内容被修改或删除时，外层仓库不一定给出变更提示。
- 来源声明记录的 origin commit 为 `44615ed4b9f033922b3361ea02c02f173b8bf82e`。
- 当前外层仓库对象库无法通过 `git cat-file` 验证该 commit。该值只能作为来源声明，不能据此证明当前 `origin/src/` 与该 Git 对象完全一致，也不能把它当作外层仓库可恢复的快照。
- 每次开始结构审计前必须先确认 `origin/` 非空；若 `origin/` 或 `origin/src/` 被清空，应立即停止迁移相关操作。

## 统一统计口径

### 口径定义

- 递归排除路径中的 `bin/`、`obj/`、`artifacts/`、`TestResults/`。
- “项目”只统计 `*.csproj`、`*.vcxproj`、`*.proj`。
- “文件”统计排除上述输出目录后的全部普通文件，项目文件也包含在文件数中。
- “当前仓库主要项目根”合并统计 `src/`、`cycle-breakers/`、`Demo/`、`Docs/`、`eng/`。
- 根解决方案统计以 [`Microsoft.Dotnet.Wpf.slnx`](../Microsoft.Dotnet.Wpf.slnx) 中唯一的 `<Project Path="...">` 声明为准，不与磁盘项目数混用。

### 当前结果

| 范围 | 项目数 | 文件数 | 说明 |
|---|---:|---:|---|
| `origin/src/` | 90 | 6380 | 本地来源快照 |
| 当前主源码树 `src/` | 56 | 4980 | 不含根级 `cycle-breakers/` 等其他项目根 |
| 当前仓库主要项目根合计 | 68 | — | 合并 `src/`、`cycle-breakers/`、`Demo/`、`Docs/`、`eng/` |
| 根 `slnx` | 57 | — | 解决方案声明数，不代表磁盘项目总数或 IDE 加载状态 |

这些数字是树状态的瞬时结果。新增、迁移、删除项目或生成文件后必须按同一口径重算，不能长期沿用旧数字，也不能仅用项目数差值推导“遗漏项目数”。

## 已验证的重点结构差异

### 一级模块边界

对 `origin/src/` 与 `src/Microsoft.DotNet.Wpf/src/` 的一级目录名进行集合比较后，origin 比当前树额外包含以下三个模块：

| 模块 | 当前处置 | 审计判读 |
|---|---|---|
| `PenImc` | 采用 binary 资产方案 | 不作为待复制的源码模块；后续检查资产清单、平台覆盖和消费链 |
| `WpfGfx` | 采用 binary 资产方案 | 不作为待复制的源码模块；后续检查资产清单、平台覆盖和消费链 |
| `System.Windows.Primitives` | 尚未确定是否迁入 | 是否属于目标树取决于目标 WPF/.NET 版本和兼容边界，不能直接判定为必迁或应排除 |

因此，一级目录缺失不等同于三个迁移遗漏：`PenImc` 和 `WpfGfx` 是源码边界改为 binary 资产边界，`System.Windows.Primitives` 则是待版本决策项。

### 根 `slnx` 未直接纳管的 11 个项目

磁盘 68 个项目与根 `slnx` 57 个项目之间的 11 项差额，分类与当前定位以 [00-overview.md 的项目清单](00-overview.md#未直接纳入根-slnx-的-11-个项目) 为准：

| 分类 | 数量 | 当前定位 |
|---|---:|---|
| `System.Printing.vcxproj` | 1 | 真实实现尚未纳入根 `slnx` |
| `Extensions` 下的项目 | 5 | 是否保留取决于目标版本和兼容范围 |
| `OSVersionHelper.vcxproj` | 1 | 是否已由 binary 方案替代仍待确认 |
| `eng/Builder/PackageTestApp/PackageTestApp.csproj` | 1 | Builder 使用的模板项目，不是待迁入的主链实现 |
| `ThemeGenerator.proj` | 2 | 生成工具项目 |
| `wpf-etw.proj` | 1 | 生成工具项目 |
| 合计 | 11 | 必须逐类判读，不能统一称为遗漏 |

模板项目和生成项目是否进入根入口取决于其职责，不应仅因未被根 `slnx` 直接纳管就归类为缺失实现。

### `PresentationUI` 生成边界

- `PresentationUI.csproj` 已启用 `InternalMarkupCompilation`，并已纳入 `InstallationError.xaml`、`TenFeetInstallationError.xaml`、`TenFeetInstallationProgress.xaml` 和 `MS/Internal/Documents/FindToolBar.xaml`。
- 当前产物树中已有对应的 4 个 `.g.cs`；这证明生成链和现存生成结果已经存在，但不能证明清理输出后仍可稳定重现。
- 对应的 4 个 `.xaml.cs` 仍显式声明基类。剩余审计重点是验证干净状态下的可重复生成，再依据生成结果逐个收敛显式基类差异。

### 当前重组层

- 根 `cycle-breakers/` 下的 8 个项目属于当前重组树维护的桥接层，并全部纳入根 `slnx`；它们计入 68 个磁盘项目和 57 个解决方案项目，但不计入 `src/` 的 56 个主源码项目。
- 各桥接项目的直接消费者、状态和退出条件见 [cycle-breaker.md](cycle-breaker.md)。结构审计不得仅凭名称判断某个桥接项目或其中某个文件与来源树的关系。
- `System.Printing.vcxproj` 真实实现仍位于磁盘但未被根 `slnx` 直接纳管。该事实应与 cycle-breaker 的暂时桥接职责分开记录。

### 主题引用程序集边界

- 7 个主题 ref 项目当前均使用 `PresentationUI`、`System.Xaml`、`WindowsBase`、`PresentationCore` 的 ref 项目作为编译依赖。
- 它们对 `PresentationFramework-ref.csproj` 的项目引用只承担构建排序，设置 `ReferenceOutputAssembly="false"` 和 `PrivateAssets="all"`；实际编译引用显式指向 `$(ArtifactsObjDir)PresentationFramework-ref\$(WpfNativePlatform)\$(Configuration)\$(TargetFramework)\ref\PresentationFramework.dll`。
- 该差异有当前强制重建证据：若直接让项目引用参与程序集解析，打印相关 `PresentationFramework-System.Printing-api-cycle` 会以同名 `PresentationFramework.dll` 传递进入主题 ref，`PresentationFramework.Royale-ref` 会因选中不完整 bridge 而出现 53 个缺少控件类型的 `CS0234`。
- 该显式引用是当前 cycle-breaker 阶段的隔离边界，不是长期目标。只有在打印真实实现与 ref 依赖闭合、同名 bridge 不再泄漏到非打印消费者，并且双平台强制重建仍成功后，才能回退为普通 ref-to-ref 项目引用。

## 后续审计方法

1. **保护来源**：确认 `origin/` 和 `origin/src/` 非空，检查 `origin/.gitignore`，不得依赖外层 `git status` 判断来源树是否安全。
2. **记录来源身份边界**：保留来源声明 commit，同时用外层对象库验证；验证失败时明确标为“仅来源声明”。需要可追溯快照时，另行保存文件清单或哈希，不能假设外层 Git 可恢复 `origin/src/`。
3. **重算统一统计**：按同一排除目录和项目扩展名分别统计 `origin/src/`、`src/`、五个主要项目根及根 `slnx` 声明。
4. **比较一级模块**：只比较逻辑模块根的目录名，并为每个差异记录“源码迁入”“binary 资产”“目标版本排除”或“待确认”，避免把目录差集直接转换为迁移清单。
5. **核对解决方案边界**：枚举根 `slnx` 的项目路径并验证磁盘存在性；再对未直接纳管项目按真实实现、兼容扩展、模板、生成工具分类。
6. **执行针对性文件审计**：只有在处理具体模块或桥接时才做文件级差异，结论必须附带当前路径、消费者或生成链证据；不保留无法复核的文件数量归因和来源缺失断言。
7. **维护职责分离**：结构统计和差异留在该文档；当前构建事实更新到 [00-overview.md](00-overview.md)；后续动作更新到 [01-phase-plan.md](01-phase-plan.md)。
