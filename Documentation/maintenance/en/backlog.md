# Backlog

This document records only low-priority items that must not interrupt the main line in [01-phase-plan.md](01-phase-plan.md). Work that enters the official phase plan should be removed from here. Current build facts remain owned by [00-overview.md](00-overview.md).

## Pin verifiable origin snapshot metadata and hashes

- Background: `origin/` is the source for migration and difference audits, but confirming that the directory is non-empty does not prove that different times, machines, or restore processes used the same source snapshot.
- Evidence: `origin/` currently contains `src/` and `.gitignore`, with no readable `.git` commit metadata. [03-origin-diff-audit.md](03-origin-diff-audit.md) records a declared source commit, but that object cannot be verified by the outer repository, and a reproducible acquisition method and content-hash inventory are still missing. `origin/src` is also outside outer Git protection.
- Trigger: when replacing or refreshing `origin/`, restarting a large difference audit, or making audit conclusions reproducible across machines, first record the source identity, acquisition time, and filter rules, then generate a deterministic file inventory and hashes.

## Unify Builder and WpfDemo required native file inventories

- Background: native-file copy, package validation, and WpfDemo output validation should share one purpose-tagged declaration so adding or removing files does not drift across inventories.
- Evidence: [`eng/WpfRuntimeDependencies.props`](../../../eng/WpfRuntimeDependencies.props) defines 5 `RepoWpfNativeRuntimeFile` items. WpfDemo copies files from that item, but output validation separately hard-codes 3 files. [`NuGetPackageService.cs`](../../../eng/Builder/NuGetPackageService.cs) copies every native DLL in the package and hard-codes 4 required files in a separate array; `ijwhost.dll` also comes from a different host package.
- Trigger: when the native file set, runtime package version, package-validation rules, or consumption entry changes, first make Builder and WpfDemo read the same structured inventory, keep the runtime-package versus host-package source difference, and remove Builder's filename hard-coding. This maintenance item converges the existing x64/x86 rules. It does not replace platform expansion in the phase plan.

## Fix Builder `compare` fallback when staging is missing

- Background: `compare` should compare the complete in-repo reference-assembly inventory. Generating a report that looks complete but actually covers only one output directory when staging is missing misleads missing-item judgment.
- Evidence: after `CompareService` calls `AssemblyCollector.CollectReferenceDlls`, it uses only the directory of the first collected item as `ourDir`, then enumerates DLLs in that directory only.
- Trigger: when `compare` must be allowed to run independently of the default Builder build, compare the complete collected dictionary directly or first aggregate into a temporary directory. Until then, treat only results with a complete `staging/ref/net8.0` as valid reports.

## Review two cycle-breaker `PackageId` names

- Background: a cycle-breaker's project name, relationship name, and `PackageId` should correspond. `TargetOutputRelPath` also contains `PackageId` directly, so naming drift can produce hard-to-recognize output directories.
- Evidence: `ReachFramework-System.Printing-api-cycle.csproj` uses `PackageId` `ReachFramework-SystemPrinting-api-cycle`, dropping the dot from the project name. `PresentationFramework-System.Printing-impl-cycle.csproj` uses `PackageId` `PresentationFramework-ReachFramework`, which does not match the dependency relationship expressed by the file name.
- Trigger: after the phase plan decides whether the related cycle-breakers are kept, or when packaging, cache, or output-path logic starts depending on these identifiers, unify the names after checking consumers and existing output paths. Do not interrupt current dependency-closure work merely for naming cleanliness.

## Investigate the lifecycle intent of `DWriteLoader.UnloadDWrite`

- Background: whether `dwrite.dll` should be released at some lifecycle point after explicit load must be decided by host lifecycle and upstream design. The existence of a cleanup method is not enough to infer that it should be called or deleted.
- Evidence: [`DWriteLoader.cs`](../../../src/Microsoft.DotNet.Wpf/src/PresentationCore/MS/internal/Text/TextInterface/DWriteLoader.cs) defines `UnloadDWrite`. A current workspace search found only that definition, with no confirmed call site, while `LoadDWrite` is called from the `PresentationCore` module-initializer path.
- Trigger: when native-module unload, process shutdown, collectible load-context, or related resource-lifecycle issues appear, or when preparing to change this method, first compare against a pinned origin snapshot and verify the actual lifecycle, then decide to add a call, keep it, or remove it.

## Establish a durable log convention for topic verification results

- Background: topic conclusions need to be associated with commands, environment, exit codes, and original logs. Keeping only a summary in documentation makes it hard to recheck conclusion bounds or compare later regressions.
- Evidence: [00-overview.md](00-overview.md) states that some build results have no durable independent logs. Builder diagnostic logs are written to `artifacts/log/Builder`, and `artifacts/` is excluded by `.gitignore`. Existing topic material mainly stores textual verification summaries, with no unified naming, metadata, retention location, or checksum method.
- Trigger: when a verification result must be cited as a long-term fact, reproduced across machines, or compared with a second record of the same topic, establish a convention that at least stores the command, toolchain and environment, time, exit code, log location, and log hash. Concrete phase verification actions remain owned by [01-phase-plan.md](01-phase-plan.md).
