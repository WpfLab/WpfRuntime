# Builder Build, Packaging, and Package Validation

## Document placement

This document describes the currently landed `eng/Builder` commands, service bounds, per-project builds, NuGet packing, and isolated consumption validation. The whole-repository project inventory and root-solution build status remain owned by [00-overview.md](00-overview.md). This document does not repeat or generalize the overall status of root `Microsoft.Dotnet.Wpf.slnx`.

Builder is a `net8.0` console project. Its own output is fixed at `eng/Builder/bin/` so cleaning `artifacts/` does not delete the running tool. It implements only Windows x64 and x86 build, pack, and validation paths. arm64 is not implemented.

## Command entry

Restore and build Builder first so `PackageDownload` assets are restored and MSBuild generates `eng/Builder/bin/PackagePaths.txt`:

```powershell
dotnet restore eng/Builder/Builder.csproj
dotnet build eng/Builder/Builder.csproj --no-restore
```

Available commands:

| Command | Purpose |
|---|---|
| `dotnet run --project eng/Builder/Builder.csproj --no-build -- --version 1.0.0` | Default build: clean `artifacts/bin`, `artifacts/obj`, and staging; build x64/x86 per project; collect assets; pack; generate a comparison report |
| `dotnet run --project eng/Builder/Builder.csproj --no-build -- clean` | Clean known build outputs. Detailed bounds are in [05-builder-clean.md](05-builder-clean.md) |
| `dotnet run --project eng/Builder/Builder.csproj --no-build -- compare` | Compare collected reference assemblies in staging with official `Microsoft.WindowsDesktop.App.Ref` for missing items and size differences. Complete a default Builder build first |
| `dotnet run --project eng/Builder/Builder.csproj --no-build -- test-package` | Select the newest package in `eng/Builder/bin/nupkg/` and run the isolated consumption matrix |
| `dotnet run --project eng/Builder/Builder.csproj --no-build -- test-package --package <nupkg-path>` | Validate an explicitly specified package |
| `dotnet eng/Builder/bin/Builder.dll ci-build --repository <tested-repository> --target solution` | Trusted GitHub Actions entry: recheck event and checkout identity, inspect leftover credentials, and rebuild the root solution in a sanitized environment |
| `dotnet eng/Builder/bin/Builder.dll ci-build --repository <tested-repository> --target package` | Trusted GitHub Actions entry: compute version and artifact identity, run full build/pack/validation, and write `GITHUB_OUTPUT` safely |
| `dotnet eng/Builder/bin/Builder.dll comment-pr-artifacts` | Trusted `workflow_run` entry: recheck run/PR/artifact metadata through Octokit and create or update a bot comment idempotently |

Commands locate the repository root by walking up from the Builder output directory until `.git` is found. If `--no-build` is used directly, the caller must ensure Builder is already built and `PackagePaths.txt` matches the current restore result.

`ci-build` and `comment-pr-artifacts` default to GitHub Actions variables `GITHUB_EVENT_PATH`, `GITHUB_EVENT_NAME`, `GITHUB_SHA`, `GITHUB_RUN_ID`, `GITHUB_RUN_ATTEMPT`, `GITHUB_REPOSITORY`, `GITHUB_OUTPUT`, and `GITHUB_STEP_SUMMARY`. The comment command also reads `GITHUB_TOKEN` from the environment and does not accept a token on the command line. When only metadata or trusted orchestration commands are needed, restore/build Builder with `-p:RestoreWpfRuntimePackages=false` to skip the WindowsDesktop `PackageDownload` required by the default build. The default remains `true`.

## Service split

