using WpfReorganize.Builder;

namespace WpfReorganize.Builder.Tests;

public sealed class BuildServiceTests
{
    [Fact]
    public void PresentationBuildTasksForX86UsesSupportedHostPlatformAndX86OutputSlot()
    {
        var arguments = BuildService.GetPresentationBuildTasksBuildArguments(
            "PresentationBuildTasks.csproj",
            "x86",
            "build.log");

        Assert.Contains(
            "/p:Platform=x64 /p:WpfNativePlatform=x86 /p:TargetFrameworks=\"net472;net8.0\"",
            arguments,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PresentationBuildTasksForX64RestoresAllTargetFrameworks()
    {
        var arguments = BuildService.GetPresentationBuildTasksBuildArguments(
            "PresentationBuildTasks.csproj",
            "x64",
            "build.log");

        Assert.Contains(
            "/p:Platform=x64 /p:WpfNativePlatform=x64 /p:TargetFrameworks=\"net472;net8.0\"",
            arguments,
            StringComparison.Ordinal);
    }
}
