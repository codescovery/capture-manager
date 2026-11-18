using SharpDX.Direct3D11;

namespace CodescoveryCaptureManager.Domain.Interfaces
{
    /// <summary>Receives every captured frame (GPU texture) of the window being shown.</summary>
    public interface IFrameTap
    {
        /// <summary>Called on the capture thread; the texture is only valid during the call.</summary>
        void OnFrame(Texture2D frame, int contentWidth, int contentHeight);

        void OnCaptureStopped();
    }
}
