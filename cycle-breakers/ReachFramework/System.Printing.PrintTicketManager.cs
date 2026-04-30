// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// ------------------------------------------------------------------------------
// Changes to this file must follow the http://aka.ms/api-review process.
// ------------------------------------------------------------------------------

namespace System.Printing
{
    internal class PrintTicketManager : System.IDisposable
    {
        public PrintTicketManager(string deviceName, int clientPrintSchemaVersion)
        {
        }

        public System.IO.MemoryStream GetPrintCapabilitiesAsXml(PrintTicket printTicket)
        {
            throw null;
        }

        public ValidationResult MergeAndValidatePrintTicket(PrintTicket basePrintTicket, PrintTicket deltaPrintTicket)
        {
            throw null;
        }

        public ValidationResult MergeAndValidatePrintTicket(PrintTicket basePrintTicket, PrintTicket deltaPrintTicket, PrintTicketScope scope)
        {
            throw null;
        }

        public void Dispose()
        {
        }
    }
}