| Component | Current responsibility |
|---|---|
| `Program.cs` | Registers default build, `clean`, `compare`, `test-package`, `relay-pr`, `ci-build`, and `comment-pr-artifacts` |
| `BuildService` | Orchestrates cleanup, per-project builds, asset collection, package validation, packing, and report comparison |
| `MsBuildService` | Finds `MSBuild.exe` through `vswhere`, `PATH`, and common Visual Studio install directories, and manages per-project diagnostic logs |
| `CleanService` | Cleans known outputs and skips/counts locked files or directories |
| `AssemblyCollector` | Collects reference and implementation assemblies from each project's own output directory. Implementation collection prefers the target-platform directory and finally allows a generic Debug-output fallback |
| `WpfRuntimeDefinition` | Reads managed-assembly and runtime NuGet dependency definitions from `eng/WpfRuntimeDependencies.props` and `eng/Versions.props` |
| `NuGetPackageService` | Resolves restored package paths, collects native assets, generates `buildTransitive` targets and nuspec, validates package assets, and packs |
| `CompareService` | Inventory- and size-level report comparison against official reference assemblies. It is not an API binary-compatibility proof |
| `PackageTestService` | Dynamically creates isolated consumer projects, publishes, validates package-asset hashes, `.deps.json` runtime registration, and `runtimeconfig.json` framework dependencies. Framework-dependent WPF apps must keep `Microsoft.NETCore.App` and `Microsoft.WindowsDesktop.App`, then run text-shaping and assembly-source WPF probes |
| `ProcessRunner` | Runs external processes, merges stdout and stderr, and timeout-kills probe execution |
| `GitHubActionsBuildService` | Trusted Builder validates tested-checkout credentials, event SHA/merge parents, and Git status, then runs solution or package gates in a sanitized isolated environment |
| `GitHubArtifactCommentService` | Paginates workflow-run, PR, artifact, and comment metadata through Octokit, decides the latest run, filters artifact identity, and writes bot comments idempotently |
| `GitHubWorkflowRunEvent` / `GitHubArtifactCommentFormatter` | Strictly parse event JSON and centrally generate markers, Markdown-safe text, sizes, and links |

## Visual Studio MSBuild dependency

Builder currently depends on Visual Studio MSBuild. The build inventory includes the C++/CLI project `DirectWriteForwarder.vcxproj`, so a Visual Studio or Visual Studio Build Tools environment with MSBuild and the corresponding C++ toolchain is required.

`MsBuildService` prefers `vswhere.exe` from Visual Studio Installer, then `PATH` and common install directories. CI also configures Visual Studio MSBuild through `microsoft/setup-msbuild`. Installing only the .NET SDK does not guarantee that the C++/CLI path is available.

## Per-project x64/x86 builds

The default build command does not invoke root `Microsoft.Dotnet.Wpf.slnx`. `BuildService` calls MSBuild one project at a time in a hard-coded dependency order:

1. First build `PresentationBuildTasks.csproj` for x64 and x86, targeting `net472`.
2. Then walk x64 and x86 separately, building core WPF, UIAutomation, `DirectWriteForwarder`, extension assemblies, and the seven theme projects.
3. All projects use `Debug`, `-restore`, `/m:1`, and `/nr:false`. After `PresentationBuildTasks` itself, later projects must use the prebuilt task assembly.
4. C# projects use the `x64` or `x86` platform. C++ projects map to `Win32` on the x86 path, so the current `DirectWriteForwarder` native platforms are `x64` and `Win32`.
5. Per-project diagnostic logs are written to `artifacts/log/Builder/<Project>-<Platform>.log`.

If a project build fails, Builder records the failure and continues the remaining projects so partial diagnostic results remain. If packing can still complete, the final exit code is `2`. Empty implementation-assembly collection for any RID, empty reference-assembly collection, or a missing hard-coded critical package asset fails immediately. That behavior must not interpret a partial pack as all projects having built successfully, and it does not prove that all non-critical assets were collected.

The current project build order is still maintained as arrays in `BuildService` and has not moved into a shared project graph or `eng/WpfRuntimeDependencies.props`. When adding, deleting, or renaming a packed project, check that hard-coded inventory at the same time.

## ref, RID implementation, and native asset collection

### Reference assemblies

`AssemblyCollector` filters `artifacts/bin/*-ref/` using the `RepoWpfRuntimeAssembly` inventory in `eng/WpfRuntimeDependencies.props`, prefers project primary DLLs in x64, AnyCPU, and generic Debug outputs, and writes:

```text
eng/Builder/bin/staging/ref/net8.0/
```

