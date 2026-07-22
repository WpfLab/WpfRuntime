# 后备待办记录

该文档只记录不应打断 [01-phase-plan.md](01-phase-plan.md) 主线的低优先级事项。进入正式阶段计划的工作应从这里移除，当前构建事实仍以 [00-overview.md](00-overview.md) 为准。

## 固定可验证的 origin 快照元数据与哈希

- 背景：`origin/` 是迁移与差异审计的来源，但仅确认目录非空不足以保证不同时间、机器或恢复过程使用的是同一份源快照。
- 证据：`origin/` 当前包含 `src/` 与 `.gitignore`，没有可读取的 `.git` 提交元数据；[03-origin-diff-audit.md](03-origin-diff-audit.md) 记录了来源声明 commit，但该对象无法由外层仓库验证，且仍缺少可复验的获取方式和内容哈希清单；`origin/src` 还处于外层 Git 保护之外。
- 触发条件：替换或刷新 `origin/`、重新开展大范围差异审计，或需要让审计结论可跨机器复验时，先记录来源标识、获取时间、过滤规则，并生成确定性的文件清单与哈希。

## 统一 Builder 与 WpfDemo 的 native 必需文件清单

- 背景：native 文件的复制、打包校验和 WpfDemo 输出校验应基于同一份带用途信息的声明，避免增删文件时多处清单漂移。
- 证据：[`eng/WpfRuntimeDependencies.props`](../eng/WpfRuntimeDependencies.props) 定义了 5 个 `RepoWpfNativeRuntimeFile`；WpfDemo 用该项复制文件，但输出校验另行硬编码 3 个文件；[`NuGetPackageService.cs`](../eng/Builder/NuGetPackageService.cs) 会复制包内全部 native DLL，并以独立数组硬编码 4 个必需文件，`ijwhost.dll` 还来自另一类 host 包。
- 触发条件：native 文件集合、运行时包版本、打包校验规则或消费入口发生变化时，先让 Builder 与 WpfDemo 读取同一份结构化清单，并保留 runtime 包与 host 包来源差异，消除 Builder 的文件名硬编码。该维护项针对现有 x64/x86 规则收敛，不替代阶段计划中的平台扩展。

## 修正 Builder `compare` 的无 staging 回退

- 背景：`compare` 应比较完整的仓库参考程序集清单；在 staging 不存在时生成看似完整但实际只覆盖单个输出目录的报告会误导缺失项判断。
- 证据：`CompareService` 调用 `AssemblyCollector.CollectReferenceDlls` 后，只把收集结果第一项所在目录作为 `ourDir`，随后仅枚举该目录中的 DLL。
- 触发条件：需要允许脱离默认 Builder 构建独立运行 `compare` 时，改为直接比较完整收集字典或先汇总到临时目录；修复前只把具有完整 `staging/ref/net8.0` 的结果作为有效报告。

## 核对两个 cycle-breaker 的 `PackageId` 命名

- 背景：cycle-breaker 的项目名、关系名和 `PackageId` 应可相互对应；`TargetOutputRelPath` 又直接包含 `PackageId`，命名偏差可能形成难以识别的输出目录。
- 证据：`ReachFramework-System.Printing-api-cycle.csproj` 的 `PackageId` 是 `ReachFramework-SystemPrinting-api-cycle`，缺少项目名中的点号；`PresentationFramework-System.Printing-impl-cycle.csproj` 的 `PackageId` 是 `PresentationFramework-ReachFramework`，与文件名表达的依赖关系不一致。
- 触发条件：阶段计划完成相关 cycle-breaker 的去留判断且项目需要保留，或打包、缓存、输出路径逻辑开始依赖这些标识时，再核对消费者与现有产物路径后统一命名；不要仅为命名整洁打断当前依赖闭合工作。

## 调查 `DWriteLoader.UnloadDWrite` 的生命周期意图

- 背景：显式加载 `dwrite.dll` 后是否需要在某个生命周期节点释放，应由宿主生命周期和上游设计决定，不能仅凭存在一个清理方法推断应调用或删除。
- 证据：[`DWriteLoader.cs`](../src/Microsoft.DotNet.Wpf/src/PresentationCore/MS/internal/Text/TextInterface/DWriteLoader.cs) 定义了 `UnloadDWrite`；当前工作区搜索只发现该定义，没有已确认调用点，而 `LoadDWrite` 由 `PresentationCore` 的模块初始化路径调用。
- 触发条件：出现 native 模块卸载、进程关闭、可卸载加载上下文或相关资源生命周期问题，或准备调整该方法时，先对照固定的 origin 快照并验证实际生命周期，再决定补充调用、保留或移除。

## 为专题验证结果建立可持久日志约定

- 背景：专题结论需要能够关联到命令、环境、退出码和原始日志；只在文档中保留摘要，难以复查结论边界或比较后续回归。
- 证据：[00-overview.md](00-overview.md) 明确指出部分构建结果没有持久化独立日志；Builder 的诊断日志写入 `artifacts/log/Builder`，而 `artifacts/` 被 `.gitignore` 排除；现有专题材料主要保存文字化验证摘要，尚无统一命名、元数据、保留位置和校验方式。
- 触发条件：某项验证结果需要作为长期事实引用、需要跨机器复验，或同一专题产生第二份可比较记录时，建立统一约定，至少保存命令、工具链与环境、时间、退出码、日志位置和日志哈希；具体阶段验证动作仍由 [01-phase-plan.md](01-phase-plan.md) 管理。
