// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// ------------------------------------------------------------------------------
// Changes to this file must follow the http://aka.ms/api-review process.
// ------------------------------------------------------------------------------

namespace System.Windows.Controls
{
    public class PrintDialog
    {
        public PageRange PageRange { get; set; }

        public PageRangeSelection PageRangeSelection { get; set; }

        public System.Printing.PrintQueue PrintQueue { get; set; }

        public System.Printing.PrintTicket PrintTicket { get; set; }

        public double PrintableAreaHeight { get; }

        public double PrintableAreaWidth { get; }

        public bool UserPageRangeEnabled { get; set; }

        public bool? ShowDialog()
        {
            throw null;
        }
    }
}
