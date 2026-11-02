using System;
using CodescoveryCaptureManager.Domain.Streaming;

namespace CodescoveryCaptureManager.Domain.Interfaces
{
    /// <summary>
    /// Hardware-encodes the frames of the window being shown to H.264. Idle (no GPU work) while nobody watches.
    /// </summary>
    public interface IVideoStream : IFrameTap
    {
        event Action<EncodedFrame> FrameEncoded;

        void SetViewerCount(int viewers);

        void RequestKeyFrame();
    }
}
