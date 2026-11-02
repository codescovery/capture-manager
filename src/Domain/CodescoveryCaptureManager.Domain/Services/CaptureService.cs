using System;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.UI.Composition;
using CodescoveryCaptureManager.Domain.Helpers;
using CodescoveryCaptureManager.Domain.Interfaces;

namespace CodescoveryCaptureManager.Domain.Services
{
    internal class CaptureService
    {
        private readonly GraphicsCaptureItem _item;
        private readonly Direct3D11CaptureFramePool _framePool;
        private readonly GraphicsCaptureSession _session;
        private SizeInt32 _lastSize;
        private readonly object _sync = new object();
        private bool _disposed;

        private readonly IDirect3DDevice _device;
        private readonly SharpDX.Direct3D11.Device _d3dDevice;
        private readonly SharpDX.DXGI.SwapChain1 _swapChain;
        private readonly IFrameTap _frameTap;

        public CaptureService(IDirect3DDevice device, GraphicsCaptureItem graphicsCaptureItem, IFrameTap frameTap = null)
        {
            _frameTap = frameTap;
            _item = graphicsCaptureItem;
            _device = device;
            _d3dDevice = Direct3D11Helper.CreateSharpDXDevice(_device);

            var factory = new SharpDX.DXGI.Factory2();
            var description = new SharpDX.DXGI.SwapChainDescription1()
            {
                Width = _item.Size.Width,
                Height = _item.Size.Height,
                Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                Stereo = false,
                SampleDescription = new SharpDX.DXGI.SampleDescription()
                {
                    Count = 1,
                    Quality = 0
                },
                Usage = SharpDX.DXGI.Usage.RenderTargetOutput,
                BufferCount = 2,
                Scaling = SharpDX.DXGI.Scaling.Stretch,
                SwapEffect = SharpDX.DXGI.SwapEffect.FlipSequential,
                AlphaMode = SharpDX.DXGI.AlphaMode.Premultiplied,
                Flags = SharpDX.DXGI.SwapChainFlags.None
            };
            _swapChain = new SharpDX.DXGI.SwapChain1(factory, _d3dDevice, ref description);

            _framePool = Direct3D11CaptureFramePool.Create(
                _device,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                2,
                graphicsCaptureItem.Size);
            _session = _framePool.CreateCaptureSession(graphicsCaptureItem);
            _lastSize = graphicsCaptureItem.Size;

            _framePool.FrameArrived += OnFrameArrived;
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
                _framePool.FrameArrived -= OnFrameArrived;
                _frameTap?.OnCaptureStopped();
                _session?.Dispose();
                _framePool?.Dispose();
                _swapChain?.Dispose();
                _d3dDevice?.Dispose();
            }
        }

        public void StartCapture()
        {
            _session.StartCapture();
        }

        public ICompositionSurface CreateSurface(Compositor compositor)
        {
            return compositor.CreateCompositionSurfaceForSwapChain(_swapChain);
        }

        private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            lock (_sync)
            {
                if (_disposed) return;
                try
                {
                    ProcessFrame(sender);
                }
                catch (Exception ex)
                {
                    // Target window may have closed or the device been lost mid-frame.
                    Console.WriteLine($"Frame processing failed. {ex.Message}");
                }
            }
        }

        private void OfferToTap(SharpDX.Direct3D11.Texture2D frame)
        {
            if (_frameTap == null) return;
            try
            {
                _frameTap.OnFrame(frame, _lastSize.Width, _lastSize.Height);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Frame tap failed. {ex.Message}");
            }
        }

        private void ProcessFrame(Direct3D11CaptureFramePool sender)
        {
            var newSize = false;

            using (var frame = sender.TryGetNextFrame())
            {
                if (frame == null) return;
                if (frame.ContentSize.Width != _lastSize.Width ||
                    frame.ContentSize.Height != _lastSize.Height)
                {
                    // The thing we have been capturing has changed size.
                    // We need to resize the swap chain first, then blit the pixels.
                    // After we do that, retire the frame and then recreate the frame pool.
                    newSize = true;
                    _lastSize = frame.ContentSize;
                    _swapChain.ResizeBuffers(
                        2,
                        _lastSize.Width,
                        _lastSize.Height,
                        SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                        SharpDX.DXGI.SwapChainFlags.None);
                }

                using (var backBuffer = _swapChain.GetBackBuffer<SharpDX.Direct3D11.Texture2D>(0))
                using (var bitmap = Direct3D11Helper.CreateSharpDXTexture2D(frame.Surface))
                {
                    _d3dDevice.ImmediateContext.CopyResource(bitmap, backBuffer);
                    OfferToTap(bitmap);
                }

            } // Retire the frame.

            _swapChain.Present(0, SharpDX.DXGI.PresentFlags.None);

            if (newSize)
            {
                _framePool.Recreate(
                    _device,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    2,
                    _lastSize);
            }
        }
    }
}
