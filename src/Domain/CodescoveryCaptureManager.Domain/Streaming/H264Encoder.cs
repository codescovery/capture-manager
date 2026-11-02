using System;
using System.Diagnostics;
using System.Threading;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.MediaFoundation;

namespace CodescoveryCaptureManager.Domain.Streaming
{
    /// <summary>
    /// H.264 encoder over Media Foundation. Prefers a hardware (GPU) encoder fed with NV12 textures; falls back
    /// to the Microsoft software encoder fed with CPU copies. Low-latency settings, no B-frames, latest frame wins.
    /// </summary>
    internal sealed class H264Encoder : IDisposable
    {
        private const int NeedMoreInput = unchecked((int)0xC00D6D72);
        private const int StreamChange = unchecked((int)0xC00D6D61);
        private const int NoEventsAvailable = unchecked((int)0xC00D3E80);
        private const int OutputProvidesSamples = 0x100 | 0x200;
        private const long TicksPerFrameUnit = 10_000_000;

        private static readonly Guid Nv12Guid = VideoFormatGuids.NV12;
        private static readonly object StartupGate = new object();
        private static bool _mediaFoundationStarted;

        private readonly Device _device;
        private readonly int _width;
        private readonly int _height;
        private readonly int _framesPerSecond;
        private readonly int _bitrate;
        private readonly Transform _transform;
        private readonly DXGIDeviceManager _deviceManager;
        private readonly IntPtr _codec;
        private readonly bool _isAsync;
        private readonly bool _providesSamples;
        private readonly int _outputBufferSize;
        private readonly object _gate = new object();
        private readonly AutoResetEvent _frameReady = new AutoResetEvent(false);
        private readonly Thread _thread;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private Texture2D _pending;
        private Texture2D _staging;
        private int _needInput;
        private volatile bool _forceKeyFrame = true;
        private volatile bool _stopping;

        public H264Encoder(Device device, int width, int height, int framesPerSecond, int bitrate)
        {
            EnsureMediaFoundation();
            _device = device;
            _width = width;
            _height = height;
            _framesPerSecond = framesPerSecond;
            _bitrate = bitrate;

            (_transform, IsHardware) = CreateTransform();
            _isAsync = IsHardware;
            _codec = MediaFoundationNative.QueryCodecApi(_transform);
            if (IsHardware) _deviceManager = AttachDeviceManager();
            Configure();
            _providesSamples = ReadOutputProvidesSamples(out _outputBufferSize);

            _transform.ProcessMessage(TMessageType.NotifyBeginStreaming, IntPtr.Zero);
            _transform.ProcessMessage(TMessageType.NotifyStartOfStream, IntPtr.Zero);

            _thread = new Thread(IsHardware ? RunAsync : RunSync) { IsBackground = true, Name = "H264Encoder" };
            _thread.Start();
        }

        public event Action<EncodedFrame> FrameEncoded;

        public bool IsHardware { get; }

        public int Width => _width;

        public int Height => _height;

        public void RequestKeyFrame() => _forceKeyFrame = true;

        /// <summary>Queues an NV12 texture. If the encoder is busy the previous queued one is replaced (latest wins).</summary>
        public void Submit(Texture2D nv12)
        {
            lock (_gate)
            {
                if (_stopping) return;
                if (_isAsync && _needInput > 0)
                {
                    _needInput--;
                    Feed(nv12);
                    return;
                }

                _pending = nv12;
            }

            if (!_isAsync) _frameReady.Set();
        }

        private static void EnsureMediaFoundation()
        {
            lock (StartupGate)
            {
                if (_mediaFoundationStarted) return;
                MediaFoundationNative.Startup();
                _mediaFoundationStarted = true;
            }
        }

        private (Transform, bool) CreateTransform()
        {
            var hardware = MediaFoundationNative.EnumerateH264Encoders(true);
            if (hardware.Length > 0) return (hardware[0], true);

            var software = MediaFoundationNative.EnumerateH264Encoders(false);
            if (software.Length == 0) throw new InvalidOperationException("No H.264 encoder available.");
            return (software[0], false);
        }

        private DXGIDeviceManager AttachDeviceManager()
        {
            _transform.Attributes.Set(TransformAttributeKeys.TransformAsyncUnlock, 1);
            var manager = new DXGIDeviceManager();
            manager.ResetDevice(_device);
            _transform.ProcessMessage(TMessageType.SetD3DManager, manager.NativePointer);
            return manager;
        }

