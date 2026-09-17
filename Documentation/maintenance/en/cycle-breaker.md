# Cycle-Breaker Topic Audit

## Responsibility and conclusion bounds

The root [`Microsoft.Dotnet.Wpf.slnx`](../../../Microsoft.Dotnet.Wpf.slnx) currently includes 8 `cycle-breaker` projects. These projects are bridges maintained by the current reorganization tree to break build-dependency cycles. They are not a long-term product boundary that replaces real implementations.

This document records only current project-reference relationships, retention conditions, and exit conditions. Whole-repository status is owned by [00-overview.md](00-overview.md). Implementation order is owned by [01-phase-plan.md](01-phase-plan.md).

"Direct consumer" here means a direct `ProjectReference` in a project file. That method does not cover generation tasks, reflection, hard-coded output paths, or other indirect consumption. "No direct consumer found" is therefore not the same as "safe to delete immediately".

## The 8 projects in the root `slnx`

Directed reference search of project files shows that 7 projects have direct consumers. `PresentationFramework-System.Printing-impl-cycle` currently has no direct consumer and is pending confirmation.

| Project | Direct consumers / status | Retention condition |
|---|---|---|
| `PresentationFramework-PresentationUI-api-cycle.csproj` | `PresentationUI-PresentationFramework-impl-cycle.csproj`; has a direct consumer | Keep while the `PresentationUI` implementation bridge still needs a minimal `PresentationFramework` API and a real project reference would form a cycle |
| `PresentationFramework-ReachFramework-impl-cycle.csproj` | `ReachFramework.csproj`, `ReachFramework-ref.csproj`; has direct consumers | Keep while the `ReachFramework` implementation or ref layer still cannot use a closed `PresentationFramework` boundary directly |
| `PresentationFramework-System.Printing-api-cycle.csproj` | `ReachFramework-System.Printing-api-cycle.csproj`, `System.Printing-ref.csproj`, `System.Printing.vcxproj`, `ReachFramework.csproj`, `ReachFramework-ref.csproj`; has direct consumers | Keep while printing-related consumers still need a minimal `PresentationFramework` API and the real `System.Printing` / `PresentationFramework` reference boundary is not closed |
| `PresentationFramework-System.Printing-impl-cycle.csproj` | Directed search found no direct consumer; **pending confirmation** | First investigate generation-time use, indirect references, and hard-coded output paths. Delete only after confirming there is no consumption and the replacement topology is explicit. Do not delete because a static search is empty |
| `PresentationUI-PresentationFramework-impl-cycle.csproj` | `PresentationFramework.csproj`; has a direct consumer | Keep while `PresentationFramework` still needs the bridge to consume the `PresentationUI` implementation surface and a direct project reference would form a cycle |
| `ReachFramework-PresentationFramework-api-cycle.csproj` | `PresentationFramework-ReachFramework-impl-cycle.csproj`, `PresentationFramework-System.Printing-api-cycle.csproj`, `PresentationFramework-System.Printing-impl-cycle.csproj`; has direct consumers | Keep while those bridge projects still need a minimal `ReachFramework` / printing contract. Shrink or exit as their consumers converge |
| `ReachFramework-System.Printing-api-cycle.csproj` | `System.Printing-ref.csproj`, `System.Printing.vcxproj`; has direct consumers | Keep while `System.Printing` implementation and ref still cannot use a real, type-identity-consistent `ReachFramework` contract |
| `System.Printing-PresentationFramework-api-cycle.csproj` | `PresentationFramework-System.Printing-api-cycle.csproj`, `PresentationFramework-System.Printing-impl-cycle.csproj`; has direct consumers | Keep while the `PresentationFramework` printing bridge still needs a minimal `System.Printing` API and the real implementation has not taken over |

"Has a direct consumer" only means the current reference edge exists. It does not prove that project content is already minimized, and it does not prove that every bridge must be kept permanently.

## Statement bounds versus origin

- Do not use "origin has no cycle-breaker projects" as a blanket premise, and do not infer origin project organization from the current directory location or project names.
- This document can only confirm that the listed 8 projects are maintained by the current reorganization tree and included in the root `slnx`.
- For a specific source file inside a bridge project, source relationship can be judged only after file-by-file path, history, or content-comparison evidence. This document does not make a source-missing judgment for files such as `XpsDocument`.
- Statistical methods and source-protection bounds for origin structural differences are in [03-origin-diff-audit.md](03-origin-diff-audit.md).

## Real `System.Printing` implementation gap

- `src/Microsoft.DotNet.Wpf/src/System.Printing/System.Printing.vcxproj` is not currently included in the root `slnx`. The real implementation still does not own the printing boundary in the root build graph.
- Printing-related cycle-breakers only temporarily close compile dependencies and a minimal contract. They are not a long-term replacement for the real `System.Printing` implementation.
- Before converging printing bridges, first build and include the real implementation according to [phase 4 in 01-phase-plan.md](01-phase-plan.md#phase-4-build-and-include-the-systemprinting-implementation), then migrate consumers one by one, check same-named assemblies and type identity, and finally evaluate deleting the bridge projects.
- Do not simulate a complete `System.Printing` by continually expanding stub APIs. New bridge members must have current-consumer evidence and an explicit exit condition.

## Removal or convergence conditions

Evaluate removing a cycle-breaker only after all of the following are true:

1. Direct `ProjectReference`s, generation-time dependencies, indirect consumption, and hard-coded output paths have been checked.
2. A real implementation project or a stable ref/API boundary already provides the required contract. Consumers can migrate without recreating a project cycle.
3. Same-named assemblies, duplicate types, and runtime-binding risks have been checked. Do not hide type-identity issues by expanding bridge content.
4. Remove only one reference edge or one project at a time, and update the root `slnx` in the same change. Related standalone projects and the root entry must be re-verified.
5. After deletion, it must still be possible to say which real project provides public API, resources, markup compilation, and the printing chain.

`PresentationFramework-System.Printing-impl-cycle` should complete consumption audit first, but remain "pending confirmation" until the evidence above is complete. Do not delete it directly.

## Low-priority project-metadata maintenance

Two `PackageId` values currently do not match their project file names:

| Project | Current `PackageId` | Inconsistency |
|---|---|---|
| `PresentationFramework-System.Printing-impl-cycle.csproj` | `PresentationFramework-ReachFramework` | Does not match the `System.Printing` boundary expressed by the project |
| `ReachFramework-System.Printing-api-cycle.csproj` | `ReachFramework-SystemPrinting-api-cycle` | Does not match the `System.Printing` spelling in the project file name |

These are low-priority maintenance items. `PackageId` also participates in `TargetOutputRelPath`. Check output paths and consumers before changing them. This topic only records the issue and does not modify project files.
