namespace CodescoveryCaptureManager.Domain.Streaming
{
    /// <summary>Snapshot of the encoder for status displays. <see cref="EncodedFrames"/> only grows; sample it twice for a rate.</summary>
    public readonly record struct VideoStreamStats(bool IsEncoding, int Width, int Height, bool IsHardware, long EncodedFrames)
    {
        public static VideoStreamStats Idle => default;

        /// <summary>Frames per second between an earlier sample and this one.</summary>
        public double FramesPerSecondSince(VideoStreamStats earlier, double elapsedSeconds)
        {
            if (elapsedSeconds <= 0 || EncodedFrames < earlier.EncodedFrames) return 0;
            return (EncodedFrames - earlier.EncodedFrames) / elapsedSeconds;
        }
    }
}
