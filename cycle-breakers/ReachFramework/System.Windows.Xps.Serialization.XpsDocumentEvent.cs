// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// ------------------------------------------------------------------------------
// Changes to this file must follow the http://aka.ms/api-review process.
// ------------------------------------------------------------------------------

namespace System.Windows.Xps.Serialization
{
    internal enum XpsDocumentEventType
    {
        None = 0,
        AddFixedDocumentSequencePre = 1,
        AddFixedDocumentPre = 2,
        AddFixedPagePre = 3,
        AddFixedPagePost = 4,
        AddFixedDocumentPost = 5,
        XpsDocumentCancel = 6,
        AddFixedDocumentSequencePrintTicketPre = 7,
        AddFixedDocumentPrintTicketPre = 8,
        AddFixedPagePrintTicketPre = 9,
        AddFixedPagePrintTicketPost = 10,
        AddFixedDocumentPrintTicketPost = 11,
        AddFixedDocumentSequencePrintTicketPost = 12,
        AddFixedDocumentSequencePost = 13,
    }

    internal class XpsSerializationXpsDriverDocEventArgs : System.EventArgs
    {
        private System.Printing.PrintTicket _printTicket;

        public XpsSerializationXpsDriverDocEventArgs(XpsDocumentEventType documentEvent, int currentCount, System.Printing.PrintTicket printTicket)
        {
            DocumentEvent = documentEvent;
            CurrentCount = currentCount;
            _printTicket = printTicket;
        }

        public int CurrentCount { get; }

        public XpsDocumentEventType DocumentEvent { get; }

        public bool Modified { get; private set; }

        public System.Printing.PrintTicket PrintTicket
        {
            get => _printTicket;
            set
            {
                _printTicket = value;
                Modified = true;
            }
        }
    }
}
