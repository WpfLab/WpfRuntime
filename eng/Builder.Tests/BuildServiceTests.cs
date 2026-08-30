using System.Xml.Linq;
using WpfReorganize.Builder;

namespace WpfReorganize.Builder.Tests;

public sealed class BuildServiceTests
{
    [Fact]
    public void PresentationBuildTasksProjectTargetsNet472AndNet80()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectPath = Path.Join(
            repositoryRoot,
            "src",
            "Microsoft.DotNet.Wpf",
            "src",
            "PresentationBuildTasks",
            "PresentationBuildTasks.csproj");
        var document = XDocument.Load(projectPath);
        var targetFrameworks = document.Descendants()
            .Single(element => element.Name.LocalName == "TargetFrameworks")
            .Value;

        Assert.Equal("net472;net8.0", targetFrameworks);
    }

    [Fact]
    public void StrongNameIdentityMatchesOriginalWpfProjectClassification()
    {
        var repositoryRoot = FindRepositoryRoot();
        var directoryBuildProps = XDocument.Load(Path.Join(repositoryRoot, "Directory.Build.props"));
        var strongNameImport = directoryBuildProps.Descendants()
            .Single(element =>
                element.Name.LocalName == "Import" &&
                string.Equals((string?)element.Attribute("Project"), @"eng\WpfStrongName.props", StringComparison.Ordinal));
        var strongNameDocument = XDocument.Load(Path.Join(repositoryRoot, (string)strongNameImport.Attribute("Project")!));
        var ecmaProjects = strongNameDocument.Descendants()
            .Single(element => element.Name.LocalName == "_WpfEcmaStrongNameProjects")
            .Value;

        Assert.Contains("System.Xaml;", ecmaProjects, StringComparison.Ordinal);
        Assert.DoesNotContain("PresentationFramework;", ecmaProjects, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Windows.Controls.Ribbon;", ecmaProjects, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Windows.Presentation;", ecmaProjects, StringComparison.Ordinal);
    }

    [Fact]
    public void RibbonStrongNameIdentityMatchesFriendAssemblyContract()
    {
        var repositoryRoot = FindRepositoryRoot();
        var refAssemblyAttributes = File.ReadAllText(Path.Join(
            repositoryRoot,
            "src",
            "Microsoft.DotNet.Wpf",
            "src",
            "Shared",
            "RefAssemblyAttrs.cs"));

        Assert.Contains(
            "SystemWindowsControlsRibbon = \"System.Windows.Controls.Ribbon, PublicKey=\" + WCP_PUBLIC_KEY_STRING",
            refAssemblyAttributes,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SystemWindowsPresentationStrongNameIdentityMatchesFriendAssemblyContract()
    {
        var repositoryRoot = FindRepositoryRoot();
        var refAssemblyAttributes = File.ReadAllText(Path.Join(
            repositoryRoot,
            "src",
            "Microsoft.DotNet.Wpf",
            "src",
            "Shared",
            "RefAssemblyAttrs.cs"));

        Assert.Contains(
            "SystemWindowsPresentation = \"System.Windows.Presentation, PublicKey=\" + WCP_PUBLIC_KEY_STRING",
            refAssemblyAttributes,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PackagePublishEnablesWpfReferenceDiagnostics()
    {
        var arguments = PackageTestService.GetPublishArguments(
            "PackageTestApp.csproj",
            "net9.0-windows",
            "win-x86",
            "NuGet.Config",
            "packages",
            "publish");

        Assert.Contains("--property:WpfRuntimeReferenceDiagnostics=true", arguments, StringComparison.Ordinal);
        Assert.Contains("--property:GenerateTemporaryTargetAssemblyDebuggingInformation=true", arguments, StringComparison.Ordinal);
        Assert.Contains("--self-contained false", arguments, StringComparison.Ordinal);
    }

    [Fact]
    public void PackagePublishAcceptsWindowsDesktopSharedFrameworkDependency()
    {
        var publishDirectory = Path.Join(Path.GetTempPath(), $"builder-runtimeconfig-{Guid.NewGuid():N}");
        Directory.CreateDirectory(publishDirectory);
        File.WriteAllText(
            Path.Join(publishDirectory, "PackageProbe.runtimeconfig.json"),
            """
            {
              "runtimeOptions": {
                "frameworks": [
                  { "name": "Microsoft.NETCore.App", "version": "8.0.0" },
                  { "name": "Microsoft.WindowsDesktop.App", "version": "8.0.0" }
                ]
              }
            }
            """);

        PackageTestService.ValidatePublishedFrameworkDependencies(
            publishDirectory,
            "PackageProbe",
            "net8.0-windows",
            "win-x64");
    }

    [Fact]
    public void PackagePublishRejectsMissingWindowsDesktopSharedFrameworkDependency()
    {
        var publishDirectory = Path.Join(Path.GetTempPath(), $"builder-runtimeconfig-{Guid.NewGuid():N}");
        Directory.CreateDirectory(publishDirectory);
        File.WriteAllText(
            Path.Join(publishDirectory, "PackageProbe.runtimeconfig.json"),
            """
            {
              "runtimeOptions": {
                "framework": {
                  "name": "Microsoft.NETCore.App",
                  "version": "8.0.0"
                }
              }
            }
            """);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PackageTestService.ValidatePublishedFrameworkDependencies(
                publishDirectory,
                "PackageProbe",
                "net8.0-windows",
                "win-x64"));

        Assert.Contains("must retain Microsoft.WindowsDesktop.App", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PackagePublishRejectsMissingDirectWriteForwarderRuntimeDependency()
    {
        var publishDirectory = Path.Join(Path.GetTempPath(), $"builder-deps-{Guid.NewGuid():N}");
        Directory.CreateDirectory(publishDirectory);
        File.WriteAllText(
            Path.Join(publishDirectory, "PackageProbe.deps.json"),
            """
            {
              "targets": {
                ".NETCoreApp,Version=v8.0/win-x64": {
                  "PackageProbe/1.0.0": {
                    "runtime": {
                      "PackageProbe.dll": {}
                    }
                  }
                }
              }
            }
            """);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PackageTestService.ValidatePublishedRuntimeDependencies(
                publishDirectory,
                "PackageProbe",
                "net8.0-windows",
                "win-x64"));

        Assert.Contains("must contain DirectWriteForwarder.dll", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PackagePublishAcceptsDirectWriteForwarderRuntimeDependency()
    {
        var publishDirectory = Path.Join(Path.GetTempPath(), $"builder-deps-{Guid.NewGuid():N}");
        Directory.CreateDirectory(publishDirectory);
        File.WriteAllText(
            Path.Join(publishDirectory, "PackageProbe.deps.json"),
            """
            {
              "targets": {
                ".NETCoreApp,Version=v8.0/win-x64": {
                  "DirectWriteForwarder/0.0.0.0": {
                    "runtime": {
                      "DirectWriteForwarder.dll": {
                        "assemblyVersion": "0.0.0.0",
                        "fileVersion": "0.0.0.0"
                      }
                    }
                  }
                }
              }
            }
            """);

        PackageTestService.ValidatePublishedRuntimeDependencies(
            publishDirectory,
            "PackageProbe",
            "net8.0-windows",
            "win-x64");
    }

    [Fact]
    public void RuntimeAssembliesAreBuiltInReleaseWithPortableSymbols()
    {
        var arguments = BuildService.GetRuntimeBuildArguments(
            "WindowsBase.csproj",
            "x86",
            "build.log");

        Assert.Contains(
            "/p:Configuration=Release /p:Platform=x86 /p:DebugSymbols=true /p:DebugType=portable",
            arguments,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("x64", "net472")]
    [InlineData("x64", "net8.0")]
    [InlineData("x86", "net472")]
    public void PresentationBuildTasksUsesSupportedHostPlatformAndExplicitTargetFramework(
        string outputPlatform,
        string targetFramework)
    {
        var arguments = BuildService.GetPresentationBuildTasksBuildArguments(
            "PresentationBuildTasks.csproj",
            outputPlatform,
            targetFramework,
            "build.log");

        Assert.Contains(
            $"/p:Configuration=Release /p:Platform=x64 /p:WpfNativePlatform={outputPlatform} /p:TargetFramework={targetFramework}",
            arguments,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PresentationBuildTasksOutputPathMatchesPackagingLayout()
    {
        var path = BuildService.GetPresentationBuildTasksOutputPath(
            "artifacts",
            "x64",
            "net8.0");

        Assert.Equal(
            Path.Join("artifacts", "bin", "PresentationBuildTasks", "x64", "Release", "net8.0", "PresentationBuildTasks.dll"),
            path);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Join(directory.FullName, "Microsoft.Dotnet.Wpf.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
