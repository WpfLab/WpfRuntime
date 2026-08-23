using System.Reflection;
using MS.Internal.Markup;

namespace WpfReorganize.Builder.Tests;

public sealed class CompatibleAssemblyResolverTests
{
    [Fact]
    public void WhenRequestedVersionIsLowerThenHigherVersionAssemblyIsResolved()
    {
        var assemblyPath = typeof(CompatibleAssemblyResolverTests).Assembly.Location;
        var availableName = AssemblyName.GetAssemblyName(assemblyPath);
        var requestedName = CopyWithVersion(availableName, new Version(0, 0, 0, 0));
        using var context = CreateMetadataLoadContext(assemblyPath);

        var assembly = context.LoadFromAssemblyName(requestedName);

        Assert.Equal(availableName.Version, assembly.GetName().Version);
    }

    [Fact]
    public void WhenRequestedVersionIsHigherThenLowerVersionAssemblyIsRejected()
    {
        var assemblyPath = typeof(CompatibleAssemblyResolverTests).Assembly.Location;
        var availableName = AssemblyName.GetAssemblyName(assemblyPath);
        var requestedName = CopyWithVersion(availableName, new Version(availableName.Version!.Major + 1, 0, 0, 0));
        using var context = CreateMetadataLoadContext(assemblyPath);

        Assert.Throws<FileNotFoundException>(() => context.LoadFromAssemblyName(requestedName));
    }

    [Fact]
    public void WhenPublicKeyTokenDiffersThenAssemblyIsRejected()
    {
        var assemblyPath = typeof(CompatibleAssemblyResolverTests).Assembly.Location;
        var availableName = AssemblyName.GetAssemblyName(assemblyPath);
        var requestedName = CopyWithVersion(availableName, availableName.Version!);
        requestedName.SetPublicKeyToken([1, 2, 3, 4, 5, 6, 7, 8]);
        using var context = CreateMetadataLoadContext(assemblyPath);

        Assert.Throws<FileNotFoundException>(() => context.LoadFromAssemblyName(requestedName));
    }

    private static MetadataLoadContext CreateMetadataLoadContext(string assemblyPath) =>
        new(new CompatibleAssemblyResolver([typeof(object).Assembly.Location, assemblyPath]), "System.Private.CoreLib");

    private static AssemblyName CopyWithVersion(AssemblyName source, Version version)
    {
        var result = new AssemblyName
        {
            Name = source.Name,
            Version = version,
            CultureName = source.CultureName,
        };
        result.SetPublicKeyToken(source.GetPublicKeyToken());
        return result;
    }
}