        private void Configure()
        {
            SetOutputType();
            SetInputType();
            MediaFoundationNative.TrySet(_codec, MediaFoundationNative.LowLatency, true);
            MediaFoundationNative.TrySet(_codec, MediaFoundationNative.BPictureCount, 0u);
            MediaFoundationNative.TrySet(_codec, MediaFoundationNative.RateControlMode, 4u);
            MediaFoundationNative.TrySet(_codec, MediaFoundationNative.MeanBitRate, (uint)_bitrate);
        }

        private void SetOutputType()
        {
            using var type = new MediaType();
            type.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
            type.Set(MediaTypeAttributeKeys.Subtype, VideoFormatGuids.H264);
            type.Set(MediaTypeAttributeKeys.AvgBitrate, _bitrate);
            type.Set(MediaTypeAttributeKeys.InterlaceMode, 2);
            type.Set(MediaTypeAttributeKeys.Mpeg2Profile, 77);
            ApplyFrameFormat(type);
            _transform.SetOutputType(0, type, 0);
        }

        private void SetInputType()
        {
            using var type = new MediaType();
            type.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
            type.Set(MediaTypeAttributeKeys.Subtype, Nv12Guid);
            type.Set(MediaTypeAttributeKeys.InterlaceMode, 2);
            ApplyFrameFormat(type);
            _transform.SetInputType(0, type, 0);
        }

        private void ApplyFrameFormat(MediaType type)
        {
            type.Set(MediaTypeAttributeKeys.FrameSize, Pack(_width, _height));
            type.Set(MediaTypeAttributeKeys.FrameRate, Pack(_framesPerSecond, 1));
            type.Set(MediaTypeAttributeKeys.PixelAspectRatio, Pack(1, 1));
        }

        private static long Pack(int high, int low) => ((long)high << 32) | (uint)low;

        private bool ReadOutputProvidesSamples(out int bufferSize)
        {
            _transform.GetOutputStreamInfo(0, out var info);
            bufferSize = Math.Max(info.CbSize, _width * _height);
            return (info.DwFlags & OutputProvidesSamples) != 0;
        }

        // Hardware (async) encoders: work is driven by NeedInput / HaveOutput events.
        private void RunAsync()
        {
            using var events = _transform.QueryInterface<MediaEventGenerator>();
            while (!_stopping)
            {
                var type = WaitForEvent(events);
                if (type == MediaEventTypes.TransformNeedInput) OnNeedInput();
                else if (type == MediaEventTypes.TransformHaveOutput) DrainOutput();
                else if (type == null) return;
            }
        }

        private MediaEventTypes? WaitForEvent(MediaEventGenerator events)
        {
            try
            {
                using var mediaEvent = events.GetEvent(true);
                return mediaEvent.TypeInfo;
            }
            catch (SharpDXException ex)
            {
                Console.WriteLine($"Encoder event failed. {ex.Message}");
                return null;
            }
        }

        private void OnNeedInput()
        {
            lock (_gate)
            {
                if (_pending == null)
                {
                    _needInput++;
                    return;
                }

                var texture = _pending;
                _pending = null;
                Feed(texture);
            }
        }

        // Software (sync) encoder: push a frame, then pull everything it has ready.
        private void RunSync()
        {
            while (!_stopping)
            {
                _frameReady.WaitOne();
                var texture = TakePending();
                if (texture == null) continue;
                Feed(texture);
                DrainOutput();
            }
        }

        private Texture2D TakePending()
        {
            lock (_gate)
            {
                var texture = _pending;
                _pending = null;
                return texture;
            }
        }

        private void Feed(Texture2D nv12)
        {
            try
            {
                ApplyKeyFrameRequest();
                using var sample = IsHardware ? CreateTextureSample(nv12) : CreateMemorySample(nv12);
                _transform.ProcessInput(0, sample, 0);
            }
            catch (Exception ex) when (ex is SharpDXException || ex is InvalidOperationException)
            {
                Console.WriteLine($"Encoder input failed. {ex.Message}");
            }
        }

        private void ApplyKeyFrameRequest()
        {
            if (!_forceKeyFrame) return;
            _forceKeyFrame = false;
            MediaFoundationNative.TrySet(_codec, MediaFoundationNative.ForceKeyFrame, 1u);
        }