Assemblies marked `PackReference="false"` do not enter `ref/net8.0`.

### RID implementation assemblies

Implementation assemblies are also filtered by the shared `RepoWpfRuntimeAssembly` names and take only the primary DLL whose name matches the project directory, so transitive copies in the project directory cannot overwrite the correct output. Search order prefers the target-platform directory, then allows a generic Debug-output fallback:

```text
artifacts/bin/<Project>/x64/Debug/net8.0/<Project>.dll
artifacts/bin/<Project>/x64/Debug/<Project>.dll
artifacts/bin/<Project>/x86/Debug/net8.0/<Project>.dll
artifacts/bin/<Project>/x86/Debug/<Project>.dll
artifacts/bin/<Project>/Win32/Debug/<Project>.dll
artifacts/bin/<Project>/Debug/net8.0/<Project>.dll
artifacts/bin/<Project>/Debug/<Project>.dll
```

Collected results go to:

```text
eng/Builder/bin/staging/runtimes/win-x64/lib/net8.0/
eng/Builder/bin/staging/runtimes/win-x86/lib/net8.0/
```

x86 collection accepts `x86` then `Win32` outputs to cover managed versus C++/CLI platform naming. Because both x64 and x86 finally allow a generic-output fallback, placing assets in different RID directories does not by itself prove architecture isolation. Combine that with output architecture or runtime verification.

### Native assets

The current WindowsDesktop native version is fixed at `8.0.6` by `eng/WpfRuntimeDependencies.props`. `Builder.csproj` uses these `PackageDownload`s instead of `PackageReference GeneratePathProperty`:

- `Microsoft.WindowsDesktop.App.Runtime.win-x64`
- `Microsoft.WindowsDesktop.App.Runtime.win-x86`
- `Microsoft.WindowsDesktop.App.Ref`
- `Microsoft.NETCore.App.Host.win-x64`
- `Microsoft.NETCore.App.Host.win-x86`

Builder writes `PackagePaths.txt` from `$(NuGetPackageRoot)` and shared versions. Packing copies DLLs from `runtimes/<rid>/native/` of the two WindowsDesktop runtime packages and supplements `ijwhost.dll` from the matching host package.

Shared props already define `RepoWpfNativeRuntimeFile`, but Builder currently still copies every DLL in the runtime-package native directory and hard-codes `ValidatePackageAssets` checks for `ijwhost.dll`, `PenImc_cor3.dll`, `PresentationNative_cor3.dll`, and `wpfgfx_cor3.dll`. It also checks `ijwhost.dll` next to `DirectWriteForwarder.dll` in `lib/net8.0`. Builder's required native-file rules are therefore not fully unified with the shared inventory.

### C++/CLI host-dependency deployment convention

`ijwhost.dll` must be written to both `runtimes/<rid>/native/` and `runtimes/<rid>/lib/net8.0/`. `DirectWriteForwarder.dll` is a C++/CLI assembly loaded from the RID-specific `lib/net8.0` directory. NuGet's `native` asset classification affects asset selection and copy only; the Windows loader will not automatically search an adjacent `native` directory. If only the native-directory copy is kept, a consumer app may exit during WPF module initialization because it cannot resolve that indirect dependency.

