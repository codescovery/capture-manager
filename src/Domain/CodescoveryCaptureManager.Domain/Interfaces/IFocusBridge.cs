using System;
using CodescoveryCaptureManager.Domain.Structs;

namespace CodescoveryCaptureManager.Domain.Interfaces
{
    public interface IFocusBridge : IDisposable
    {
        /// <summary>False when the port was taken: no web viewer for this run.</summary>
        bool IsRunning { get; }

        /// <summary>Address of the web viewer page.</summary>
        string Url { get; }

        /// <summary>Browser pages connected right now.</summary>
        int ViewerCount { get; }

        void Start();
        void Publish(CapturableWindow focusedWindow);
        void PublishBlurred();
    }
}
