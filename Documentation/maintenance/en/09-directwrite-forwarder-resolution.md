# DirectWriteForwarder Framework-Dependent Load Issue

## Problem scope

This topic records the issue where `DirectWriteForwarder.dll` is not loaded from the application output directory when an ordinary WPF development project consumes the `WpfLab.WpfRuntime` NuGet package.

The target consumption scenario is:

- Project target framework is `net8.0-windows`.
- Output is produced with `dotnet build` as framework-dependent.
- The app is started with `dotnet run --no-build`.
- The project references the `WpfLab.WpfRuntime` NuGet package built by this repository.
- Runtime continues to depend on `Microsoft.WindowsDesktop.App`. Self-contained publish must not change the problem model.

## Original test defect

`PackageTestService` previously used `dotnet publish --self-contained true` and then launched the EXE from the publish directory. That test copies a complete runtime closure into the publish directory and cannot cover assembly-resolution behavior of ordinary developers using `dotnet build` and the shared framework.

Even if that test passed, it could not prove that a framework-dependent app would load the in-package `DirectWriteForwarder.dll`. Concluding from it that the `ModuleInitializer` load order was effective was wrong.

Current package tests instead:

1. Create a consumer project with an isolated NuGet source and isolated package cache.
2. Run `dotnet build` with `SelfContained=false` kept explicit.
3. Use the SDK default `bin/Release/<TFM>/<RID>/` output directory.
4. Run `dotnet run --no-build --no-restore`.
5. Verify managed-assembly actual load path, MVID, and SHA-256.
6. Verify the exact ABI of `MS.Internal.Text.TextInterface.TextAnalyzer.Itemize`.
7. Actually run `FormattedText` text shaping and XAML control creation.

## Reproduced behavior

In a real `net8.0-windows/win-x86` build/run scenario:

- The application output directory contains `DirectWriteForwarder.dll` from the NuGet package.
- The application `.deps.json` contains a runtime-asset entry for `DirectWriteForwarder.dll`.
- `WindowsBase.dll`, `PresentationCore.dll`, and `PresentationFramework.dll` load from the application output directory.
- `DirectWriteForwarder.dll` actually loads from the installed `Microsoft.WindowsDesktop.App/8.0.x` shared-framework directory.
- Later, when `PresentationCore` calls the new in-package ABI, a `MissingMethodException` for `TextAnalyzer.Itemize` may occur.

The problem is therefore not leftover old files, mixed NuGet caches, or a missing output file. It is assembly identity and unification behavior in the default framework-dependent load context.

## ModuleInitializer responsibility and capability bounds

`PresentationCore/ModuleInitializer.cs` still has normal WPF initialization responsibilities, including:

- Setting process DPI awareness early.
- Calling `DWriteLoader.LoadDWrite()` to initialize DirectWrite.
- Calling `MS.Internal.NativeWPFDLLLoader.LoadDwrite()` to trigger WPF native/C++/CLI component initialization.

Those initialization responsibilities are different from this assembly-identity conflict and should remain.

The later app-local assembly-load logic in the same file was an independent workaround:

- `LoadAppLocalDirectWriteForwarder()`.
- `AssemblyLoadContext.Default.LoadFromAssemblyPath(...)`.
- `NoInlining` helper methods and load-order adjustments added to make that call happen first.

That workaround cannot reliably override a same-identity assembly reference already satisfied by the shared framework. `NoInlining` can prevent the JIT from resolving a static dependency too early at method entry, but it cannot solve:

- Compatible identity formed by simple name, version, culture, and public-key token between the in-package and shared-framework assemblies.
- The default load context already choosing the shared-framework assembly to satisfy the reference.

After the unified assembly-version fix, `PresentationCore` references `DirectWriteForwarder, Version=42.42.42.42424`. The shared-framework `8.0.0.0` cannot satisfy that reference. The normal `.deps.json` and default load context should then choose the in-package forwarder in the application output directory, without a manual path-based preload.

The final convergence goal is therefore: keep normal DPI, DirectWrite, and native initialization; delete the workaround that existed only to preload an app-local assembly. After deletion, the real framework-dependent NuGet consumption tests must be rerun. Only if load path, ABI, and text shaping still pass can the workaround be confirmed safe to remove.

## DirectWriteForwarder version defect

It is confirmed that `$(AssemblyVersion)` exists during `DirectWriteForwarder.vcxproj` evaluation, but a C++/CLI project does not automatically generate a managed `AssemblyVersionAttribute` the way an SDK-style C# project does.

Without generating that attribute explicitly, the produced `DirectWriteForwarder.dll` assembly version is `0.0.0.0`. That is a build-chain defect, not intended design.

Two diagnostic experiments were run:

