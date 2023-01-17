﻿using System.Runtime.InteropServices;
using System;

namespace CodescoveryCaptureManager.Domain.Interfaces
{
    [ComImport]
    [Guid("3E68D4BD-7135-4D10-8018-9FB6D9F33FA1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComVisible(true)]
    interface IInitializeWithWindow
    {
        void Initialize(
            IntPtr hwnd);
    }
}