using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;

namespace WpfReorganize.Builder.Tests;

public sealed class WindowsBaseApiTests
{
    private static string Root
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Join(directory.FullName, "Microsoft.Dotnet.Wpf.slnx")))
                directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        }
    }

    [Fact]
    public void PublicContractMatchesPreMigrationAssembly()
    {
        var path = Path.Join(Root, "artifacts", "bin", "WindowsBase", "Release", "net8.0", "WindowsBase.dll");
        Assert.True(File.Exists(path), "Build WindowsBase Release/net8.0 before running this test.");
        var paths = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll")
            .Concat(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.dll"))
            .Where(p => Path.GetFileName(p) != "WindowsBase.dll").Append(path);
        using var context = new MetadataLoadContext(new Resolver(paths));
        var assembly = context.LoadFromAssemblyPath(path);
        var signatures = new List<string>();
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var type in assembly.GetTypes().Where(t => t.IsVisible))
        {
            signatures.Add($"T {type.FullName} {type.Attributes} : {type.BaseType} [{string.Join(",", type.GetInterfaces().Select(t => t.ToString()).Order())}]");
            foreach (var member in type.GetMembers(flags).Where(IsApi))
            {
                signatures.Add($"M {type.FullName} {member.MemberType} {member} {Details(member)}");
            }
        }
        foreach (var type in assembly.GetForwardedTypes())
            signatures.Add($"F {type.AssemblyQualifiedName}");
        var actual = string.Join("\n", signatures.Order(StringComparer.Ordinal)) + "\n";
        Assert.Equal(File.ReadAllText(Path.Join(Root, "eng", "Builder.Tests", "WindowsBase.PublicApi.txt")).Replace("\r\n", "\n"), actual);
    }

    [Fact]
    public void GeneratedInteropTypesAreInternalAndHaveNoStandaloneDependency()
    {
        using var stream = File.OpenRead(Path.Join(Root, "artifacts", "bin", "WindowsBase", "Release", "net8.0", "WindowsBase.dll"));
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        var generated = metadata.TypeDefinitions.Select(metadata.GetTypeDefinition)
            .Where(t => metadata.GetString(t.Namespace).StartsWith("Windows.Win32", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(generated);
        Assert.All(generated, t => Assert.Equal(TypeAttributes.NotPublic, t.Attributes & TypeAttributes.VisibilityMask));
        Assert.DoesNotContain(metadata.AssemblyReferences.Select(metadata.GetAssemblyReference),
            a => metadata.GetString(a.Name) is "WpfRuntime.Windows.Primitives" or "System.Windows.Primitives");
    }

    private sealed class Resolver(IEnumerable<string> paths) : MetadataAssemblyResolver
    {
        private readonly PathAssemblyResolver resolver = new(paths);

        public override Assembly? Resolve(MetadataLoadContext context, AssemblyName name)
        {
            var resolved = resolver.Resolve(context, name);
            if (resolved is not null) return resolved;
            var package = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages", name.Name!.ToLowerInvariant());
            var candidate = Directory.Exists(package) ? Directory.GetFiles(package, name.Name + ".dll", SearchOption.AllDirectories)
                .Where(p => p.Contains("net8.0") && !p.Contains("\\runtimes\\")).Order().LastOrDefault() : null;
            return candidate is null ? null : context.LoadFromAssemblyPath(candidate);
        }
    }

    private static bool Visible(MethodBase method) => method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;

    private static bool IsApi(MemberInfo member) => member switch
    {
        Type type => type.IsVisible,
        MethodBase method => Visible(method),
        FieldInfo field => field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly,
        PropertyInfo property => property.GetAccessors(true).Any(Visible),
        EventInfo evt => new[] { evt.GetAddMethod(true), evt.GetRemoveMethod(true), evt.GetRaiseMethod(true) }.Any(m => m is not null && Visible(m)),
        _ => false
    };

    private static string Details(MemberInfo member) => member switch
    {
        MethodBase method => $"{method.Attributes} {method.CallingConvention} {string.Join(";", method.GetParameters().Select(p => $"{p.Name}:{p.Attributes}:{p.RawDefaultValue}"))} {string.Join(";", (method is MethodInfo info ? info.GetGenericArguments() : Type.EmptyTypes).Select(t => $"{t.GenericParameterAttributes}:{string.Join(",", t.GetGenericParameterConstraints().Select(c => c.ToString()))}"))}",
        FieldInfo field => $"{field.Attributes} {(field.IsLiteral ? field.GetRawConstantValue() : null)}",
        PropertyInfo property => string.Join(";", property.GetAccessors(true).Select(m => $"{m.Name}:{m.Attributes}")),
        EventInfo evt => $"{evt.EventHandlerType} {evt.AddMethod?.Attributes} {evt.RemoveMethod?.Attributes}",
        _ => ""
    };
}
