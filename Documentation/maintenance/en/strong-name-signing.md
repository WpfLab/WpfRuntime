# WPF Strong-Name Identity

Assemblies produced by this repository must keep the same strong-name identity as the original `dotnet/wpf` repository. The identity map is maintained in [`eng/WpfStrongName.props`](../../../eng/WpfStrongName.props). Do not scatter project lists into `Directory.Build.props` or individual project files.

## Key sources

The repository uses public SNK files provided by `Microsoft.DotNet.Arcade.Sdk` for public signing:

- `MicrosoftShared` uses `tools/snk/35MSSharedLib1024.snk`, public key token `31bf3856ad364e35`.
- `ECMA` uses `tools/snk/ECMA.snk`, public key token `b77a5c561934e089`.

Public SNK files contain only the public-key material needed for public signing. Official shipping builds still receive final signatures from Microsoft's internal signing system. This repository does not store Microsoft private keys.

## Identity mapping

The mapping is based on `origin/wpf/eng/WpfArcadeSdk/tools/ShippingProjects.props` and `Signing.props`:

- Assemblies in the original WPF `UseMicrosoftSharedKeyId` list remain `MicrosoftShared`.
- Shipping/helper projects that are not in that list use `ECMA`.
- The current solution's ECMA project list is maintained in `eng/WpfStrongName.props`, including `System.Xaml`, `System.Windows.Input.Manipulations`, and their corresponding reference assemblies.
- In this reorganized repository, `System.Windows.Controls.Ribbon` and `System.Windows.Presentation` depend on `InternalsVisibleTo` declarations that use the MicrosoftShared public key. Those assemblies and their corresponding reference assemblies therefore remain MicrosoftShared. This is an evidenced deviation from the origin default classification.
- WPF-generated `_wpftmp` temporary projects use the same key as the original project name.

Key identity examples:

| Assembly | StrongNameKeyId | PublicKeyToken |
| --- | --- | --- |
| `System.Xaml` | `ECMA` | `b77a5c561934e089` |
| `PresentationFramework` | `MicrosoftShared` | `31bf3856ad364e35` |
| `PresentationCore` | `MicrosoftShared` | `31bf3856ad364e35` |
| `WindowsBase` | `MicrosoftShared` | `31bf3856ad364e35` |

## Change constraints

- When adding or restoring a WPF product project, first confirm its original signing identity against `origin/wpf`, and check the current repository's `InternalsVisibleTo` declarations, shared `BuildInfo` constants, and reorganization notes.
- Do not mechanically rewrite existing friend-identity contracts from the origin project list alone. Do not unify everything to ECMA or MicrosoftShared just to resolve a build conflict.
- A reference assembly and its corresponding runtime assembly must use the same identity.
- After changing the mapping, build the affected implementation projects and `-ref` projects, and run Builder tests.
- Consumer projects must dynamically remove all same-named inbox WPF references based on the union of in-package `ref` and managed runtime assets, then add the in-package references uniformly. Runtime-only WPF assemblies must also enter the replacement set. Do not maintain a handwritten removal list that can omit assemblies, and do not hide mixed references by changing assembly identity.
