// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// ------------------------------------------------------------------------------
// Changes to this file must follow the http://aka.ms/api-review process.
// ------------------------------------------------------------------------------

namespace System.Printing
{
    public class PrintJobException : System.Exception
    {
        public PrintJobException()
        {
        }

        public PrintJobException(string message)
            : base(message)
        {
        }

        public PrintJobException(string message, System.Exception innerException)
            : base(message, innerException)
        {
        }

        public PrintJobException(int errorCode, string message)
            : base(message)
        {
            HResult = errorCode;
        }

        public PrintJobException(int errorCode, string message, System.Exception innerException)
            : base(message, innerException)
        {
            HResult = errorCode;
        }
    }

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

    public class PrintingCanceledException : PrintJobException
    {
        public PrintingCanceledException()
        {
        }

        public PrintingCanceledException(string message)
            : base(message)
        {
        }

        public PrintingCanceledException(string message, System.Exception innerException)
            : base(message, innerException)
        {
        }

        public PrintingCanceledException(int errorCode, string message)
            : base(message)
        {
            HResult = errorCode;
        }

        public PrintingCanceledException(int errorCode, string message, System.Exception innerException)
            : base(message, innerException)
        {
            HResult = errorCode;
        }
    }
}

namespace System.Printing.Interop
{
    internal static class NamespaceAnchor
    {
    }
}
