// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;

namespace System.Windows.Media;

public sealed class FormattedTextTests
{
    [StaFact]
    public void Constructor_WhenTextIsMeasured_DoesNotThrow()
    {
        FormattedText formattedText = new(
            "WPF text shaping probe",
            CultureInfo.GetCultureInfo("en-US"),
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            12,
            Brushes.Black,
            1);

        double width = formattedText.Width;

        width.Should().BeGreaterThan(0);
    }
}
