using System;
using CodescoveryCaptureManager.Domain.Structs;

namespace CodescoveryCaptureManager.Domain.Interfaces
{
    public interface IFocusBridge : IDisposable
    {
        void Start();
        void Publish(CapturableWindow focusedWindow);
    }
}
