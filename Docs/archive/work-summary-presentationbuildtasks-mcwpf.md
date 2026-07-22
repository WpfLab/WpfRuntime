# PresentationBuildTasks 与 mcwpf 迁移历史摘要

> 历史材料，不代表当前状态。

## 记录过的主题

该材料记录了 `PresentationBuildTasks` 目标框架调整、`mcwpf` 从旧项目格式迁移到 SDK 风格、模板文件处理，以及两个项目进入构建编排后的阶段性验证。它保留的是迁移决策形成过程，不是当前项目清单或构建基线。

## 已被取代的结论

- 基于传统根解决方案得出的项目纳管数量和整体验证结论，已由根 `.slnx` 的当前声明清单与验证边界取代。
- 当时使用的 SDK、目标框架和构建成功描述均为时间点记录，不能外推到当前工作区。
- `PresentationBuildTasks` 的加载路径和预构建方式已演进为当前的任务程序集选择、按需构建与锁定输出处理机制。
- 材料中的后续迁移建议已由现行阶段计划、Builder 专题和差异审计重新分工。

## 当前资料

- [当前状态概览](../00-overview.md)
- [后续阶段计划](../01-phase-plan.md)
- [PresentationBuildTasks 按需构建机制](../PresentationBuildTasks-bootstrap.md)
- [Builder 构建与打包专题](../05-builder-plan.md)
- [与 origin 的差异审计](../03-origin-diff-audit.md)