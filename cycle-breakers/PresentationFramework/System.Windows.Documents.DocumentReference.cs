// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// ------------------------------------------------------------------------------
// Changes to this file must follow the http://aka.ms/api-review process.
// ------------------------------------------------------------------------------

namespace System.Windows.Documents
{
    public interface IFrameworkInputElement
    {
        string Name { get; }
    }

    public partial class Hyperlink : System.Windows.FrameworkElement
    {
        public System.Uri NavigateUri { get; set; }
    }

    public sealed partial class DocumentReference : System.Windows.Controls.Control, IDocumentPaginatorSource
    {
        public DocumentPaginator DocumentPaginator => throw null;

        public FixedDocument GetDocument(bool forceReload) { throw null; }
    }

    public partial class PageContent
    {
        public FixedPage GetPageRoot(bool forceReload) { throw null; }
    }

    public class DocumentReferenceCollection : System.Collections.ObjectModel.Collection<DocumentReference>
    {
    }

    public class PageContentCollection : System.Collections.ObjectModel.Collection<PageContent>
    {
    }
}
