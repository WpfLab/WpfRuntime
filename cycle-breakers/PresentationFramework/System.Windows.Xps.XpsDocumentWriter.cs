// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// ------------------------------------------------------------------------------
// Changes to this file must follow the http://aka.ms/api-review process.
// ------------------------------------------------------------------------------

namespace System.Windows.Xps
{
    public partial class XpsDocumentWriter : System.Windows.Documents.Serialization.SerializerWriter
    {
        internal XpsDocumentWriter() { }
        public override event System.Windows.Documents.Serialization.WritingCancelledEventHandler WritingCancelled { add { } remove { } }
        public override event System.Windows.Documents.Serialization.WritingCompletedEventHandler WritingCompleted { add { } remove { } }
        public override event System.Windows.Documents.Serialization.WritingPrintTicketRequiredEventHandler WritingPrintTicketRequired { add { } remove { } }
        public override event System.Windows.Documents.Serialization.WritingProgressChangedEventHandler WritingProgressChanged { add { } remove { } }
        public override void CancelAsync() { }
        public override System.Windows.Documents.Serialization.SerializerWriterCollator CreateVisualsCollator() { throw null; }
        public override System.Windows.Documents.Serialization.SerializerWriterCollator CreateVisualsCollator(System.Printing.PrintTicket documentSequencePrintTicket, System.Printing.PrintTicket documentPrintTicket) { throw null; }
        public virtual void raise_WritingCancelled(object sender, System.Windows.Documents.Serialization.WritingCancelledEventArgs args) { }
        public virtual void raise_WritingCompleted(object sender, System.Windows.Documents.Serialization.WritingCompletedEventArgs e) { }
        public virtual void raise_WritingPrintTicketRequired(object sender, System.Windows.Documents.Serialization.WritingPrintTicketRequiredEventArgs e) { }
        public virtual void raise_WritingProgressChanged(object sender, System.Windows.Documents.Serialization.WritingProgressChangedEventArgs e) { }
        public void Write(string documentPath) { }
        public void Write(string documentPath, System.Windows.Xps.XpsDocumentNotificationLevel notificationLevel) { }
        public override void Write(System.Windows.Documents.DocumentPaginator documentPaginator) { }
        public override void Write(System.Windows.Documents.DocumentPaginator documentPaginator, System.Printing.PrintTicket printTicket) { }
        public override void Write(System.Windows.Documents.FixedDocument fixedDocument) { }
        public override void Write(System.Windows.Documents.FixedDocument fixedDocument, System.Printing.PrintTicket printTicket) { }
        public override void Write(System.Windows.Documents.FixedDocumentSequence fixedDocumentSequence) { }
        public override void Write(System.Windows.Documents.FixedDocumentSequence fixedDocumentSequence, System.Printing.PrintTicket printTicket) { }
        public override void Write(System.Windows.Documents.FixedPage fixedPage) { }
        public override void Write(System.Windows.Documents.FixedPage fixedPage, System.Printing.PrintTicket printTicket) { }
        public override void Write(System.Windows.Media.Visual visual) { }
        public override void Write(System.Windows.Media.Visual visual, System.Printing.PrintTicket printTicket) { }
        public void WriteAsync(string documentPath) { }
        public void WriteAsync(string documentPath, System.Windows.Xps.XpsDocumentNotificationLevel notificationLevel) { }
        public override void WriteAsync(System.Windows.Documents.DocumentPaginator documentPaginator) { }
        public override void WriteAsync(System.Windows.Documents.DocumentPaginator documentPaginator, object userSuppliedState) { }
        public override void WriteAsync(System.Windows.Documents.DocumentPaginator documentPaginator, System.Printing.PrintTicket printTicket) { }
        public override void WriteAsync(System.Windows.Documents.DocumentPaginator documentPaginator, System.Printing.PrintTicket printTicket, object userSuppliedState) { }
        public override void WriteAsync(System.Windows.Documents.FixedDocument fixedDocument) { }
        public override void WriteAsync(System.Windows.Documents.FixedDocument fixedDocument, object userSuppliedState) { }
        public override void WriteAsync(System.Windows.Documents.FixedDocument fixedDocument, System.Printing.PrintTicket printTicket) { }
        public override void WriteAsync(System.Windows.Documents.FixedDocument fixedDocument, System.Printing.PrintTicket printTicket, object userSuppliedState) { }
        public override void WriteAsync(System.Windows.Documents.FixedDocumentSequence fixedDocumentSequence) { }
        public override void WriteAsync(System.Windows.Documents.FixedDocumentSequence fixedDocumentSequence, object userSuppliedState) { }
        public override void WriteAsync(System.Windows.Documents.FixedDocumentSequence fixedDocumentSequence, System.Printing.PrintTicket printTicket) { }
        public override void WriteAsync(System.Windows.Documents.FixedDocumentSequence fixedDocumentSequence, System.Printing.PrintTicket printTicket, object userSuppliedState) { }
        public override void WriteAsync(System.Windows.Documents.FixedPage fixedPage) { }
        public override void WriteAsync(System.Windows.Documents.FixedPage fixedPage, object userSuppliedState) { }
        public override void WriteAsync(System.Windows.Documents.FixedPage fixedPage, System.Printing.PrintTicket printTicket) { }
        public override void WriteAsync(System.Windows.Documents.FixedPage fixedPage, System.Printing.PrintTicket printTicket, object userSuppliedState) { }
        public override void WriteAsync(System.Windows.Media.Visual visual) { }
        public override void WriteAsync(System.Windows.Media.Visual visual, object userSuppliedState) { }
        public override void WriteAsync(System.Windows.Media.Visual visual, System.Printing.PrintTicket printTicket) { }
        public override void WriteAsync(System.Windows.Media.Visual visual, System.Printing.PrintTicket printTicket, object userSuppliedState) { }
    }

    public enum XpsDocumentNotificationLevel
    {
        None = 0,
        ReceiveNotificationDisabled = 2,
        ReceiveNotificationEnabled = 1,
    }
}