Therefore, besides keeping the standard native-asset copy, Builder also places `ijwhost.dll` next to `DirectWriteForwarder.dll` and validates both locations before packing. Package consumption tests allow only this same-name exception for `ijwhost.dll` and require the runtime-lib and native copies to be byte-identical. Publish validation uses the runtime-lib copy as the baseline. Generated `buildTransitive` targets remove every existing `ijwhost.dll` publish item from `ResolvedFileToPublish` by file name before the SDK publish-file conflict check, then explicitly add the `lib/net8.0` copy as the only publish source. Do not delete by a concatenated package-file path, because SDK-generated publish-asset identities may be path-normalized. Without de-duplication, flattening two same-named assets to the publish root triggers `NETSDK1152`. Related C++/CLI runtime-load background is in [dotnet/runtime#38231](https://github.com/dotnet/runtime/issues/38231).

## NuGet package structure and consumption logic

The package ID is `WpfLab.WpfRuntime`. Current layout:

```text
WpfLab.WpfRuntime.<version>.nupkg
├─ ref/net8.0/*.dll
├─ runtimes/win-x64/lib/net8.0/*.dll (includes ijwhost.dll)
├─ runtimes/win-x64/native/*.dll (includes ijwhost.dll)
├─ runtimes/win-x86/lib/net8.0/*.dll (includes ijwhost.dll)
├─ runtimes/win-x86/native/*.dll (includes ijwhost.dll)
└─ buildTransitive/WpfLab.WpfRuntime.targets
```

The nuspec writes runtime-package dependency groups for `net8.0` and `net9.0`. Dependency versions come from `eng/WpfRuntimeDependencies.props` and `eng/Versions.props`. Implementation assemblies remain `net8.0` assets in RID directories. Public `lib/net8.0` does not carry those implementations. A generic-output fallback may put the same managed DLL into both RIDs. Directory layout alone does not prove different binary architectures.

`buildTransitive/WpfLab.WpfRuntime.targets` provides this consumption behavior:

- Remove the `Microsoft.WindowsDesktop.App.WPF` FrameworkReference.
- After reference resolution, remove selected same-named WPF references by file name and inject in-package `ref/net8.0`. The current implementation does not distinguish whether those references came from inbox, an explicit reference, or another package.
- When `RuntimeIdentifier` is `win-x64` or `win-x86`, select the matching managed implementations and native DLLs.
- After ordinary Build and Publish, copy RID assets into the application output directory.

### Naming convention for Builder and generated targets

Externally shipped packages, files, and diagnostic sources use the official name `WpfLab.WpfRuntime`. Private MSBuild properties, items, and targets that start with an underscore in generated targets do not mechanically concatenate organization and product names. They use a short `WpfRuntime` prefix, for example `_WpfRuntimeIdentifier`, `_WpfRuntimeReferenceDll`, and `RemoveInboxWpfReferencesForWpfRuntime`. Existing `_DotNetCampus...` and `...ForDotNetCampusWpfLib` names are legacy. Later generated-logic changes should migrate to this convention. Those internal names are not a compatibility contract.

The `DotNetCampus.Cli` namespace and `DotNetCampus.CommandLine` package name are the official names of the current third-party command-line dependency. They are outside repository or NuGet package renaming and should remain.

Before packing, Builder validates core ref, implementation, native, and `buildTransitive` files for both RIDs. Actual `dotnet pack` uses a minimal SDK project in the system temp directory so the temporary pack project does not inherit repository-root build imports. Generated packages are written to `eng/Builder/bin/nupkg/`.

At the end of the build, Builder also compares official `Microsoft.WindowsDesktop.App.Ref` in report mode. That comparison only checks inventory gaps and significant size differences. Report mode does not fail the full build command and cannot replace API, load, or runtime verification. When running `compare` independently, first ensure `staging/ref/net8.0` was produced by a complete Builder build. The current no-staging fallback selects only one directory from collection results and may produce an incomplete report.

## PackageTestApp isolated consumption template

`eng/Builder/PackageTestApp/PackageTestApp.csproj` is a dynamic isolated-consumption template, not a main-chain WPF implementation project, so it is not directly included in root `Microsoft.Dotnet.Wpf.slnx`. `Builder.csproj` includes the template files only as content. `PackageTestService` copies the template into a new `eng/Builder/bin/package-tests/<timestamp>-<id>/` directory on each validation, then dynamically changes the target framework, assembly name, and package version under test.

The isolated directory also generates independent:

- `global.json`: selects the .NET 9 SDK and allows rolling forward to a higher major version.
- `NuGet.Config`: explicitly configures only the package-under-test directory and nuget.org.
- `restore-packages/`: isolated from the repository's ordinary restore directory.
- `extracted-package/`: used to compare published files against assets inside the nupkg file by file.

The template creates a window, loads XAML controls and resources, raises routed events, and confirms that `WindowsBase`, `PresentationCore`, and `PresentationFramework` load from the publish directory. The probe also requires those in-package WPF implementation assemblies to remain `.NETCoreApp,Version=v8.0` even when the consumer targets .NET 9.

## Package-validation matrix

`test-package` dynamically creates three consumer projects:

| Project | Target framework |
|---|---|
| `SingleNet8` | `net8.0-windows` |
| `SingleNet9` | `net9.0-windows` |
| `MultiTarget` | `net8.0-windows;net9.0-windows` |

The validation service first checks the net8.0/net9.0 dependency groups and shared runtime-package versions in the nuspec. Then each target framework runs self-contained Publish for `win-x86` and `win-x64`, forming eight publish-and-run combinations. Each combination:

1. Validates that the publish directory contains the primary DLL of every runtime NuGet dependency.
2. SHA-256-compares the matching RID implementation and native DLLs from the package with the publish directory, file by file.
3. Starts the published WPF app and verifies XAML, resources, controls, events, and assembly load sources.
4. Sets a 30-second timeout per probe. Timeout kills the entire process tree. A non-zero exit code is a failure.

This matrix validates the isolated consumption contract of the generated package. It is not equivalent to all root-solution projects, Visual Studio F5, non-self-contained publish, or arm64 verification.

## CI path

`.github/workflows/build.yml` contains two independent Windows jobs:

- `build-solution`: checks out the trusted Builder at `github.sha` and the tested commit/PR merge ref separately; lightly builds the trusted Builder; then `ci-build --target solution` rechecks identity and Rebuilds the tested checkout as root `Debug|x64`.
- `build-package`: uses the same trusted/tested dual checkout; `ci-build --target package` restores and builds the tested Builder in a sanitized environment, runs the default x64/x86 build and pack, validates the exact nupkg, and produces artifact names and absolute package paths.

The YAML of both jobs keeps only pinned-version Actions, environment setup, and a one-line Builder invocation. It no longer maintains PowerShell identity calculation or build scripts. If any earlier `build-package` step fails, a `failure()` diagnostic step tries to upload `eng/Builder/bin/package-tests` from the tested checkout. When build and package tests both succeed, later steps upload the nupkg using the exact path output by C#. Having these steps configured in the workflow does not mean any local workspace or the latest remote run already passed. Actual conclusions must come from the corresponding run logs and artifacts.

`.github/workflows/comment-pr-build-artifacts.yml` checks out only the trusted Builder at `github.sha`. It does not check out a PR ref and does not download artifacts. It lightly builds Builder on Ubuntu, then calls `comment-pr-artifacts` with a one-line command. Only that final command step receives `GITHUB_TOKEN`. Restore/Build steps do not hold a write token.

## Current verification bounds

- Builder has implemented x64 and x86. arm64 has no PackageDownload, build loop, package directory, or test-matrix implementation.
- The C++/CLI path depends on Visual Studio MSBuild, a `vswhere`-discoverable install, and the matching C++ toolchain. A usable `dotnet` SDK is not enough to infer that Builder can run.
- The default Builder build uses its own hard-coded per-project inventory and does not represent the full build status of root `Microsoft.Dotnet.Wpf.slnx`. Whole-repository status is only in [00-overview.md](00-overview.md).
- The native runtime version is currently `8.0.6`. Paths come from `PackageDownload`, `$(NuGetPackageRoot)`, and `PackagePaths.txt`. `GeneratePathProperty` is not used.
- Managed assembly names and runtime-package dependencies already read shared definitions. Per-project build order, native full-copy, and critical native-file validation still have Builder-internal hard-coding and have not fully converged onto the shared inventory.
- `compare` is an inventory/size report. It does not prove API compatibility, strong-name consistency, or runtime behavior. Fallback results without complete staging also cannot be used as a complete-inventory basis.
- When `--package` is not passed, `test-package` selects only the last-written package in the output directory. Confirm the validation target from package path and timestamp.
- When this workspace was inspected, `eng/Builder/bin/nupkg/` and `eng/Builder/bin/package-tests/` did not exist, so this document does not claim that a full-matrix package validation just completed. When current evidence is needed, regenerate the package, run `test-package`, and keep the corresponding logs and artifacts.
