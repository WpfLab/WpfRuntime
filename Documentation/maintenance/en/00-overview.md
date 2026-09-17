# Current Status Overview

> Audit baseline: current workspace verification. This file is the single source of current facts. Later actions are in [01-phase-plan.md](01-phase-plan.md).

## Repository and build entry

| Item | Current fact |
|---|---|
| Root solution entry | [`../../../Microsoft.Dotnet.Wpf.slnx`](../../../Microsoft.Dotnet.Wpf.slnx) |
| Root traditional solution | The repository root has no traditional solution file with that name; other modules may keep independent `.sln` files |
| `origin/` | Currently non-empty. If it later becomes empty, stop migration immediately |
| `origin/src` protection bound | Excluded by `origin/.gitignore` and not protected by the outer Git status. Do not run `git clean -xdf` |
| .NET SDK | `global.json` specifies `8.0.101`, with `rollForward` `latestFeature` |
| MSBuild | Current workspace verification used `18.7.8.30822` |

## Project inventory

Excluding `origin/`, `artifacts/`, `bin/`, and `obj/`, the disk currently has **70** project files.

The root `slnx` declares **59** unique projects, and all 59 declared paths currently exist:

| Area | Count |
|---|---:|
| `src/Microsoft.DotNet.Wpf/src/` | 46 |
| `src/Microsoft.DotNet.Wpf/cycle-breakers/` | 8 |
| `Demo/` | 1 |
| `Documentation/maintenance/` | 1 |
| `eng/` | 3 |
| Total | 59 |

The IDE interface has not re-enumerated the added `eng/Builder.Tests` and `eng/Builder.ProcessTestHelper` projects. Only the existence of the 59 declared root-`slnx` paths can be confirmed. Actual load success is **pending Visual Studio verification** and must not be described as 59 projects already loaded.

### 11 projects not directly included in the root `slnx`

| Category | Count | Current placement |
|---|---:|---|
| `System.Printing.vcxproj` | 1 | Real implementation gap; not built yet and not included in the root `slnx` |
| Projects under `Extensions` | 5 | Retention depends on the target version and compatibility scope; to be confirmed |
| `OSVersionHelper.vcxproj` | 1 | May already be replaced by a binary approach; to be confirmed |
| `eng/Builder/PackageTestApp/PackageTestApp.csproj` | 1 | Template used by Builder; not equivalent to a main-chain implementation waiting to be migrated |
| `ThemeGenerator.proj` | 2 | Generator tool projects |
| `wpf-etw.proj` | 1 | Generator tool project |
| Total | 11 | Do not treat all 11 as missing main-chain implementations |

## Current build status

The following results come from current workspace verification. Final dual-platform forced-rebuild logs are stored in `artifacts/logs/Microsoft.Dotnet.Wpf-Debug-x64-final.{log,binlog}` and `artifacts/logs/Microsoft.Dotnet.Wpf-Debug-AnyCPU-final.{log,binlog}`. `artifacts/` is regenerable output and is not a source-of-truth for source facts.

| Scope | Configuration | Result | Conclusion bound |
|---|---|---|---|
| `ValidateSolutionConfiguration` | `Debug\|x64` | Succeeded | Proves only that solution configuration mapping can be resolved |
| `ValidateSolutionConfiguration` | `Debug\|Any CPU` | Succeeded | Proves only that solution configuration mapping can be resolved |
| Root `slnx` Restore + Rebuild | `Debug\|x64` | Succeeded: 3445 warnings, 0 errors | Full forced rebuild, exit code 0 |
| Root `slnx` Restore + Rebuild | `Debug\|Any CPU` | Succeeded: 3445 warnings, 0 errors | Full forced rebuild, exit code 0; solution project mapping and native assets use x64 |
| `WindowsBase` | `Debug\|x64` | Restore/Build succeeded | `Accessibility.dll` resolved from the installed `Microsoft.WindowsDesktop.App.Ref/8.0.1/ref/net8.0`; the earlier `CS0234` is not currently reproducible |
| `PresentationFramework` | `Debug\|x64` | Restore/Build succeeded | Standalone project verification |
| `PresentationUI` | `Debug\|x64` | Restore/Build succeeded | Standalone project verification |
| `WindowsFormsIntegration` | `Debug\|x64` | Restore/Build succeeded | Standalone project verification |
| `DirectWriteForwarder` | `Debug\|x64` | Covered by the root solution forced rebuild | The final log still contains `D9035`, but it did not cause failure |
| `System.Printing` | Not run | Not built | Old errors are historical clues only and must not be treated as the current first error |

