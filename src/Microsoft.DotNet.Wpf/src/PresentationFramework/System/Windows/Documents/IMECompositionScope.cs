// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace System.Windows.Documents
{
    // SetText cannot report implicit block boundaries while the IME holds a lock.
    // Track deferred edges here rather than walking the tree: replay can recreate
    // elements, and deleted elements may now belong to separate trees.
    internal sealed class IMECompositionScope
    {
        internal List<TextElement> DeferredElements { get; } = new List<TextElement>();
    }
}
