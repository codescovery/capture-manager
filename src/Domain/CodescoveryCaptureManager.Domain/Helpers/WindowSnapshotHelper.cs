using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using SharpDX.Direct3D11;

namespace CodescoveryCaptureManager.Domain.Helpers
{
    /// <summary>Raw BGRA pixels of one captured frame; <see cref="Stride"/> is bytes per row.</summary>
    public sealed record WindowSnapshot(int Width, int Height, int Stride, byte[] Bgra);

    /// <summary>
    /// Grabs a single frame of a window through Windows Graphics Capture. Works for GPU-composited
    /// windows (Windows Terminal, browsers) where <c>PrintWindow</c> fails. Minimized windows yield no frame.
    /// </summary>
    public static class WindowSnapshotHelper
    {
        private static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(1.5);

        public static async Task<WindowSnapshot> TryCaptureAsync(IntPtr windowHandle)
        {
            try
            {
                var item = CaptureHelper.CreateItemForWindow(windowHandle);
                return item == null ? null : await CaptureFirstFrameAsync(item);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Snapshot failed for {windowHandle}. {ex.Message}");
                return null;
            }
        }

        private static async Task<WindowSnapshot> CaptureFirstFrameAsync(GraphicsCaptureItem item)
        {
            if (item.Size.Width <= 0 || item.Size.Height <= 0) return null;

            var device = Direct3D11Helper.CreateDevice();
            using var d3dDevice = Direct3D11Helper.CreateSharpDXDevice(device);
            using var framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, item.Size);
            using var session = framePool.CreateCaptureSession(item);

            var frameArrived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            framePool.FrameArrived += (_, _) => frameArrived.TrySetResult(true);
            session.IsCursorCaptureEnabled = false;
            session.StartCapture();

            var finished = await Task.WhenAny(frameArrived.Task, Task.Delay(FrameTimeout));
            if (finished != frameArrived.Task) return null;

            using var frame = framePool.TryGetNextFrame();
            return frame == null ? null : ReadPixels(d3dDevice, frame);
        }

        private static WindowSnapshot ReadPixels(Device d3dDevice, Direct3D11CaptureFrame frame)
        {
            using var source = Direct3D11Helper.CreateSharpDXTexture2D(frame.Surface);
            var description = source.Description;
            using var staging = new Texture2D(d3dDevice, new Texture2DDescription
            {
                Width = description.Width,
                Height = description.Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = description.Format,
                SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0),
                Usage = ResourceUsage.Staging,
                BindFlags = BindFlags.None,
                CpuAccessFlags = CpuAccessFlags.Read,
                OptionFlags = ResourceOptionFlags.None,
            });

            var context = d3dDevice.ImmediateContext;
            context.CopyResource(source, staging);

            var box = context.MapSubresource(staging, 0, MapMode.Read, MapFlags.None);
            try
            {
                return CopyRows(box, description.Width, description.Height);
            }
            finally
            {
                context.UnmapSubresource(staging, 0);
            }
        }

        private static WindowSnapshot CopyRows(SharpDX.DataBox box, int width, int height)
        {
            var stride = width * 4;
            var pixels = new byte[stride * height];
            for (var row = 0; row < height; row++)
                Marshal.Copy(box.DataPointer + row * box.RowPitch, pixels, row * stride, stride);
            return new WindowSnapshot(width, height, stride, pixels);
        }
    }
}
