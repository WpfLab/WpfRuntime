using WpfReorganize.Builder;

namespace WpfReorganize.Builder.Tests;

public sealed class CurrentPackageValidationTests
{
    [Fact]
    public void CurrentSourceBuildsAndPublishedPackagePassesRuntimeValidation()
    {
        var context = BuilderContext.Create();
        var version = $"1.0.0-validation.{DateTime.UtcNow:yyyyMMddHHmmss}";

        Assert.Equal(0, BuildService.Run(context, version));
        Assert.Equal(0, PackageTestService.Run(context, Path.Join(context.NupkgOutputDir, $"WpfLab.WpfRuntime.{version}.nupkg")));
    }
}