Final forced-rebuild commands:

- `msbuild Microsoft.Dotnet.Wpf.slnx -restore /t:Rebuild /p:Configuration=Debug /p:Platform=x64 /m:1 /nr:false /v:minimal`
- `msbuild Microsoft.Dotnet.Wpf.slnx -restore /t:Rebuild /p:Configuration=Debug "/p:Platform=Any CPU" /m:1 /nr:false /v:minimal`

The forced rebuild previously exposed that 7 theme reference assemblies depended on a pre-existing `PresentationFramework.dll`: printing-related cycle-breakers share that assembly name with the complete reference assembly, and `PresentationFramework.Royale-ref` selected an incomplete bridge and produced 53 `CS0234` errors. The 7 theme ref projects now use ref-to-ref project dependencies and explicitly compile against `$(ArtifactsObjDir)PresentationFramework-ref\$(WpfNativePlatform)\$(Configuration)\$(TargetFramework)\ref\PresentationFramework.dll`. A Trusted Builder clean build then exposed the same class of issue in runtime theme projects: conditional implementation-assembly references disappear from the project item list after `artifacts` is cleaned, MarkupCompile may load a same-named cycle-breaker, and fail because `LostFocusEventManager` cannot be resolved as known type 380. The 7 runtime theme projects now reduce `PresentationFramework.csproj` to build ordering only and unconditionally reference the complete implementation output. Trusted Builder re-verification is still required.

Warnings that may currently appear without causing the final dual-platform forced rebuild to fail:

- `NU1603`.
- `MSB3243`.
- Warnings that skip related scripts when Perl is missing.
- `D9035`.

The previously observed WpfDemo output-file lock did not reappear in the latest re-check. It remains a historical environment clue and is no longer the current first blocker.

## Landed capabilities

### Native and Builder

- A shared native-asset inventory has been implemented.
- Builder has implemented x64 and x86 paths.
- The NuGet framework-dependent consumption issue has been fixed. Builder propagates the unified assembly version `42.42.42.42424` to all x86/x64 WPF runtime assemblies. C++/CLI `DirectWriteForwarder` writes the same CLR version explicitly. A PE-metadata version-consistency gate runs before packing. `PresentationCore/ModuleInitializer.cs` no longer contains the manual app-local load or `NoInlining` workaround and keeps only normal initialization. The cleaned `WpfLab.WpfRuntime.1.0.0-cleanup-validation.nupkg` passed a real `dotnet build` + `dotnet run --no-build` matrix for .NET 8/9, win-x86/win-x64, and single-target/multi-target projects. Detailed evidence is in [09-directwrite-forwarder-resolution.md](09-directwrite-forwarder-resolution.md).
- Builder has registered a standalone `relay-pr` command. It uses Octokit 14.0.0 to read the source PR and, in an independent clone, performs fixed base/head SHA fetch, pure Patch application, local gates, exact-SHA push, and target PR create/reuse. If `GITHUB_TOKEN` is missing, the command exits before clone and remote writes. `--allow-untrusted-build` only controls whether local build validation runs in the Temp workspace; it is skipped by default and GitHub Actions is used instead.
- Local gates run in an isolated HOME/NuGet/AppData/TEMP environment and execute Builder Restore/Build, x64/x86 build and pack, exact nupkg `test-package`, and root `Debug|x64` Rebuild, then verify HEAD, tree, index, and tracked working tree before and after the build.
- `eng/Builder.Tests` and `eng/Builder.ProcessTestHelper` are included in the root `slnx`. Unit, process, and local bare-repository integration tests cover URL/remote parsing, sensitive environment, cancel/timeout, PR-ref fallback, pure Patch apply and conflict, exact-SHA push, lease races, GitHub Actions event/identity, artifact-comment formatting, workflow security contracts, and checkout newline-preservation contracts.
- Builder has registered `ci-build` and `comment-pr-artifacts` commands, moving GitHub Actions checkout-credential checks, event/merge identity validation, version and artifact calculation, build/pack gates, workflow-run/artifact queries, and idempotent comments into C#.
- `.github/workflows/build.yml` uses `pull_request_target`, checks out the trusted Builder at `github.sha` and the PR merge ref separately, then builds the tested checkout in a read-only job. Successfully produced `-test.*` NuGet packages are downloaded and pushed to NuGet.org and GitHub Packages by an isolated job that neither checks out nor executes PR content. Tag packages keep the tag's full semantic version, optionally stripping a `v` prefix. `.github/workflows/comment-pr-build-artifacts.yml` checks out only the trusted Builder at `github.sha`, does not check out the PR or download artifacts, and writes bot comments through Octokit.
- arm64 is not implemented.

