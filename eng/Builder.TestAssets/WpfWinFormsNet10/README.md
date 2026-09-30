# .NET 10 WPF / WinForms package compatibility probe

This project is a fixture for `WpfWinFormsCompatibilityTests`, not part of the repository build graph. The driver copies it outside the repository to avoid inheriting the .NET 8 SDK and repository build settings, and creates its embedded splash image before building.

Prerequisites:
- Windows x64 with an interactive desktop, .NET 10 SDK and Windows Desktop runtime.
- A freshly built WpfLab.WpfRuntime NuGet package.
- Set `WPF_RUNTIME_TEST_PACKAGE` to the absolute path of that package.

Run the Builder.Tests test with filter `FullyQualifiedName~WpfWinFormsCompatibilityTests`. The `PackageIntegration` trait identifies tests requiring these prerequisites. Missing prerequisites fail rather than silently pass.

The driver uses an isolated package cache and NuGet source mapping to consume the exact local package, builds the fixture, and runs it with timeouts. Success requires exit code zero and the exact output line `WPF_WINFORMS_NET10_PASS`. Build and execution output and the retained temporary directory are included in test diagnostics.

The probe checks framework assembly locations and versions, absence of local copies of framework private DLLs, app-local WpfRuntime assembly loading, WinForms button behavior and handle creation, WPF handle creation, and SplashScreen display/close. Unhandled failures produce a nonzero exit code.
