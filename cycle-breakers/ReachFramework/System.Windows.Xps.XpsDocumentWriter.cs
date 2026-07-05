// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Windows.Xps
{
    /// <summary>
    /// Stub for XpsDocumentWriter. The full type lives in ReachFramework.dll.
    /// This cycle-breaker stub prevents CS0234 when ReachFramework-ref
    /// compiles its public API surface and the inbox ReachFramework has been
    /// removed by RemoveInboxWpfReference.
    /// </summary>
    public class XpsDocumentWriter
    {
    }
}