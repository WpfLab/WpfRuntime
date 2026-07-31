// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// ------------------------------------------------------------------------------
// Changes to this file must follow the http://aka.ms/api-review process.
// ------------------------------------------------------------------------------

namespace System.Windows.Xps.Packaging
{
    public interface IXpsFixedDocumentSequenceReader
    {
        System.Printing.PrintTicket PrintTicket { get; }

        System.Collections.ObjectModel.ReadOnlyCollection<IXpsFixedDocumentReader> FixedDocuments { get; }
    }

    public interface IXpsFixedDocumentReader
    {
        System.Printing.PrintTicket PrintTicket { get; }

        System.Collections.ObjectModel.ReadOnlyCollection<IXpsFixedPageReader> FixedPages { get; }
    }

    public interface IXpsFixedPageReader
    {
        System.Printing.PrintTicket PrintTicket { get; }
    }

    public partial class XpsDocument
    {
        public XpsDocument(System.IO.Packaging.Package package)
        {
        }

        public XpsDocument(string path, System.IO.FileAccess packageAccess)
        {
        }

        public System.Windows.Documents.FixedDocumentSequence GetFixedDocumentSequence()
        {
            return null;
        }

        public IXpsFixedDocumentSequenceReader FixedDocumentSequenceReader => null;

        public void Close()
        {
        }

        internal System.Windows.Xps.Serialization.PackageSerializationManager CreateSerializationManager(bool batchMode)
        {
            return null;
        }

        internal System.Windows.Xps.Serialization.PackageSerializationManager CreateAsyncSerializationManager(bool batchMode)
        {
            return null;
        }

        internal void DisposeSerializationManager()
        {
        }

        internal static void SaveWithUI(System.IntPtr parent, System.Uri source, System.Uri target)
        {
        }

        public static System.Windows.Xps.XpsDocumentWriter CreateXpsDocumentWriter(XpsDocument xpsDocument)
        {
            return null;
        }
    }
}
