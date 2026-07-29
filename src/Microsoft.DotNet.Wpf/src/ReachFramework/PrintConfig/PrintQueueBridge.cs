// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Printing
{
    using System;

    internal class PrintSystemDispatcherObject : System.Windows.Threading.DispatcherObject
    {
        public void VerifyThreadLocality()
        {
        }
    }

    internal partial class PrintQueue
    {
        public string FullName { get; } = string.Empty;

        public virtual PrintTicket UserPrintTicket { get; set; }

        public PrintJobSettings CurrentJobSettings { get; } = new PrintJobSettings();

        public int ClientPrintSchemaVersion { get; } = 1;

        internal System.Windows.Xps.Serialization.RCW.IXpsOMPackageWriter XpsOMPackageWriter { get; set; }

        public PrintCapabilities GetPrintCapabilities(PrintTicket printTicket)
        {
            throw new NotSupportedException();
        }

        public ValidationResult MergeAndValidatePrintTicket(PrintTicket basePrintTicket, PrintTicket deltaPrintTicket)
        {
            throw new NotSupportedException();
        }

        internal ILegacyDevice GetLegacyDevice()
        {
            throw new NotSupportedException();
        }

        internal static uint GetDpiX(ILegacyDevice legacyDevice)
        {
            throw new NotSupportedException();
        }

        internal static uint GetDpiY(ILegacyDevice legacyDevice)
        {
            throw new NotSupportedException();
        }

    }

    internal partial class PrintJobSettings
    {
        public PrintTicket CurrentPrintTicket { get; set; }

        public string Description { get; set; }
    }
}

namespace MS.Internal.PrintWin32Thunk
{
    using System;

    internal partial class XpsPrintStream : System.IO.Stream
    {
        public override bool CanRead => throw new NotSupportedException();

        public override bool CanSeek => throw new NotSupportedException();

        public override bool CanWrite => throw new NotSupportedException();

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public static XpsPrintStream CreateXpsPrintStream()
        {
            throw new NotSupportedException();
        }

        public System.Runtime.InteropServices.ComTypes.IStream GetManagedIStream()
        {
            throw new NotSupportedException();
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, System.IO.SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
