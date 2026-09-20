# WpfDemo 构建运行历史计划摘要

> 历史材料：本文只保留 WpfDemo 早期构建运行计划的实施脉络，不作为当前状态或后续执行依据。

当前权威实现见 [../07-wpfdemo-implementation.md](../07-wpfdemo-implementation.md)，当前状态见 [../00-overview.md](../00-overview.md)。历史未完成项已转入 [../01-phase-plan.md](../01-phase-plan.md)。

## 历史目标

原计划旨在把 `Demo/WpfDemo` 改造成仓库 WPF 的 x64 开发宿主：构建上游实现、使用自动 ref 编译 C#/XAML、部署 app-local 托管与 native 闭包，并通过运行探针确认未加载系统 WPF 共享框架。

## 阶段 1-7 实施摘要

阶段 1-7 已大部分实现：

1. WpfDemo 已恢复仓库 props/targets 继承，并统一使用仓库 SDK 与 artifacts 布局。
2. `PresentationFramework` 和七个主题项目已作为 `ReferenceOutputAssembly=false` 的构建依赖；解决方案 `Any CPU` 与 `x64` 均映射到 WpfDemo 项目 `x64`。
3. WpfDemo 已接入仓库 `PresentationBuildTasks/Microsoft.WinFX.targets`。
4. C# 与 XAML 已改用实现项目自动生成的 ref，并移除同名 SDK inbox 引用。
5. runtime 已改为按共享清单从每个项目主输出收集，并登记到 deps；正式实现不再整包复制 `PresentationFramework` 输出目录。
6. Builder 与 WpfDemo 已共用 runtime 版本、NuGet 包、程序集、主题和 native 清单；x64 native 文件及 `ijwhost.dll` 已纳入部署校验。
7. `WpfRuntimeProbe` 已实现 app-local 托管与 native 来源断言，并支持 `--verify-repo-wpf` 自动报告和退出码。

## 历史结论边界

- 现存验证报告只能证明 x64 探针曾通过，不能证明当前完整解决方案构建成功。
- Visual Studio F5 的实现尚未复验。
- WpfDemo 当前只支持 `Debug|x64`；`x86`、`arm64` 和 Publish 未实现。
- `System.Printing` 不属于当前 WpfDemo runtime 清单。
- 只发现 `UnloadDWrite()` 定义，未确认调用，不能把 DWrite 退出释放记为已验证能力。

完整解决方案复验、Visual Studio F5、平台扩展、Publish 和其他仓库级未决项统一由 [../01-phase-plan.md](../01-phase-plan.md) 管理。