### WpfDemo

- WpfDemo x64 consumption and deployment have been implemented.
- WpfDemo x64 command-line build and runtime probes have historical/existing verification reports.
- The current copy mode for culture resource assemblies has not been proven to preserve culture subdirectories. Same-named satellite assemblies may be flattened or overwritten.
- Visual Studio F5 has not been re-verified.
- WpfDemo x86, arm64, and Publish are not implemented.

### PresentationUI

- `PresentationUI` is configured with `InternalMarkupCompilation`.
- The current `artifacts` tree contains 4 `.g.cs` files.
- The corresponding 4 `.xaml.cs` files still declare base classes explicitly.
- Existing outputs do not prove stable generation from a clean state. Clean generation and explicit base-class rollback both remain to be verified.

### Cycle-breaker

- The root `slnx` includes 8 cycle-breaker projects.
- 7 of them have direct consumers.
- `PresentationFramework-System.Printing-impl-cycle` currently has no direct consumer; keep-or-remove is to be confirmed.

## Current open items

1. Confirm the actual Visual Studio load status of the 59 projects, and re-verify WpfDemo F5.
2. Verify and fix WpfDemo culture resource-assembly subdirectory deployment so same-named resources from different cultures are not overwritten.
3. Verify that PresentationUI's 4 `.g.cs` files can be regenerated from a clean output, then decide whether to roll back the explicit base classes in the 4 `.xaml.cs` files.
4. Build `System.Printing.vcxproj` and decide the fix and inclusion approach from the current first real error.
5. Determine the target-version boundary:
   - `System.Windows.Primitives` exists only in `origin` and is missing from the current repository. Whether to migrate it depends on the target-version boundary and must not be treated as a required migration.
   - Retention of the 5 `Extensions` projects depends on compatibility goals.
6. Decide whether `PresentationFramework-System.Printing-impl-cycle`, which has no direct consumer, still has retention value, and continue isolating same-named cycle-breakers from non-printing ref consumers.
7. Complete the target scope for product-test migration, WpfDemo x86, arm64, Publish, and legacy generator tools. Builder PR-relay tests have landed, but that does not mean WPF product-test migration is complete.
8. Run a real GitHub sample-PR end-to-end acceptance of `relay-pr` in a dedicated low-privilege/disposable environment, and verify same-repo PR, fork PR, failure, rerun, new-commit, and artifact-comment scenarios from the topic-document matrix.

## Verification bounds

- Root-`slnx` `Debug|x64` and `Debug|Any CPU` Restore + Rebuild succeeded in the current workspace. That conclusion does not generalize to other configurations, platforms, or Visual Studio design-time/F5 behavior.
- Local automated verification of Builder PR relay does not include a real GitHub push, PR creation, or untrusted external-PR build. Actions permissions, fork, and comment behavior still need real PR acceptance.
- The `DirectWriteForwarder` conflict was reproduced by a real framework-dependent NuGet consumption test. It must not be avoided by removing `Microsoft.WindowsDesktop.App` or switching to self-contained publish. Completion criteria: all in-repo WPF runtime assemblies and the C++/CLI forwarder use unified assembly version `42.42.42.42424`; consumer projects keep shared-framework dependencies; after `dotnet build`, `dotnet run --no-build` actually loads the forwarder from the output directory and verifies exact ABI, text shaping, MVID/SHA-256, and load path.
- Theme-ref and runtime-theme success currently depends on the explicit complete `PresentationFramework` reference bound. Until same-named printing cycle-breakers converge, do not remove that isolation and do not use conditional output references that disappear during clean project evaluation.
- Standalone project success does not replace root-`slnx` success. Incremental success does not replace forced-rebuild success.
- IDE enumeration of project paths does not prove that projects are loaded. Visual Studio load status and an actual build are required.
- Generated files already present in `artifacts` do not prove they can be reproduced from a clean state.
- Historical commands, old errors, and topic documents that were not re-run must not be promoted to current conclusions.
