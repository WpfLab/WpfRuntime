// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// ------------------------------------------------------------------------------
// Changes to this file must follow the http://aka.ms/api-review process.
// ------------------------------------------------------------------------------

namespace System.Windows.Documents.Serialization
{
    public interface ISerializerFactory
    {
        string DefaultFileExtension { get; }
        string DisplayName { get; }
        string ManufacturerName { get; }
        System.Uri ManufacturerWebsite { get; }
        System.Windows.Documents.Serialization.SerializerWriter CreateSerializerWriter(System.IO.Stream stream);
    }

    public abstract partial class SerializerWriter
    {
        protected SerializerWriter() { }
        public abstract event System.Windows.Documents.Serialization.WritingCancelledEventHandler WritingCancelled;
        public abstract event System.Windows.Documents.Serialization.WritingCompletedEventHandler WritingCompleted;
        public abstract event System.Windows.Documents.Serialization.WritingPrintTicketRequiredEventHandler WritingPrintTicketRequired;
        public abstract event System.Windows.Documents.Serialization.WritingProgressChangedEventHandler WritingProgressChanged;
        public abstract void CancelAsync();
        public abstract System.Windows.Documents.Serialization.SerializerWriterCollator CreateVisualsCollator();
        public virtual System.Windows.Documents.Serialization.SerializerWriterCollator CreateVisualsCollator(System.Printing.PrintTicket documentSequencePT, System.Printing.PrintTicket documentPT) { throw null; }
        public abstract void Write(System.Windows.Documents.DocumentPaginator documentPaginator);
        public virtual void Write(System.Windows.Documents.DocumentPaginator documentPaginator, System.Printing.PrintTicket printTicket) { }
        public abstract void Write(System.Windows.Documents.FixedDocument fixedDocument);
        public virtual void Write(System.Windows.Documents.FixedDocument fixedDocument, System.Printing.PrintTicket printTicket) { }
        public abstract void Write(System.Windows.Documents.FixedDocumentSequence fixedDocumentSequence);
        public virtual void Write(System.Windows.Documents.FixedDocumentSequence fixedDocumentSequence, System.Printing.PrintTicket printTicket) { }
        public abstract void Write(System.Windows.Documents.FixedPage fixedPage);
        public virtual void Write(System.Windows.Documents.FixedPage fixedPage, System.Printing.PrintTicket printTicket) { }
        public abstract void Write(System.Windows.Media.Visual visual);
        public virtual void Write(System.Windows.Media.Visual visual, System.Printing.PrintTicket printTicket) { }
        public abstract void WriteAsync(System.Windows.Documents.DocumentPaginator documentPaginator);
        public abstract void WriteAsync(System.Windows.Documents.DocumentPaginator documentPaginator, object userState);
        public virtual void WriteAsync(System.Windows.Documents.DocumentPaginator documentPaginator, System.Printing.PrintTicket printTicket) { }
        public virtual void WriteAsync(System.Windows.Documents.DocumentPaginator documentPaginator, System.Printing.PrintTicket printTicket, object userState) { }
        public abstract void WriteAsync(System.Windows.Documents.FixedDocument fixedDocument);
        public abstract void WriteAsync(System.Windows.Documents.FixedDocument fixedDocument, object userState);
        public virtual void WriteAsync(System.Windows.Documents.FixedDocument fixedDocument, System.Printing.PrintTicket printTicket) { }
        public virtual void WriteAsync(System.Windows.Documents.FixedDocument fixedDocument, System.Printing.PrintTicket printTicket, object userState) { }
        public abstract void WriteAsync(System.Windows.Documents.FixedDocumentSequence fixedDocumentSequence);
        public abstract void WriteAsync(System.Windows.Documents.FixedDocumentSequence fixedDocumentSequence, object userState);
        public virtual void WriteAsync(System.Windows.Documents.FixedDocumentSequence fixedDocumentSequence, System.Printing.PrintTicket printTicket) { }
        public virtual void WriteAsync(System.Windows.Documents.FixedDocumentSequence fixedDocumentSequence, System.Printing.PrintTicket printTicket, object userState) { }
        public abstract void WriteAsync(System.Windows.Documents.FixedPage fixedPage);
        public abstract void WriteAsync(System.Windows.Documents.FixedPage fixedPage, object userState);
        public virtual void WriteAsync(System.Windows.Documents.FixedPage fixedPage, System.Printing.PrintTicket printTicket) { }
        public virtual void WriteAsync(System.Windows.Documents.FixedPage fixedPage, System.Printing.PrintTicket printTicket, object userState) { }
        public abstract void WriteAsync(System.Windows.Media.Visual visual);
        public abstract void WriteAsync(System.Windows.Media.Visual visual, object userState);
        public virtual void WriteAsync(System.Windows.Media.Visual visual, System.Printing.PrintTicket printTicket) { }
        public virtual void WriteAsync(System.Windows.Media.Visual visual, System.Printing.PrintTicket printTicket, object userState) { }
    }
}

