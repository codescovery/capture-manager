using System;
using System.Numerics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.UI.Composition;
using CodescoveryCaptureManager.Domain.Helpers;

namespace CodescoveryCaptureManager.Domain.Services
{
    public class WindowCaptureService 
    {
        private readonly Compositor _compositor;
        private readonly ContainerVisual _root;
        private readonly CompositionSurfaceBrush _brush;
        private readonly SpriteVisual _content;
        private readonly IDirect3DDevice _device;
        private CaptureService _captureService;
        private bool _isCapturing;
        private IntPtr _activeCapturableWindowInteropHandle;
        public IntPtr ActiveInteropHandle() => _activeCapturableWindowInteropHandle;
        public bool IsCapturing() => _isCapturing;
        public bool IsCapturing(IntPtr activeCapturableWindowInteropHandle)
            => activeCapturableWindowInteropHandle.Equals(_activeCapturableWindowInteropHandle) && IsCapturing();

        public Visual GetVisual() => _root;
        public WindowCaptureService(Compositor compositor)
        {
            _isCapturing = false;
            _compositor = compositor;
            _activeCapturableWindowInteropHandle = IntPtr.Zero;
            _device = Direct3D11Helper.CreateDevice();
            _root = InitializeRoot();

            _brush = InitializeCompositionSurfaceBrush();

            _content = InitializeContent();
            _root.Children.InsertAtTop(_content);
        }

        private ContainerVisual InitializeRoot()
        {
            var root = _compositor.CreateContainerVisual();
            root.RelativeSizeAdjustment = Vector2.One;
            return root;
        }
        private SpriteVisual InitializeContent()
        {
            var shadow = _compositor.CreateDropShadow();
            shadow.Mask = _brush;

            var content = _compositor.CreateSpriteVisual();
            content.AnchorPoint = new Vector2(0.5f);
            content.RelativeOffsetAdjustment = new Vector3(0.5f, 0.5f, 0);
            content.RelativeSizeAdjustment = Vector2.One;
            content.Size = new Vector2(-80, -80);
            content.Brush = _brush;
            content.Shadow = shadow;
            return content;
        }

        private CompositionSurfaceBrush InitializeCompositionSurfaceBrush()
        {
            var brush = _compositor.CreateSurfaceBrush();
            brush.HorizontalAlignmentRatio = 0.5f;
            brush.VerticalAlignmentRatio = 0.5f;
            brush.Stretch = CompositionStretch.Uniform;
            return brush;
        }


        public void StartCapture(GraphicsCaptureItem item, IntPtr activeWindowCaptureHandle)
        {
            StopCapture();
            _isCapturing = true;
            _activeCapturableWindowInteropHandle = activeWindowCaptureHandle;
            _captureService = new CaptureService(_device, item);
            _brush.Surface = _captureService.CreateSurface(_compositor);
            _captureService.StartCapture();
        }

        public void StopCapture()
        {
            _activeCapturableWindowInteropHandle = IntPtr.Zero;
            _isCapturing = false;
            _captureService?.Dispose();
            _brush.Surface = null;
        }
        public void Dispose()
        {
            StopCapture();
            _root?.Dispose();
            _content?.Dispose();
            _brush?.Dispose();
            _device?.Dispose();
        }

    }
}
