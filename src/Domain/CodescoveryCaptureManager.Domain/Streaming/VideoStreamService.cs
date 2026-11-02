using System;
using System.Diagnostics;
using SharpDX.Direct3D11;
using CodescoveryCaptureManager.Domain.Interfaces;

namespace CodescoveryCaptureManager.Domain.Streaming
{
    internal sealed class VideoStreamService : IVideoStream, IDisposable
    {
        private const int FramesPerSecond = 60;
        private const int MaxFramesPerSecond = 120;
        private const int MaxDimension = 4096;
        private const double BitsPerPixel = 0.12;
        private const int MinBitrate = 4_000_000;
        private const int MaxBitrate = 40_000_000;
        private static readonly long MinFrameTicks = Stopwatch.Frequency / (MaxFramesPerSecond + 5);

        private readonly object _gate = new object();
        private int _viewers;
        private long _lastFrameTicks;
        private bool _failed;
        private IntPtr _deviceHandle;
        private Nv12Converter _converter;
        private H264Encoder _encoder;
        private int _sourceWidth;
        private int _sourceHeight;

        public event Action<EncodedFrame> FrameEncoded;

        public void SetViewerCount(int viewers)
        {
            lock (_gate)
            {
                _viewers = viewers;
                _failed = false;
                if (viewers == 0) DisposePipeline();
            }
        }

        public void RequestKeyFrame()
        {
            lock (_gate)
            {
                _encoder?.RequestKeyFrame();
            }
        }

        public void OnFrame(Texture2D frame, int contentWidth, int contentHeight)
        {
            lock (_gate)
            {
                if (_viewers == 0 || _failed || IsTooSoon()) return;
                if (!EnsurePipeline(frame.Device, contentWidth, contentHeight)) return;

                var nv12 = _converter.Convert(frame, contentWidth, contentHeight);
                _encoder.Submit(nv12);
            }
        }

        public void OnCaptureStopped()
        {
            lock (_gate)
            {
                DisposePipeline();
            }
        }

        private bool IsTooSoon()
        {
            var now = Stopwatch.GetTimestamp();
            if (now - _lastFrameTicks < MinFrameTicks) return true;
            _lastFrameTicks = now;
            return false;
        }

        private bool EnsurePipeline(Device device, int contentWidth, int contentHeight)
        {
            if (_encoder != null && _deviceHandle == device.NativePointer && _sourceWidth == contentWidth && _sourceHeight == contentHeight) return true;

            DisposePipeline();
            try
            {
                CreatePipeline(device, contentWidth, contentHeight);
                return true;
            }
            catch (Exception ex)
            {
                _failed = true;
                DisposePipeline();
                Console.WriteLine($"Video stream not started. {ex.Message}");
                return false;
            }
        }

        private void CreatePipeline(Device device, int contentWidth, int contentHeight)
        {
            var (width, height) = FitEven(contentWidth, contentHeight);
            _deviceHandle = device.NativePointer;
            _sourceWidth = contentWidth;
            _sourceHeight = contentHeight;
            _converter = new Nv12Converter(device, contentWidth, contentHeight, width, height, FramesPerSecond);
            _encoder = new H264Encoder(device, width, height, FramesPerSecond, BitrateFor(width, height));
            _encoder.FrameEncoded += OnEncoded;
            Console.WriteLine($"Video stream: {width}x{height} {(_encoder.IsHardware ? "hardware" : "software")} H.264.");
        }

        private void OnEncoded(EncodedFrame frame) => FrameEncoded?.Invoke(frame);

        private static (int, int) FitEven(int width, int height)
        {
            var scale = Math.Min(1.0, (double)MaxDimension / Math.Max(width, height));
            return (Even((int)(width * scale)), Even((int)(height * scale)));
        }

        private static int Even(int value) => Math.Max(2, value & ~1);

        private static int BitrateFor(int width, int height)
        {
            var bitrate = (int)(width * (double)height * FramesPerSecond * BitsPerPixel);
            return Math.Min(MaxBitrate, Math.Max(MinBitrate, bitrate));
        }

        private void DisposePipeline()
        {
            if (_encoder != null) _encoder.FrameEncoded -= OnEncoded;
            _encoder?.Dispose();
            _converter?.Dispose();
            _encoder = null;
            _converter = null;
            _deviceHandle = IntPtr.Zero;
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _viewers = 0;
                DisposePipeline();
            }
        }
    }
}
