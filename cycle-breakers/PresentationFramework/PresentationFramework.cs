// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// ------------------------------------------------------------------------------
// Changes to this file must follow the http://aka.ms/api-review process.
// ------------------------------------------------------------------------------

namespace Microsoft.Win32
{
    public abstract partial class CommonDialog
    {
        protected CommonDialog() { }
        public object Tag { get { throw null; } set { } }
        protected virtual void CheckPermissionsToShowDialog() { }
        protected virtual System.IntPtr HookProc(System.IntPtr hwnd, int msg, System.IntPtr wParam, System.IntPtr lParam) { throw null; }
        public abstract void Reset();
        protected abstract bool RunDialog(System.IntPtr hwndOwner);
        public virtual System.Nullable<bool> ShowDialog() { throw null; }
        public System.Nullable<bool> ShowDialog(System.Windows.Window owner) { throw null; }
    }
}
