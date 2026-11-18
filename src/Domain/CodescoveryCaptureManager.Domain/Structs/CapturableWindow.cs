using System;
using System.Diagnostics;

namespace CodescoveryCaptureManager.Domain.Structs
{
    public struct CapturableWindow
    {
        public string Name { get; set; }
        public IntPtr Handle { get; set; }
        public Process Process { get; set; }
        /// <summary>Process name without ".exe". Kept separately so profile rules do not touch the (disposable) Process.</summary>
        public string ProcessName { get; set; }
    }
}