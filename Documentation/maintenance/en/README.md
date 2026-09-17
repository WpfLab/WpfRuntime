# WPF Reorganization Documentation

This directory maintains current facts, later phases, and unattended handoff. The language-neutral entry is [`../README.md`](../README.md). The repository root entry is [`Microsoft.Dotnet.Wpf.slnx`](../../../Microsoft.Dotnet.Wpf.slnx). The repository root does not contain a traditional solution file with that name; other modules may keep their own `.sln` files.

Chinese is the source language. If this translation conflicts with [`../zh-CN/`](../zh-CN/), the Chinese documents win until the translation is updated.

## Document index

### Core documents

- [00-overview.md](00-overview.md): the single source of current facts. Records the project inventory, build status, landed capabilities, open items, and verification bounds.
- [01-phase-plan.md](01-phase-plan.md): later phases only, including actions, completion criteria, and risks.
- [02-next-session-handoff.md](02-next-session-handoff.md): startup safety checks, the first blocker, continuous-progress rules, and stop conditions.

### Active topics

- [03-origin-diff-audit.md](03-origin-diff-audit.md): verified differences from `origin`, migration trade-offs, and convergence evidence. It does not summarize whole-repository status.
- [05-builder-clean.md](05-builder-clean.md): Builder clean command usage, cleanup scope, and safety bounds.
- [05-builder-plan.md](05-builder-plan.md): Builder build, asset collection, packaging design, and topic implementation details.
- [07-wpfdemo-implementation.md](07-wpfdemo-implementation.md): how WpfDemo consumes in-repo WPF, including MSBuild data flow and extension constraints.
- [08-builder-pr-relay-design.md](08-builder-pr-relay-design.md): how Builder imports commits from a GitHub PR, creates a target PR after local validation, and how Actions writes build artifacts back.
- [09-directwrite-forwarder-resolution.md](09-directwrite-forwarder-resolution.md): DirectWriteForwarder assembly-identity issues when consuming the NuGet package as framework-dependent, plus the test model and fix constraints.
- [PresentationBuildTasks-bootstrap.md](PresentationBuildTasks-bootstrap.md): PresentationBuildTasks assembly selection, on-demand build, and locked-output handling.
- [strong-name-signing.md](strong-name-signing.md): WPF strong-name key sources, identity mapping that matches the original repository, and change constraints.
- [cycle-breaker.md](cycle-breaker.md): cycle-breaker evidence, responsibilities, retention conditions, and exit conditions.
- [backlog.md](backlog.md): observed issues that must not interrupt the official phase order. Items that enter the official plan are governed by `01-phase-plan.md`.

### Historical archive

The archive is not translated. Use [`../zh-CN/archive/README.md`](../zh-CN/archive/README.md) only to trace historical design changes. It is not a source of current status or execution order.

The three core documents own current facts, later plans, and handoff operations. Active topics own one mechanism, its evidence, and its constraints. They must not repeat whole-repository status. The archive does not decide current status.

## Reading order

1. Read [`../README.md`](../README.md) for the documentation layout, then this file for document roles and safety constraints.
2. Read [00-overview.md](00-overview.md) for current workspace facts.
3. Read [01-phase-plan.md](01-phase-plan.md) and choose the next phase by priority.
4. Before making changes, read [02-next-session-handoff.md](02-next-session-handoff.md), complete the startup checks, and continue from the first blocker.
5. After the three core documents, open the topic that matches the task. If a topic conflicts with `00-overview.md`, `00-overview.md` wins.
6. Consult the Chinese archive only when tracing historical design decisions, old rationale, or historical issue clues.

## Fact-maintenance rules

- Record only verified facts. Mark content without direct evidence as "to be confirmed" or "pending Visual Studio verification".
- When new evidence overturns an old conclusion, rewrite the old conclusion. Do not keep contradictory states side by side.
- `00-overview.md` owns current status. `01-phase-plan.md` does not keep completion history. `02-next-session-handoff.md` does not keep session logs.
- Project counts must state the counting method. Solution declarations, on-disk projects, and IDE load status must be described separately.
- A successful build covers only the configuration, platform, and entry that actually ran. Incremental builds, standalone project builds, and restore success must not be generalized to a full solution build.
- Avoid conversation-turn wording, personal-machine absolute paths, and unverified completion claims.
- Markdown links use relative paths. After historical material is classified, it belongs in the Chinese `archive/` and the index must be updated.

## Unattended-progress rules

- Do not wait for the user to split the task. After safety checks, continue in the priority order of [01-phase-plan.md](01-phase-plan.md).
- After a local verification passes, continue with the next real blocker on the same main line. Do not replace implementation with status recitation.
- For diagnosable build, load, or environment problems, collect the first real error and try to fix it. Stop only when a stop condition in the handoff document is met.
- When actual status changes, update `00-overview.md`. Update `01-phase-plan.md` or `02-next-session-handoff.md` only when phase order or the handoff procedure changes. Do not copy volatile status.

## Safety constraints

- Before migration, confirm `origin/` is non-empty. If it is empty, stop immediately.
- `origin/src` is excluded by `origin/.gitignore` and is not protected by the outer Git status. When copying source files, prefer a verifiable script copy and check the source directory before and after the copy.
- Check Git changes before editing and protect existing work. Do not overwrite unexplained modifications.
- Do not run `git clean -xdf`. Cleanup must be limited to confirmed regenerable output directories.
- Do not manufacture a successful build by removing projects. Real implementation projects should enter the root `slnx` after dependencies close. If they cannot, record a precise reason.
- Prefer `msbuild` for native/WPF main-chain work. If primitive types or same-named assemblies conflict, first check whether in-repo implementations and SDK inbox references are both in the compilation graph.
