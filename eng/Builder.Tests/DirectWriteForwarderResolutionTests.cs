using System.IO.Compression;
using System.Text.Json;
using WpfReorganize.Builder;

namespace WpfReorganize.Builder.Tests;

public sealed class DirectWriteForwarderResolutionTests
{
    [Fact]
    public void FrameworkDependentWindowsDesktopPublishLoadsSharedFrameworkAssemblyDespiteAppLocalRuntimeAsset()
    {
        var workspace = CreateWorkspace(ProbeMode.NameBinding);

        var publishDirectory = PublishProbe(workspace);
        var loadedAssemblyPath = RunProbe(publishDirectory);

        Assert.StartsWith(GetWindowsDesktopSharedFrameworkDirectory(), loadedAssemblyPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AppLocalLoadInMethodWithDirectDependencyStillLoadsSharedFrameworkAssembly()
    {
        var workspace = CreateWorkspace(ProbeMode.SameMethodDependencyCall);

        var publishDirectory = PublishProbe(workspace);
        var loadedAssemblyPath = RunProbe(publishDirectory);

        Assert.StartsWith(GetWindowsDesktopSharedFrameworkDirectory(), loadedAssemblyPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AppLocalLoadBeforeNoInliningDependencyCallUsesAppLocalAssembly()
    {
        var workspace = CreateWorkspace(ProbeMode.NoInliningDependencyCall);

        var publishDirectory = PublishProbe(workspace);
        var loadedAssemblyPath = RunProbe(publishDirectory);

        Assert.Equal(Path.Join(publishDirectory, "DirectWriteForwarder.dll"), loadedAssemblyPath, ignoreCase: true);
    }

    private static ProbeWorkspace CreateWorkspace(ProbeMode probeMode)
    {
        var rootDirectory = Path.Join(
            Path.GetTempPath(),
            $"directwrite-forwarder-resolution-{Guid.NewGuid():N}");
        var assemblyDirectory = Path.Join(rootDirectory, "assembly");
        var packageDirectory = Path.Join(rootDirectory, "package");
        var packageSourceDirectory = Path.Join(rootDirectory, "packages");
        var probeDirectory = Path.Join(rootDirectory, "probe");
        Directory.CreateDirectory(assemblyDirectory);
        Directory.CreateDirectory(packageDirectory);
        Directory.CreateDirectory(packageSourceDirectory);
        Directory.CreateDirectory(probeDirectory);

        File.WriteAllText(
            Path.Join(assemblyDirectory, "DirectWriteForwarder.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <AssemblyName>DirectWriteForwarder</AssemblyName>
                <Version>0.0.0</Version>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(
            Path.Join(assemblyDirectory, "Marker.cs"),
            "namespace DirectWriteForwarderProbe; public static class Marker { public static string GetAssemblyLocation() => typeof(Marker).Assembly.Location; }");

        RunDotNet(
            assemblyDirectory,
            "build",
            "DirectWriteForwarder.csproj",
            "--configuration",
            "Release",
            "--nologo");

        var runtimeDirectory = Path.Join(packageDirectory, "runtimes", "win-x64", "lib", "net8.0");
        Directory.CreateDirectory(runtimeDirectory);
        var directWriteForwarderPath = Path.Join(
            assemblyDirectory,
            "bin",
            "Release",
            "net8.0",
            "DirectWriteForwarder.dll");
        File.Copy(directWriteForwarderPath, Path.Join(runtimeDirectory, "DirectWriteForwarder.dll"));
        File.Copy(directWriteForwarderPath, Path.Join(runtimeDirectory, "ijwhost.dll"));

        NuGetPackageService.GenerateBuildTransitiveFiles(packageDirectory);
        CreatePackage(packageDirectory, packageSourceDirectory);
        WriteProbeProject(probeDirectory);
        File.WriteAllText(Path.Join(probeDirectory, "probe-mode.txt"), probeMode.ToString());
        WriteNuGetConfig(rootDirectory, packageSourceDirectory);

        return new ProbeWorkspace(rootDirectory, probeDirectory);
    }

    private static string PublishProbe(ProbeWorkspace workspace)
    {
        var publishDirectory = Path.Join(workspace.RootDirectory, "publish");
        RunDotNet(
            workspace.ProbeDirectory,
            "publish",
            "Probe.csproj",
            "--configuration",
            "Release",
            "--framework",
            "net8.0-windows",
            "--runtime",
            "win-x64",
            "--self-contained",
            "false",
            "--configfile",
            Path.Join(workspace.RootDirectory, "NuGet.Config"),
            "--packages",
            Path.Join(workspace.RootDirectory, "restore-packages"),
            "--output",
            publishDirectory,
            "--nologo");

        Assert.True(File.Exists(Path.Join(publishDirectory, "DirectWriteForwarder.dll")));
        AssertDepsContainsDirectWriteForwarder(Path.Join(publishDirectory, "Probe.deps.json"));
        return publishDirectory;
    }

    private static string RunProbe(string publishDirectory)
    {
        var result = ProcessRunner.Run(
            new ProcessRunOptions("dotnet", publishDirectory, Path.Join(publishDirectory, "Probe.dll"))
            {
                Timeout = TimeSpan.FromSeconds(30),
            });

        Assert.Equal(0, result.ExitCode);
        return result.StandardOutput.Trim();
    }

    private static void RunDotNet(string workingDirectory, params string[] arguments)
    {
        var result = ProcessRunner.Run(new ProcessRunOptions("dotnet", workingDirectory, arguments));
        Assert.True(result.ExitCode == 0, result.Output);
    }

    private static void CreatePackage(string packageDirectory, string packageSourceDirectory)
    {
        File.WriteAllText(
            Path.Join(packageDirectory, "WpfLab.WpfRuntime.nuspec"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>WpfLab.WpfRuntime</id>
                <version>1.0.0-resolution-test</version>
                <authors>WpfLab</authors>
                <description>DirectWriteForwarder host resolution probe.</description>
              </metadata>
            </package>
            """);

        var packagePath = Path.Join(packageSourceDirectory, "WpfLab.WpfRuntime.1.0.0-resolution-test.nupkg");
        ZipFile.CreateFromDirectory(packageDirectory, packagePath);
    }

    private static void WriteProbeProject(string probeDirectory)
    {
        File.WriteAllText(
            Path.Join(probeDirectory, "Probe.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net8.0-windows</TargetFramework>
                <UseWPF>true</UseWPF>
                <UseAppHost>false</UseAppHost>
                <AssemblyName>Probe</AssemblyName>
              </PropertyGroup>
              <ItemGroup>
                <Content Include="probe-mode.txt" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="PreserveNewest" />
                <PackageReference Include="WpfLab.WpfRuntime" Version="1.0.0-resolution-test" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(
            Path.Join(probeDirectory, "Program.cs"),
            """
            using System;
            using System.IO;
            using System.Reflection;
            using System.Runtime.CompilerServices;
            using System.Runtime.Loader;
            using DirectWriteForwarderProbe;

            var mode = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "probe-mode.txt"));
            if (mode == "NameBinding")
            {
                Console.WriteLine(Assembly.Load("DirectWriteForwarder").Location);
                return;
            }

            try
            {
                if (mode == "SameMethodDependencyCall")
                    LoadAndCallDependencyInSameMethod();
                else
                    LoadBeforeDependencyCall();
            }
            catch (TypeLoadException)
            {
                Console.WriteLine(Assembly.Load("DirectWriteForwarder").Location);
            }
            catch (MissingMethodException)
            {
                Console.WriteLine(Assembly.Load("DirectWriteForwarder").Location);
            }

            static void LoadAndCallDependencyInSameMethod()
            {
                AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(AppContext.BaseDirectory, "DirectWriteForwarder.dll"));
                Console.WriteLine(Marker.GetAssemblyLocation());
            }

            static void LoadBeforeDependencyCall()
            {
                AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(AppContext.BaseDirectory, "DirectWriteForwarder.dll"));
                CallDependency();
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            static void CallDependency()
            {
                Console.WriteLine(Marker.GetAssemblyLocation());
            }
            """);
    }

    private static void WriteNuGetConfig(string rootDirectory, string packageSourceDirectory)
    {
        File.WriteAllText(
            Path.Join(rootDirectory, "NuGet.Config"),
            $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="resolution-test" value="{{packageSourceDirectory}}" />
              </packageSources>
            </configuration>
            """);
    }

    private static void AssertDepsContainsDirectWriteForwarder(string depsPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(depsPath));
        var containsDirectWriteForwarder = document.RootElement
            .GetProperty("targets")
            .EnumerateObject()
            .SelectMany(target => target.Value.EnumerateObject())
            .Any(library =>
                library.Value.TryGetProperty("runtime", out var runtimeAssets) &&
                runtimeAssets.EnumerateObject().Any(asset =>
                    string.Equals(Path.GetFileName(asset.Name), "DirectWriteForwarder.dll", StringComparison.OrdinalIgnoreCase)));

        Assert.True(containsDirectWriteForwarder);
    }

    private static string GetWindowsDesktopSharedFrameworkDirectory() =>
        Path.Join(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "dotnet",
            "shared",
            "Microsoft.WindowsDesktop.App");

    private enum ProbeMode
    {
        NameBinding,
        SameMethodDependencyCall,
        NoInliningDependencyCall,
    }

    private sealed record ProbeWorkspace(string RootDirectory, string ProbeDirectory);
}
