# Later Phase Plan

Current facts and landed capabilities are in [00-overview.md](00-overview.md). This file defines later work only and does not keep completion history. Execute phases in order. When a phase's verification passes, start the next phase immediately.

## Phase 2: Verify Visual Studio project load and WpfDemo F5

### Goal

Confirm that projects declared in the root `slnx` actually load in Visual Studio, and verify the WpfDemo debug-launch path.

### Actions

1. Open the root `slnx` in Visual Studio and check each project for not loaded, load failed, or reload required.
2. Compare IDE status with the 57 solution-declared paths and record any difference and the first load error.
3. Set WpfDemo as the startup project under `Debug|x64` and press F5.
4. Verify dependency builds, app-local load sources, breakpoint hits, and process-exit behavior.
5. Check whether culture resource assemblies keep culture subdirectories, and fix collection rules that could flatten or overwrite same-named satellite assemblies.
6. Re-verify WpfDemo F5 under the solution `Any CPU` mapping. If it is unsupported, record the exact platform mapping or launch blocker.

### Completion criteria

- All 57 declared projects have an explicit Visual Studio load status. Failed loads are fixed or recorded as a reproducible blocker.
- WpfDemo `Debug|x64` F5 succeeds and can hit breakpoints in in-repo implementation code.
- Culture resource assemblies in WpfDemo output keep the correct subdirectories, with at least one repeatable localized-resource verification.
- `Any CPU` F5 has a conclusion from actual execution, not inferred from a command-line build.

### Risks

- Design-time build, the project system, and command-line MSBuild may behave differently.
- F5 may retrigger output-file locks or expose incremental build-order issues.

## Phase 3: Verify PresentationUI clean generation and converge patches

### Goal

Prove that `InternalMarkupCompilation` can stably generate the required `.g.cs` files from a clean state, and after sufficient evidence, converge the explicit base-class differences in the 4 `.xaml.cs` files.

### Actions

1. Record the paths of the existing 4 `.g.cs` files and their corresponding XAML, and confirm they are regenerable outputs.
2. Clean only regenerable PresentationUI-related outputs, then run a standalone Restore + Build.
3. Verify that all 4 `.g.cs` files are regenerated and contain the required base class, `InitializeComponent`, and other members.
4. After the generation chain is stable, roll back the explicit base-class patches in the corresponding `.xaml.cs` files one by one, and build-verify each change.
5. Compare the final difference against `origin`. Record a concrete reason for anything that cannot be rolled back. Do not hide generation failure behind old outputs.

### Completion criteria

- The 4 `.g.cs` files can be regenerated without depending on pre-existing `artifacts`.
- PresentationUI standalone Restore + Build succeeds.
- The 4 `.xaml.cs` explicit base classes have been rolled back, or each retained item has a currently reproducible technical reason.

### Risks

- Existing `.g.cs` files may come from an old build and do not prove the current configuration is complete.
- Rolling back base classes may expose markup-compile order, task-assembly loading, or same-named assembly-resolution issues.

## Phase 4: Build and include the System.Printing implementation

### Goal

Obtain a currently reproducible build conclusion for `System.Printing.vcxproj`, fix the real implementation gap, and include it in the root `slnx` after dependencies close.

### Actions

1. Independently Restore + Build `System.Printing.vcxproj` on the current toolchain and save the first real error. Old errors are investigation clues only.
2. Classify errors by source, C++/CLI, platform toolset, references, and cycle-breaker bounds.
3. After each error surface is resolved, rerun the standalone build. Do not introduce a large bridge in one step.
4. After the standalone build is stable, add the project to the root `slnx` and configure x64, x86, and Any CPU mappings. Leave arm64 mapping until the corresponding platform capability exists.
5. Rerun the relevant root-`slnx` configurations and confirm inclusion does not break the existing main chain.

### Completion criteria

- `System.Printing.vcxproj` builds independently in the target configuration.
- The project is directly included in the root `slnx`. Build success is not maintained by deleting projects.
- The root-`slnx` build covers this implementation project. Printing type identity and direct consumers use real output or have an explicit transitional bound.

### Risks

- C++/CLI with the current .NET, toolset, and platform combination may expose new compile or link gaps.
- Expanding stub APIs to pass quickly increases same-named type and assembly-identity risk.
- Inclusion may change the build order of existing printing-related projects.

## Phase 5: Determine target version and compatibility bounds

### Goal

Make evidenced migrate-or-exclude decisions for `System.Windows.Primitives` and `Extensions`, and avoid treating version differences as omissions.

### Actions