- Hard-coded `AssemblyVersion("8.0.0.1")`: net8 x86/x64 build/run could load the app-local forwarder and pass shaping, but that version was not connected to the repository unified-version system. It only proved assembly identity was the root cause and is not a final implementation.
- Ordinary `$(AssemblyVersion)`, which is `8.0.0.0`: real build/run still loaded the shared-framework forwarder, because the shared-framework version is also `8.0.0.0` and the identity conflict remained.

Those experiments have been reverted.

## Unified isolation-version requirement

In-repo WPF runtime assemblies should use a unified isolation assembly version:

`42.42.42.42424`

The current implementation uses this unified version chain:

1. Builder passes `42.42.42.42424` into all runtime project builds through a standalone `WpfRuntimeAssemblyVersion` property, not mixed with the NuGet package version.
2. Root `Directory.Build.targets` maps `WpfRuntimeAssemblyVersion` to `AssemblyVersion` after Arcade props evaluation and before assembly-attribute generation.
3. SDK-style managed projects write `AssemblyVersionAttribute` through the normal assembly-attribute generation flow.
4. C++/CLI `DirectWriteForwarder` receives the same `WpfRuntimeAssemblyVersion` through a preprocessor macro and explicitly generates a managed `AssemblyVersionAttribute` in `OtherAssemblyAttrs.cpp`, avoiding a fallback to `0.0.0.0`.
5. Before packing, Builder reads the actual CLR version of every collected x86/x64 runtime assembly from PE metadata and stops packing if any assembly is not `42.42.42.42424`.
6. Consumer probes also check actual load path, assembly version, MVID, SHA-256, `TextAnalyzer.Itemize` ABI, text shaping, and XAML control creation.

NuGet semantic version and CLR assembly version are different concepts. Builder's `--version` argument continues to control the NuGet package version. `42.42.42.42424` controls in-repo runtime-assembly identity isolation and must not be derived directly from an arbitrary NuGet prerelease version string.

## Fixes not to adopt

The following are not acceptable as the final fix:

- Changing or downgrading the Arcade SDK in `global.json`.
- Adding special assembly-resolution logic that applies only to `Demo/WpfDemo`.
- Adding `SkipDirectWriteForwarderProjectReference` to bypass the NuGet consumption issue.
- Hard-coding a temporary version number in `OtherAssemblyAttrs.cpp`.
- Copying an app-local DLL without verifying the actual load location.
- Only checking that a runtime asset exists in `.deps.json`.
- Using self-contained publish results instead of framework-dependent build/run verification.

## MSBuild and `dotnet build` bounds

The repository itself contains C++/CLI projects. Full product builds should continue to use Visual Studio `MSBuild.exe` found by Builder. `dotnet build` uses Core MSBuild and cannot reliably host Visual C++ targets. Manually setting `VCTargetsPath` produces MSBuild API incompatibility when Visual C++ tasks load and is not a correct fix.

That does not affect NuGet consumption verification: when developers consume an already built NuGet package they should not build the in-repo vcxproj. Consumer projects must be able to use ordinary `dotnet build` and `dotnet run` directly.

## Current status and next steps

Currently completed:

- Real framework-dependent build/run tests can stably reproduce the original issue.
- It is confirmed that a same-identity forwarder may still be satisfied by the shared framework even when the app-local file exists and `.deps.json` already registers it.
- It is confirmed that neither `0.0.0.0` nor `8.0.0.0` can serve as the isolation assembly identity for this repository's package.
- Builder has propagated unified assembly version `42.42.42.42424` to all x86/x64 WPF runtime projects.
- `DirectWriteForwarder` explicitly writes the same C++/CLI managed assembly version.
- The pre-pack version gate has confirmed that all collected x86/x64 runtime assemblies are `42.42.42.42424`.
- `PresentationCore/ModuleInitializer.cs` has been restored to normal initialization logic and keeps only DPI awareness, `DWriteLoader.LoadDWrite()`, and `NativeWPFDLLLoader.LoadDwrite()`. The manual app-local load and `NoInlining` workaround have been removed.
- The cleaned and rebuilt `WpfLab.WpfRuntime.1.0.0-cleanup-validation.nupkg` has passed the framework-dependent consumption matrix.
- The consumption matrix covers .NET 8, .NET 9, win-x86, win-x64, single-target, and multi-target projects, and verifies app-local load, exact ABI, text shaping, and XAML controls.
- Builder unit tests currently pass 140 tests.

Current conclusion: unified assembly identity is the root fix. `ModuleInitializer` no longer owns an assembly-resolution workaround. Later changes must not reintroduce self-contained-only verification or a manual preload as a substitute for the framework-dependent build/run gate.
