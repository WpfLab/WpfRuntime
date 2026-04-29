// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// ------------------------------------------------------------------------------
// Changes to this file must follow the http://aka.ms/api-review process.
// ------------------------------------------------------------------------------

namespace System.Printing
{
    public interface ILegacyDevice
    {
        int StartDocument(string printerName, string jobName, string filename, byte[] deviceMode);
        void StartDocumentWithoutCreatingDC(string printerName, string jobName, string filename);
        void EndDocument();
        void CreateDeviceContext(string printerName, string jobName, byte[] deviceMode);
        void DeleteDeviceContext();
        string ExtEscGetName();
        bool ExtEscMXDWPassThru();
        void StartPage(byte[] deviceMode, int rasterizationDPI);
        void EndPage();
        void PopTransform();
        void PopClip();
        void PushClip(System.Windows.Media.Geometry clipGeometry);
        void PushTransform(System.Windows.Media.Matrix transform);
        void DrawGeometry(System.Windows.Media.Brush brush, System.Windows.Media.Pen pen, System.Windows.Media.Brush strokeBrush, System.Windows.Media.Geometry geometry);
        void DrawImage(System.Windows.Media.Imaging.BitmapSource source, byte[] buffer, System.Windows.Rect rect);
        void DrawGlyphRun(System.Windows.Media.Brush brush, System.Windows.Media.GlyphRun glyphRun);
        void Comment(string message);
    }

    public class PrintQueue
    {
        private PrintQueue()
        {
        }

        public string FullName { get; } = string.Empty;

        public virtual PrintTicket UserPrintTicket { get; set; }

        public PrintCapabilities GetPrintCapabilities(PrintTicket printTicket)
        {
            throw new NotSupportedException();
        }

        public ILegacyDevice GetLegacyDevice()
        {
            throw new NotSupportedException();
        }

        public static uint GetDpiX(ILegacyDevice legacyDevice)
        {
            throw new NotSupportedException();
        }

        public static uint GetDpiY(ILegacyDevice legacyDevice)
        {
            throw new NotSupportedException();
        }
    }

    public class PrintCapabilities
    {
        public double? OrientedPageMediaWidth { get; }

        public double? OrientedPageMediaHeight { get; }
    }

    public sealed partial class PrintTicket
    {
        private PrintTicket()
        {
        }
    }

}
