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
    public void WhenWpfContractUsesHistoricalPublicKeyTokenThenHigherVersionAssemblyIsResolved()
    {
        var assemblyPath = typeof(CompatibleAssemblyResolverTests).Assembly.Location;
        var availableName = AssemblyName.GetAssemblyName(assemblyPath);
        var requestedName = new AssemblyName
        {
            Name = "System.Xaml",
            Version = new Version(4, 0, 0, 0),
            CultureName = availableName.CultureName,
        };
        requestedName.SetPublicKeyToken(Convert.FromHexString("B77A5C561934E089"));
        var candidateName = CopyWithVersion(availableName, new Version(8, 0, 0, 0));
        candidateName.Name = "System.Xaml";

        Assert.True(CompatibleAssemblyResolver.IsCompatibleReferenceForTest(requestedName, candidateName));
    }

    [Fact]
    public void WhenNonWpfPublicKeyTokenDiffersThenAssemblyIsRejected()
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
