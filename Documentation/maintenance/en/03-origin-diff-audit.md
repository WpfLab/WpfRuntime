# Structural Difference Audit Between the Current Tree and the Origin Snapshot

## Document responsibility

This document records only structural differences between the current working tree and the local `origin` snapshot, plus the counting method and later re-audit method. Whole-repository status is owned by [00-overview.md](00-overview.md). Later implementation order is owned by [01-phase-plan.md](01-phase-plan.md). Do not copy whole-repository build status here, and do not promote historical build results to current conclusions.

## Source and protection bounds

- The source tree is counted as repository-relative `origin/src/`. The current main source tree is counted as `src/`. First-level WPF module comparison uses `origin/src/` and `src/Microsoft.DotNet.Wpf/src/`.
- `origin/.gitignore` explicitly ignores `src/`. Therefore `origin/src/` is not protected by the outer Git status. If its contents are modified or deleted, the outer repository may not show a change.
- The declared origin commit is `44615ed4b9f033922b3361ea02c02f173b8bf82e`.
- The current outer repository object store cannot verify that commit with `git cat-file`. The value is a source declaration only. It does not prove that the current `origin/src/` matches that Git object, and it cannot be treated as a snapshot recoverable from the outer repository.
- Before every structural audit, confirm that `origin/` is non-empty. If `origin/` or `origin/src/` has been emptied, stop migration-related work immediately.

## Unified counting method

### Definition

- Recursively exclude `bin/`, `obj/`, `artifacts/`, and `TestResults/` from paths.
- "Projects" count only `*.csproj`, `*.vcxproj`, and `*.proj`.
- "Files" count all ordinary files after excluding those output directories. Project files are included in the file count.
- "Current repository primary project roots" combine `src/`, `Demo/`, `Documentation/maintenance/`, and `eng/`.
- Root-solution counts use unique `<Project Path="...">` declarations in [`Microsoft.Dotnet.Wpf.slnx`](../../../Microsoft.Dotnet.Wpf.slnx). Do not mix them with on-disk project counts.

### Current results

| Scope | Projects | Files | Notes |
|---|---:|---:|---|
| `origin/src/` | 90 | 6380 | Local source snapshot |
| Current main source tree `src/` | 64 | 4980 | Includes the 8 bridge projects under `src/Microsoft.DotNet.Wpf/cycle-breakers/` |
| Current repository primary project roots total | 68 | — | Combined `src/`, `Demo/`, `Documentation/maintenance/`, and `eng/` |
| Root `slnx` | 57 | — | Solution declaration count. It does not represent on-disk project total or IDE load status |

These numbers are a snapshot of tree state. After adding, migrating, or deleting projects or generating files, recount with the same method. Do not keep old numbers long-term, and do not derive a "missing project count" from project-count differences alone.

## Verified structural differences

### First-level module bounds

After comparing first-level directory names of `origin/src/` and `src/Microsoft.DotNet.Wpf/src/`, origin contains these three extra modules:

| Module | Current disposition | Audit reading |
|---|---|---|
| `PenImc` | Binary-asset approach | Not a source module waiting to be copied. Later check the asset inventory, platform coverage, and consumption chain |
| `WpfGfx` | Binary-asset approach | Not a source module waiting to be copied. Later check the asset inventory, platform coverage, and consumption chain |
| `System.Windows.Primitives` | Not yet decided whether to migrate | Whether it belongs in the target tree depends on the target WPF/.NET version and compatibility bound. Do not judge it as required or excluded |

Therefore a missing first-level directory is not the same as three migration omissions. `PenImc` and `WpfGfx` changed from a source bound to a binary-asset bound. `System.Windows.Primitives` is a version-decision item.

### 11 projects not directly included in the root `slnx`

