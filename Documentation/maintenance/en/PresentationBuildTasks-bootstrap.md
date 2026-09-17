# PresentationBuildTasks On-Demand Build

## Scope

`PresentationBuildTasks.dll` is the in-repo MSBuild task assembly used for XAML/BAML and resource generation. This file only specifies host matching, location, bootstrap, and locked-output handling. Whole-repository build status is owned by [00-overview.md](00-overview.md).

Implementation sources:

- [`Microsoft.WinFX.targets`](../../../src/Microsoft.DotNet.Wpf/src/PresentationBuildTasks/Microsoft.WinFX.targets)
- [`PresentationBuildTasks.csproj`](../../../src/Microsoft.DotNet.Wpf/src/PresentationBuildTasks/PresentationBuildTasks.csproj)
- Root [`Directory.Build.targets`](../../../Directory.Build.targets)

## Host TFM and unique DLL path

`Microsoft.WinFX.targets` selects the task-assembly TFM from the current MSBuild host:

- `$(MSBuildRuntimeType) == Core`: `net8.0`
- other hosts: `net472`

Task loading and project output share one in-repo path:

`artifacts\bin\PresentationBuildTasks\$(WpfNativePlatform)\$(Configuration)\$(_PresentationBuildTasksTfm)\PresentationBuildTasks.dll`

Do not probe fallback DLLs from the GAC, Visual Studio install directories, WindowsDesktop SDK tools, other platform directories, or historical outputs. Do not use `artifacts\ide-bin` or extra copy paths.

## Bootstrap and failure behavior when missing

`BuildPresentationBuildTasksOnDemand` defaults to `true` when unset. When the unique DLL is missing and `PresentationBuildTasks.csproj` exists in the repository, `Microsoft.WinFX.targets` nested-builds that project before validating the task assembly, passing:

- the current `Configuration`
- `Platform=$(WpfNativePlatform)`
- `TargetFramework=$(_PresentationBuildTasksTfm)`
- `BuildPresentationBuildTasksOnDemand=false`

The last value prevents the nested build from retriggering the same bootstrap chain. Nested-build failures propagate immediately. If the project is missing, on-demand build is disabled, or the unique DLL is still missing after the nested build, `ValidatePresentationBuildTasksAssembly` reports an explicit error before markup compilation or main resource generation. It does not silently fall back to another task assembly.

## Switch responsibilities

| Switch | Default / current setting | Responsibility | Not responsible for |
|---|---|---|---|
| `BuildPresentationBuildTasksOnDemand` | Defaults to `true` in the in-repo `Microsoft.WinFX.targets` | Builds in-repo `PresentationBuildTasks.csproj` when the unique DLL is missing; nested builds pass `false` to prevent recursion | Does not change the DLL path and does not select an SDK or external task assembly |
| `ImportFrameworkWinFXTargets` | Usually unset; WpfDemo's in-repo consumption mode sets it to `true` | This is an inverted switch: `true` prevents the WindowsDesktop SDK from auto-importing its own `Microsoft.WinFX.targets`, so the project can import the in-repo version | Does not import the in-repo targets by itself, and does not build or locate `PresentationBuildTasks.dll` |
| `UsePrebuiltPresentationBuildTasks` | Off by default; Builder sets it to `true` after prebuilding the task DLL and then building other projects | Root `Directory.Build.targets` uses it to remove product-project `ProjectReference`s to `PresentationBuildTasks.csproj`, so each project does not reschedule the task project through a project reference | Does not mean an external DLL is used, does not change the unique load path, and does not replace missing-DLL validation. If on-demand build is also off and the DLL is missing, the build fails |

## Locked-output handling

Before `CoreCompile`, `PresentationBuildTasks.csproj` scans DLLs in the current `OutDir` and its subdirectories:

1. Try to delete old DLLs first.
2. If deletion fails with `IOException` or `UnauthorizedAccessException`, rename the original file to `<name>.locked.<process-id>.<guid>.dll`.
3. The same scope covers satellite DLLs in language resource subdirectories. Later compilation still writes to the same canonical output directory.

This policy does not terminate Visual Studio or MSBuild processes and has no IDE-specific output branch. If the rename itself fails and the file or directory has not already disappeared, the error continues to surface. Do not hide the problem by skipping cleanup.

## Verification bounds

- The rename-after-delete-failure logic for locked DLLs exists in the project file. Whether a full Visual Studio build is no longer affected by a locked task DLL is pending Visual Studio verification.
- Whether WpfDemo F5 stably uses the in-repo targets and unique task DLL through design-time build, incremental build, and debug launch is pending Visual Studio verification.
