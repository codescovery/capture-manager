using System;
using System.Collections.Generic;
using CodescoveryCaptureManager.Domain.Structs;

namespace CodescoveryCaptureManager.Domain.Interfaces
{
    public interface IWindowsProcessServices:IDisposable
    {
        IReadOnlyList<CapturableWindow> GetOpenedWindows(params System.Windows.Window[] ignoredWindows);

        /// <summary>Describes one window by handle. False when it is not capturable (hidden, untitled, ignored process, gone).</summary>
        bool TryDescribeWindow(IntPtr windowHandle, out CapturableWindow window);
    }
}