1. Define the repository's target WPF/.NET version, required API surface, and compatibility scenarios.
2. Compare `System.Windows.Primitives` ownership, consumers, and build entries across the current tree, `origin`, and the target version.
3. Inventory the purpose, consumers, target frameworks, and shipping needs of the 5 `Extensions` projects.
4. For each project, record a decision of "migrate and include", "keep but do not include in the root entry", or "outside the target bound", with evidence.
5. Schedule build and inclusion for projects that will be migrated. For excluded items, ensure there are no dangling references.

### Completion criteria

- Whether `System.Windows.Primitives` is migrated has an explicit version rationale. Unverified "must migrate" or "must not migrate" conclusions are no longer used.
- Each of the 5 `Extensions` projects has a compatibility goal, consumers, and disposition.
- The decision is consistent with the root-`slnx` inventory, shipping scope, and test scope.

### Risks

- An unclear target version can migrate too many old modules or omit compatibility APIs.
- Some projects may be consumed only indirectly by specific packaging, tests, or internal toolchains.

## Phase 6: Converge cycle-breakers

### Goal

After real implementations and project references gradually close, reduce or remove cycle-breakers that are no longer needed, while keeping a buildable topology.

### Actions

1. Build a list of direct consumers, exposed APIs, and replacement paths for the 8 cycle-breakers.
2. First investigate `PresentationFramework-System.Printing-impl-cycle`, which currently has no direct consumer, and confirm whether generation-time or indirect consumption exists.
3. Starting from leaf nodes, replace bridges with real project references or a smaller stable contract.
4. Remove only one dependency edge at a time, then verify the related standalone projects and the root `slnx`.
5. Delete cycle-breakers confirmed to have no use. For projects that cannot be removed, record the retention reason and exit condition.

### Completion criteria

- All 8 projects have an explicit consumer and retention status.
- Projects with no consumer and no generation-time use are removed, and the root `slnx` is updated.
- Remaining cycle-breakers contain only the minimum API needed to break an actual cycle.

### Risks

- Static search may miss reflection, generation tasks, or hard-coded output-path consumers.
- Replacing same-named assemblies may change compile-time type identity or runtime binding.

## Phase 7: Migrate tests

### Goal

Establish a runnable test baseline covering core managed projects, the WpfDemo consumption chain, and key native bounds.

### Actions

1. Inventory test projects, test infrastructure, data dependencies, and platform limits in the target version.
2. First migrate core unit tests that can run stably in the current x64 environment, then handle integration, graphics, and printing tests.
3. Include test projects in an appropriate solution or test entry. Do not treat test-only templates as product implementations.
4. Add repeatable verification for the WpfDemo runtime probe, PresentationUI generation, and printing type identity.
5. Record skipped tests with an explicit environment reason and recovery condition.

### Completion criteria

- There is a repeatable test command and result record.
- The core main chain has at least smoke verification. New or changed public behavior has corresponding tests.
- Skipped items are classified by platform or environment. Permanent skips must not hide product defects.

### Risks

- Upstream tests may depend on internal infrastructure, specific fonts, a graphics environment, printers, or desktop interaction.
- Test migration may expose behavior differences between the current implementation and the target version.

## Phase 8: Extend WpfDemo x86, arm64, Publish, and legacy generators

### Goal

After the x64/Any CPU main chain is stable, complete WpfDemo x86, end-to-end arm64, the publish flow, and any legacy generators that still need to be kept.

### Actions

1. Add WpfDemo x86 project configuration, solution mapping, runtime/native asset selection, and probe architecture assertions.
2. Parameterize the shared native inventory, Builder, WpfDemo, and solution platform mapping for arm64.
3. Implement and verify WpfDemo Publish, confirming managed, native, localized-resource, and runtime-config completeness.
4. Evaluate whether `OSVersionHelper.vcxproj` has been fully replaced by the binary approach.
5. Evaluate whether the two `ThemeGenerator.proj` files and `wpf-etw.proj` need to be built, modernized, or kept only as generator entries.
6. Establish reproducible commands for tools that must be kept. For projects that do not belong in the root product build, document an independent entry.

### Completion criteria

- WpfDemo x86 build, asset deployment, and runtime probe have actual verification.
- arm64 build, asset deployment, and runtime probe have actual verification, or there is explicit environment-blocker evidence.
- WpfDemo Publish output can start and is confirmed to load the target in-repo assets.
- `OSVersionHelper`, ThemeGenerator, and `wpf-etw` all have an explicit disposition and verification entry.

### Risks

- arm64 native binaries, the C++ toolchain, or NuGet assets may be incomplete.
- Publish trimming, self-contained settings, and runtime-asset selection may change WPF load behavior.
- Legacy generators may depend on Perl, internal MSBuild tasks, or an unavailable historical environment.