namespace System.Windows.Xps
{
    public partial class XpsDocumentWriter
    {
        public XpsDocumentWriter(object document) { }

        public event System.Windows.Documents.Serialization.WritingPrintTicketRequiredEventHandler WritingPrintTicketRequired;
        public event System.Windows.Documents.Serialization.WritingProgressChangedEventHandler WritingProgressChanged;
        public event System.Windows.Documents.Serialization.WritingCompletedEventHandler WritingCompleted;
        public event System.Windows.Documents.Serialization.WritingCancelledEventHandler WritingCancelled;

        public void CancelAsync() { }
        public System.Windows.Documents.Serialization.SerializerWriterCollator CreateVisualsCollator() { throw null; }
        public System.Windows.Documents.Serialization.SerializerWriterCollator CreateVisualsCollator(System.Printing.PrintTicket documentSequencePT, System.Printing.PrintTicket documentPT) { throw null; }
        public void Write(System.Windows.Documents.DocumentPaginator documentPaginator) { }
        public void Write(System.Windows.Documents.DocumentPaginator documentPaginator, System.Printing.PrintTicket printTicket) { }
        public void Write(System.Windows.Documents.FixedDocument fixedDocument) { }
        public void Write(System.Windows.Documents.FixedDocument fixedDocument, System.Printing.PrintTicket printTicket) { }
        public void Write(System.Windows.Documents.FixedDocumentSequence fixedDocumentSequence) { }
        public void Write(System.Windows.Documents.FixedDocumentSequence fixedDocumentSequence, System.Printing.PrintTicket printTicket) { }
        public void Write(System.Windows.Documents.FixedPage fixedPage) { }
        public void Write(System.Windows.Documents.FixedPage fixedPage, System.Printing.PrintTicket printTicket) { }
        public void Write(System.Windows.Media.Visual visual) { }
        public void Write(System.Windows.Media.Visual visual, System.Printing.PrintTicket printTicket) { }
        public void WriteAsync(System.Windows.Documents.DocumentPaginator documentPaginator) { }
        public void WriteAsync(System.Windows.Documents.DocumentPaginator documentPaginator, object userState) { }
        public void WriteAsync(System.Windows.Documents.DocumentPaginator documentPaginator, System.Printing.PrintTicket printTicket) { }
        public void WriteAsync(System.Windows.Documents.DocumentPaginator documentPaginator, System.Printing.PrintTicket printTicket, object userState) { }
        public void WriteAsync(System.Windows.Documents.FixedDocument fixedDocument) { }
        public void WriteAsync(System.Windows.Documents.FixedDocument fixedDocument, object userState) { }
        public void WriteAsync(System.Windows.Documents.FixedDocument fixedDocument, System.Printing.PrintTicket printTicket) { }
        public void WriteAsync(System.Windows.Documents.FixedDocument fixedDocument, System.Printing.PrintTicket printTicket, object userState) { }
        public void WriteAsync(System.Windows.Documents.FixedDocumentSequence fixedDocumentSequence) { }
        public void WriteAsync(System.Windows.Documents.FixedDocumentSequence fixedDocumentSequence, object userState) { }
        public void WriteAsync(System.Windows.Documents.FixedDocumentSequence fixedDocumentSequence, System.Printing.PrintTicket printTicket) { }
        public void WriteAsync(System.Windows.Documents.FixedDocumentSequence fixedDocumentSequence, System.Printing.PrintTicket printTicket, object userState) { }
        public void WriteAsync(System.Windows.Documents.FixedPage fixedPage) { }
        public void WriteAsync(System.Windows.Documents.FixedPage fixedPage, object userState) { }
        public void WriteAsync(System.Windows.Documents.FixedPage fixedPage, System.Printing.PrintTicket printTicket) { }
        public void WriteAsync(System.Windows.Documents.FixedPage fixedPage, System.Printing.PrintTicket printTicket, object userState) { }
        public void WriteAsync(System.Windows.Media.Visual visual) { }
        public void WriteAsync(System.Windows.Media.Visual visual, object userState) { }
        public void WriteAsync(System.Windows.Media.Visual visual, System.Printing.PrintTicket printTicket) { }
        public void WriteAsync(System.Windows.Media.Visual visual, System.Printing.PrintTicket printTicket, object userState) { }
    }
}
