// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// ------------------------------------------------------------------------------
// Changes to this file must follow the http://aka.ms/api-review process.
// ------------------------------------------------------------------------------

namespace System.Windows.Documents
{
    using System.Collections.Generic;

    public sealed partial class FixedDocument : IDocumentPaginatorSource
    {
        public System.Windows.Threading.Dispatcher Dispatcher { get { throw null; } }

        public bool IsInitialized { get { throw null; } }

        public IEnumerable<PageContent> Pages => throw null;

        public DocumentPaginator DocumentPaginator => throw null;
    }
}
