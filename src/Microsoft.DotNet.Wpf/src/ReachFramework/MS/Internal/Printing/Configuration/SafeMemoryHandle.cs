// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace MS.Internal.PrintWin32Thunk
{
    using System;
    using System.Runtime.InteropServices;

    internal sealed class SafeMemoryHandle : SafeHandle
    {
        private readonly bool _ownsHandle;

        private SafeMemoryHandle(IntPtr handle, int size, bool ownsHandle)
            : base(IntPtr.Zero, ownsHandle)
        {
            _ownsHandle = ownsHandle;
            Size = size;
            SetHandle(handle);
        }

        public SafeMemoryHandle(IntPtr win32Pointer)
            : this(win32Pointer, 0, ownsHandle: false)
        {
        }

        public override bool IsInvalid => handle == IntPtr.Zero;

        public static SafeMemoryHandle Null { get; } = new SafeMemoryHandle(IntPtr.Zero, 0, ownsHandle: false);

        public int Size { get; }

        public void CopyFromArray(byte[] source, int startIndex, int length)
        {
            Marshal.Copy(source, startIndex, handle, length);
        }

        public void CopyToArray(byte[] destination, int startIndex, int length)
        {
            Marshal.Copy(handle, destination, startIndex, length);
        }

        public static SafeMemoryHandle Create(int byteCount)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(byteCount);

            IntPtr buffer = Marshal.AllocHGlobal(byteCount);
            return new SafeMemoryHandle(buffer, byteCount, ownsHandle: true);
        }

        public static bool TryCreate(int byteCount, ref SafeMemoryHandle result)
        {
            try
            {
                result = Create(byteCount);
                return true;
            }
            catch (OutOfMemoryException)
            {
                result = Null;
                return false;
            }
        }

        public static SafeMemoryHandle Wrap(IntPtr win32Pointer)
        {
            return new SafeMemoryHandle(win32Pointer, 0, ownsHandle: false);
        }

        protected override bool ReleaseHandle()
        {
            if (_ownsHandle && handle != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(handle);
                handle = IntPtr.Zero;
            }

            return true;
        }
    }
}
