// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using MS.Internal;

namespace System.Windows.Documents
{
    // Undo unit for TextContainer.UpdateIMELeftEdgeDeferred calls.
    internal sealed class TextTreeIMEDeferralUndoUnit : TextTreeUndoUnit
    {
        private readonly bool _isDeferred;

        // symbolOffset is just before the start edge of the element.
        internal TextTreeIMEDeferralUndoUnit(TextContainer tree, int symbolOffset, bool isDeferred) : base(tree, symbolOffset)
        {
            _isDeferred = isDeferred;
        }

        // Restores the recorded edge state, or reveals the edge if composition has ended.
        public override void DoCore()
        {
            VerifyTreeContentHashCode();

            TextPointer start = new TextPointer(this.TextContainer, this.SymbolOffset, LogicalDirection.Forward);
            Invariant.Assert(start.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.ElementStart, "TextTree undo unit out of sync with TextTree.");

            this.TextContainer.UpdateIMELeftEdgeDeferred(start.GetAdjacentElementFromOuterPosition(LogicalDirection.Forward), _isDeferred);
        }
    }
}
