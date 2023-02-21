using System;
using System.Collections.Generic;
using CodescoveryCaptureManager.Domain.Structs;

namespace CodescoveryCaptureManager.Domain.Interfaces
{
    public interface IWindowsProcessServices:IDisposable
    {
        IReadOnlyList<CapturableWindow> GetOpenedWindows(params System.Windows.Window[] ignoredWindows);
    }
}