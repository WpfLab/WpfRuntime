// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Printing
{
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;

    internal interface ILegacyDevice
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
        void PushClip(Geometry clipGeometry);
        void PushTransform(Matrix transform);
        void DrawGeometry(Brush brush, Pen pen, Brush strokeBrush, Geometry geometry);
        void DrawImage(BitmapSource source, byte[] buffer, Rect rect);
        void DrawGlyphRun(Brush brush, GlyphRun glyphRun);
        void Comment(string message);
    }
}
