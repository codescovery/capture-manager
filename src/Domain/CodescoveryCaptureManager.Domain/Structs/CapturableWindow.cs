using System;
using System.Diagnostics;

namespace CodescoveryCaptureManager.Domain.Structs
{
    public struct CapturableWindow
    {
        public string Name { get; set; }
        public IntPtr Handle { get; set; }
        public Process Process { get; set; }
    }
}