        private Sample CreateTextureSample(Texture2D nv12)
        {
            MediaFactory.CreateDXGISurfaceBuffer(typeof(Texture2D).GUID, nv12, 0, false, out var buffer);
            buffer.CurrentLength = _width * _height * 3 / 2;
            return BuildSample(buffer);
        }

        private Sample CreateMemorySample(Texture2D nv12)
        {
            var size = _width * _height * 3 / 2;
            var buffer = MediaFactory.CreateMemoryBuffer(size);
            var destination = buffer.Lock(out _, out _);
            CopyNv12(nv12, destination);
            buffer.CurrentLength = size;
            buffer.Unlock();
            return BuildSample(buffer);
        }

        private void CopyNv12(Texture2D nv12, IntPtr destination)
        {
            var context = _device.ImmediateContext;
            _staging ??= CreateStaging();
            context.CopyResource(nv12, _staging);
            var box = context.MapSubresource(_staging, 0, MapMode.Read, MapFlags.None);
            try
            {
                CopyPlanes(box.DataPointer, box.RowPitch, destination);
            }
            finally
            {
                context.UnmapSubresource(_staging, 0);
            }
        }

        private void CopyPlanes(IntPtr source, int pitch, IntPtr destination)
        {
            var rows = _height + _height / 2;
            for (var row = 0; row < rows; row++)
                Utilities.CopyMemory(destination + row * _width, source + row * pitch, _width);
        }

        private Texture2D CreateStaging()
        {
            var description = new Texture2DDescription
            {
                Width = _width,
                Height = _height,
                MipLevels = 1,
                ArraySize = 1,
                Format = SharpDX.DXGI.Format.NV12,
                SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0),
                Usage = ResourceUsage.Staging,
                BindFlags = BindFlags.None,
                CpuAccessFlags = CpuAccessFlags.Read,
                OptionFlags = ResourceOptionFlags.None,
            };
            return new Texture2D(_device, description);
        }

        private Sample BuildSample(MediaBuffer buffer)
        {
            var sample = MediaFactory.CreateSample();
            sample.AddBuffer(buffer);
            buffer.Dispose();
            sample.SampleTime = _clock.ElapsedTicks * TicksPerFrameUnit / Stopwatch.Frequency;
            sample.SampleDuration = TicksPerFrameUnit / _framesPerSecond;
            return sample;
        }

        private void DrainOutput()
        {
            while (!_stopping && TryReadOutput(out var frame))
            {
                if (frame != null) FrameEncoded?.Invoke(frame);
                if (_isAsync) return;
            }
        }

        private bool TryReadOutput(out EncodedFrame frame)
        {
            frame = null;
            var provided = _providesSamples ? null : CreateOutputSample();
            var hr = MediaFoundationNative.ProcessOutput(_transform, provided?.NativePointer ?? IntPtr.Zero, out var outputPointer);
            if (hr == NeedMoreInput) return Discard(provided, false);
            if (hr == StreamChange)
            {
                SetOutputType();
                return Discard(provided, true);
            }

            if (hr < 0)
            {
                Console.WriteLine($"Encoder output failed (0x{hr:X8}).");
                return Discard(provided, false);
            }

            var sample = provided ?? (outputPointer == IntPtr.Zero ? null : new Sample(outputPointer));
            frame = ReadFrame(sample);
            return true;
        }

        private static bool Discard(Sample sample, bool result)
        {
            sample?.Dispose();
            return result;
        }

        private Sample CreateOutputSample()
        {
            var sample = MediaFactory.CreateSample();
            var buffer = MediaFactory.CreateMemoryBuffer(_outputBufferSize);
            sample.AddBuffer(buffer);
            buffer.Dispose();
            return sample;
        }

        private static EncodedFrame ReadFrame(Sample sample)
        {
            if (sample == null) return null;
            using (sample)
            using (var buffer = sample.ConvertToContiguousBuffer())
            {
                var pointer = buffer.Lock(out _, out var length);
                var data = new byte[length];
                System.Runtime.InteropServices.Marshal.Copy(pointer, data, 0, length);
                buffer.Unlock();
                return new EncodedFrame(data, AnnexB.ContainsKeyFrame(data));
            }
        }

        public void Dispose()
        {
            _stopping = true;
            MediaFoundationNative.Shutdown(_transform);
            _frameReady.Set();
            _thread.Join(2000);
            _transform.Dispose();
            MediaFoundationNative.ReleaseCodecApi(_codec);
            _deviceManager?.Dispose();
            _staging?.Dispose();
            _frameReady.Dispose();
        }
    }
}