The 11-item gap between 68 on-disk projects and 57 root-`slnx` projects is classified in [the project inventory in 00-overview.md](00-overview.md#11-projects-not-directly-included-in-the-root-slnx):

| Category | Count | Current placement |
|---|---:|---|
| `System.Printing.vcxproj` | 1 | Real implementation is not yet included in the root `slnx` |
| Projects under `Extensions` | 5 | Retention depends on the target version and compatibility scope |
| `OSVersionHelper.vcxproj` | 1 | Whether it has already been replaced by a binary approach is still to be confirmed |
| `eng/Builder/PackageTestApp/PackageTestApp.csproj` | 1 | Template used by Builder, not a main-chain implementation waiting to be migrated |
| `ThemeGenerator.proj` | 2 | Generator tool projects |
| `wpf-etw.proj` | 1 | Generator tool project |
| Total | 11 | Must be judged by category. Do not call them omissions as a group |

Whether template and generator projects enter the root entry depends on their role. Do not classify them as missing implementations only because they are not directly included in the root `slnx`.

### PresentationUI generation bound

- `PresentationUI.csproj` has enabled `InternalMarkupCompilation` and already includes `InstallationError.xaml`, `TenFeetInstallationError.xaml`, `TenFeetInstallationProgress.xaml`, and `MS/Internal/Documents/FindToolBar.xaml`.
- The current output tree already has the corresponding 4 `.g.cs` files. That proves the generation chain and existing generation results exist. It does not prove they can still be reproduced stably after outputs are cleaned.
- The corresponding 4 `.xaml.cs` files still declare base classes explicitly. The remaining audit focus is verifying repeatable generation from a clean state, then converging explicit base-class differences one by one from those results.

### Current reorganization layer

- The 8 projects under `src/Microsoft.DotNet.Wpf/cycle-breakers/` belong to the bridge layer maintained by the current reorganization tree and are all included in the root `slnx`. They count in the 68 on-disk projects, 57 solution projects, and 64 `src/` projects.
- Direct consumers, status, and exit conditions for each bridge project are in [cycle-breaker.md](cycle-breaker.md). A structural audit must not judge a bridge project or a file inside it against the source tree by name alone.
- The real `System.Printing.vcxproj` implementation is still on disk but not directly included in the root `slnx`. Record that fact separately from the temporary bridging role of cycle-breakers.

### Theme-assembly bounds

- The 7 theme ref projects currently compile against the `PresentationUI`, `System.Xaml`, `WindowsBase`, and `PresentationCore` ref projects.
- Their project reference to `PresentationFramework-ref.csproj` is build ordering only, with `ReferenceOutputAssembly="false"` and `PrivateAssets="all"`. The actual compile reference explicitly points to `$(ArtifactsObjDir)PresentationFramework-ref\$(WpfNativePlatform)\$(Configuration)\$(TargetFramework)\ref\PresentationFramework.dll`.
- This difference has current forced-rebuild evidence: if the project reference participates in assembly resolution, the printing-related `PresentationFramework-System.Printing-api-cycle` can pass a same-named `PresentationFramework.dll` into theme refs, and `PresentationFramework.Royale-ref` selected an incomplete bridge and produced 53 `CS0234` errors for missing control types.
- After Trusted Builder cleans `artifacts`, runtime theme projects expose the same class of issue: a conditional `PresentationFramework.dll` reference is removed during project evaluation because the file does not exist yet, then MarkupCompile may load a same-named cycle-breaker and fail because known type 380 `LostFocusEventManager` cannot be resolved. The 7 runtime theme projects currently set `PresentationFramework.csproj` as a `ReferenceOutputAssembly="false"`, `PrivateAssets="all"` build-ordering dependency, and unconditionally reference `$(ArtifactsBinDir)PresentationFramework\$(WpfNativePlatform)\$(Configuration)\$(TargetFramework)\PresentationFramework.dll`. That change still needs Trusted Builder re-verification.
- These explicit references are an isolation bound for the current cycle-breaker phase, not a long-term goal. They can fall back to ordinary project references only after the real printing implementation and ref dependencies close, same-named bridges no longer leak to non-printing consumers, and dual-platform forced rebuilds still succeed.

## Later audit method

1. **Protect the source**: confirm `origin/` and `origin/src/` are non-empty, inspect `origin/.gitignore`, and do not rely on outer `git status` to decide whether the source tree is safe.
2. **Record source-identity bounds**: keep the declared source commit and verify it against the outer object store. If verification fails, mark it as "source declaration only". When a traceable snapshot is required, save a file inventory or hashes separately. Do not assume the outer Git can restore `origin/src/`.
3. **Recount with the unified method**: count `origin/src/`, `src/`, the five primary project roots, and root-`slnx` declarations using the same excluded directories and project extensions.
4. **Compare first-level modules**: compare only logical module-root directory names, and record each difference as "source migration", "binary asset", "target-version exclusion", or "to be confirmed". Do not convert a directory set difference directly into a migration list.
5. **Check solution bounds**: enumerate root-`slnx` project paths and verify they exist on disk. Then classify projects not directly included as real implementation, compatibility extension, template, or generator tool.
6. **Run targeted file audits**: do file-level diffs only when handling a specific module or bridge. Conclusions must include current path, consumer, or generation-chain evidence. Do not keep unverifiable file-count attributions or source-missing claims.
7. **Keep responsibilities separate**: structural counts and differences stay in this document. Current build facts go to [00-overview.md](00-overview.md). Later actions go to [01-phase-plan.md](01-phase-plan.md).
