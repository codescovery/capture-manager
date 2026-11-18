namespace CodescoveryCaptureManager.Domain.Streaming
{
    /// <summary>One H.264 access unit in Annex-B format.</summary>
    public sealed class EncodedFrame
    {
        public EncodedFrame(byte[] data, bool isKeyFrame)
        {
            Data = data;
            IsKeyFrame = isKeyFrame;
        }

        public byte[] Data { get; }

        public bool IsKeyFrame { get; }
    }
}
