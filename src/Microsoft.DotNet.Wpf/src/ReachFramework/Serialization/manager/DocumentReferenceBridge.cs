// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Windows.Documents
{
    using System;
    using System.Collections.Generic;

    public interface IFrameworkInputElement
    {
        string Name { get; }
    }

    public partial class DocumentReference : IDocumentPaginatorSource
    {
        public DocumentPaginator DocumentPaginator => throw new NotSupportedException();
    }

    public sealed partial class FixedDocument
    {
        public IEnumerable<PageContent> Pages => throw new NotSupportedException();
    }

    public sealed partial class FixedDocumentSequence
    {
        public IEnumerable<DocumentReference> References => throw new NotSupportedException();
    }

    public sealed partial class FixedPage
    {
        public double Width { get; set; }

        public double Height { get; set; }
    }

    public partial class PageContent
    {
    }
}
