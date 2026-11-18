using System;
using SharpDX.Direct3D11;
using Format = SharpDX.DXGI.Format;
using SampleDescription = SharpDX.DXGI.SampleDescription;

namespace CodescoveryCaptureManager.Domain.Streaming
{
    /// <summary>GPU BGRA to NV12 (BT.709) conversion and scaling with the D3D11 video processor. Zero CPU copies.</summary>
    internal sealed class Nv12Converter : IDisposable
    {
        private const int RingSize = 8;

        private readonly Device _device;
        private readonly VideoDevice _videoDevice;
        private readonly VideoContext _videoContext;
        private readonly VideoProcessorEnumerator _enumerator;
        private readonly VideoProcessor _processor;
        private readonly Texture2D[] _textures = new Texture2D[RingSize];
        private readonly VideoProcessorOutputView[] _outputViews = new VideoProcessorOutputView[RingSize];
        private int _next;
        private int _sourceWidth;
        private int _sourceHeight;

        public Nv12Converter(Device device, int sourceWidth, int sourceHeight, int width, int height, int framesPerSecond)
        {
            _device = device;
            Width = width;
            Height = height;
            _videoDevice = device.QueryInterface<VideoDevice>();
            _videoContext = device.ImmediateContext.QueryInterface<VideoContext>();

            var content = new VideoProcessorContentDescription
            {
                InputFrameFormat = VideoFrameFormat.Progressive,
                InputFrameRate = new SharpDX.DXGI.Rational(framesPerSecond, 1),
                InputWidth = sourceWidth,
                InputHeight = sourceHeight,
                OutputFrameRate = new SharpDX.DXGI.Rational(framesPerSecond, 1),
                OutputWidth = width,
                OutputHeight = height,
                Usage = VideoUsage.PlaybackNormal,
            };
            _videoDevice.CreateVideoProcessorEnumerator(ref content, out _enumerator);
            _videoDevice.CreateVideoProcessor(_enumerator, 0, out _processor);
            ConfigureColorSpaces();
            CreateRing();
        }

        public int Width { get; }

        public int Height { get; }

        public Texture2D Convert(Texture2D source, int contentWidth, int contentHeight)
        {
            UpdateSourceRect(contentWidth, contentHeight);

            var slot = _next;
            _next = (_next + 1) % RingSize;

            using var inputView = CreateInputView(source);
            var stream = new VideoProcessorStream { Enable = true, PInputSurface = inputView };
            _videoContext.VideoProcessorBlt(_processor, _outputViews[slot], 0, 1, new[] { stream });
            return _textures[slot];
        }

        private void UpdateSourceRect(int contentWidth, int contentHeight)
        {
            if (contentWidth == _sourceWidth && contentHeight == _sourceHeight) return;

            _sourceWidth = contentWidth;
            _sourceHeight = contentHeight;
            _videoContext.VideoProcessorSetStreamSourceRect(_processor, 0, true, new SharpDX.Mathematics.Interop.RawRectangle(0, 0, contentWidth, contentHeight));
            _videoContext.VideoProcessorSetStreamDestRect(_processor, 0, true, new SharpDX.Mathematics.Interop.RawRectangle(0, 0, Width, Height));
            _videoContext.VideoProcessorSetOutputTargetRect(_processor, true, new SharpDX.Mathematics.Interop.RawRectangle(0, 0, Width, Height));
        }

        private VideoProcessorInputView CreateInputView(Texture2D source)
        {
            var description = new VideoProcessorInputViewDescription
            {
                FourCC = 0,
                Dimension = VpivDimension.Texture2D,
                Texture2D = new Texture2DVpiv { MipSlice = 0, ArraySlice = 0 },
            };
            _videoDevice.CreateVideoProcessorInputView(source, _enumerator, description, out var view);
            return view;
        }

        private void ConfigureColorSpaces()
        {
            var rgb = new VideoProcessorColorSpace { Usage = false, RgbRange = false, YCbCrMatrix = false, YCbCrXvYCC = false, NominalRange = 2 };
            var yuv = new VideoProcessorColorSpace { Usage = false, RgbRange = false, YCbCrMatrix = true, YCbCrXvYCC = false, NominalRange = 1 };
            _videoContext.VideoProcessorSetStreamColorSpace(_processor, 0, rgb);
            _videoContext.VideoProcessorSetOutputColorSpace(_processor, yuv);
        }

        private void CreateRing()
        {
            var description = new Texture2DDescription
            {
                Width = Width,
                Height = Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.NV12,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None,
            };
            var outputDescription = new VideoProcessorOutputViewDescription
            {
                Dimension = VpovDimension.Texture2D,
                Texture2D = new Texture2DVpov { MipSlice = 0 },
            };
            for (var index = 0; index < RingSize; index++)
            {
                _textures[index] = new Texture2D(_device, description);
                _videoDevice.CreateVideoProcessorOutputView(_textures[index], _enumerator, outputDescription, out _outputViews[index]);
            }
        }

        public void Dispose()
        {
            foreach (var view in _outputViews) view?.Dispose();
            foreach (var texture in _textures) texture?.Dispose();
            _processor?.Dispose();
            _enumerator?.Dispose();
            _videoContext?.Dispose();
            _videoDevice?.Dispose();
        }
    }
}
