// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace MS.Internal.Markup
{
    internal sealed class CompatibleAssemblyResolver : MetadataAssemblyResolver
    {
        private static readonly HashSet<string> WpfContractAssemblyNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "PresentationCore",
            "PresentationFramework",
            "System.Xaml",
            "System.Windows.Controls.Ribbon",
            "System.Windows.Input.Manipulations",
            "UIAutomationClient",
            "UIAutomationClientSideProviders",
            "UIAutomationProvider",
            "UIAutomationTypes",
            "WindowsBase",
            "WindowsFormsIntegration",
        };

        private readonly IReadOnlyList<AssemblyCandidate> _candidates;

        internal CompatibleAssemblyResolver(IEnumerable<string> assemblyPaths)
        {
            _candidates = assemblyPaths
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(path => new AssemblyCandidate(path, AssemblyName.GetAssemblyName(path)))
                .ToList();
        }

        public override Assembly? Resolve(MetadataLoadContext context, AssemblyName assemblyName)
        {
            AssemblyCandidate? candidate = _candidates
                .Where(candidate => IsCompatibleReference(assemblyName, candidate.Name))
                .OrderBy(candidate => candidate.Name.Version)
                .FirstOrDefault();

            if (candidate == null)
            {
                return null;
            }

            return context.LoadFromAssemblyPath(candidate.Path);
        }

        internal static bool IsCompatibleReferenceForTest(AssemblyName requested, AssemblyName candidate) =>
            IsCompatibleReference(requested, candidate);

        private static bool IsCompatibleReference(AssemblyName requested, AssemblyName candidate)
        {
            return string.Equals(requested.Name, candidate.Name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(NormalizeCulture(requested.CultureName), NormalizeCulture(candidate.CultureName), StringComparison.OrdinalIgnoreCase)
                && (IsWpfContractAssembly(requested.Name)
                    || PublicKeyTokenMatches(requested.GetPublicKeyToken(), candidate.GetPublicKeyToken()))
                && (requested.Version == null || candidate.Version >= requested.Version);
        }

        private static bool IsWpfContractAssembly(string? assemblyName) =>
            assemblyName != null && WpfContractAssemblyNames.Contains(assemblyName);

        private static string NormalizeCulture(string? cultureName) =>
            string.IsNullOrEmpty(cultureName) ? CultureInfo.InvariantCulture.Name : cultureName;

        private static bool PublicKeyTokenMatches(byte[]? requested, byte[]? candidate)
        {
            if (requested == null || requested.Length == 0)
            {
                return true;
            }

            return candidate != null && requested.SequenceEqual(candidate);
        }

        private sealed class AssemblyCandidate
        {
            internal AssemblyCandidate(string path, AssemblyName name)
            {
                Path = path;
                Name = name;
            }

            internal string Path { get; }

            internal AssemblyName Name { get; }
        }
    }
}
