using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using CodescoveryCaptureManager.Domain.Helpers;
using CodescoveryCaptureManager.Domain.Interop;

namespace CodescoveryCaptureManager.App.Helpers
{
    /// <summary>
    /// Renders the real content of a window (PrintWindow, not a screen copy) and its app icon,
    /// off the UI thread. Results are frozen so any thread can hand them to WPF.
    /// </summary>
    internal static class WindowThumbnailProvider
    {
        private const uint PrintWindowRenderFullContent = 2;
        public const int ThumbnailWidth = 480;

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        public static bool IsMinimized(IntPtr windowHandle) => IsIconic(windowHandle);

        /// <summary>PrintWindow first; Graphics Capture for windows it cannot render (e.g. Windows Terminal).</summary>
        public static async Task<BitmapSource> CaptureThumbnailAsync(IntPtr windowHandle)
        {
            if (IsIconic(windowHandle)) return null;

            var printed = await Task.Run(() => CaptureThumbnail(windowHandle));
            if (printed != null) return printed;

            var snapshot = await WindowSnapshotHelper.TryCaptureAsync(windowHandle);
            return snapshot == null ? null : await Task.Run(() => ToThumbnail(snapshot));
        }

        private static BitmapSource ToThumbnail(WindowSnapshot snapshot)
        {
            BitmapSource source = BitmapSource.Create(
                snapshot.Width, snapshot.Height, 96, 96, System.Windows.Media.PixelFormats.Bgr32, null, snapshot.Bgra, snapshot.Stride);
            if (snapshot.Width > ThumbnailWidth)
                source = new TransformedBitmap(source, new ScaleTransform((double)ThumbnailWidth / snapshot.Width, (double)ThumbnailWidth / snapshot.Width));
            source.Freeze();
            return source;
        }

        public static Task<BitmapSource> LoadIconAsync(Process process)
            => Task.Run(() => LoadIcon(process));

        private static BitmapSource CaptureThumbnail(IntPtr windowHandle)
        {
            try
            {
                if (IsIconic(windowHandle)) return null;
                if (!NativeMethods.GetWindowRect(windowHandle, out var rect)) return null;

                var width = rect.Right - rect.Left;
                var height = rect.Bottom - rect.Top;
                if (width <= 0 || height <= 0) return null;

                using var full = RenderWindow(windowHandle, width, height);
                return full == null ? null : ToFrozenBitmapSource(Downscale(full));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Thumbnail failed for {windowHandle}. {ex.Message}");
                return null;
            }
        }

        private static Bitmap RenderWindow(IntPtr windowHandle, int width, int height)
        {
            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);
            var deviceContext = graphics.GetHdc();
            try
            {
                if (NativeMethods.PrintWindow(windowHandle, deviceContext, PrintWindowRenderFullContent))
                    return bitmap;
            }
            finally
            {
                graphics.ReleaseHdc(deviceContext);
            }

            bitmap.Dispose();
            return null;
        }

        private static Bitmap Downscale(Bitmap source)
        {
            if (source.Width <= ThumbnailWidth) return (Bitmap)source.Clone();

            var thumbnailHeight = Math.Max(1, source.Height * ThumbnailWidth / source.Width);
            var thumbnail = new Bitmap(ThumbnailWidth, thumbnailHeight, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(thumbnail);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(source, 0, 0, ThumbnailWidth, thumbnailHeight);
            return thumbnail;
        }

        private static BitmapSource ToFrozenBitmapSource(Bitmap bitmap)
        {
            using (bitmap)
            {
                var hBitmap = bitmap.GetHbitmap();
                try
                {
                    var source = Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    source.Freeze();
                    return source;
                }
                finally
                {
                    NativeMethods.DeleteObject(hBitmap);
                }
            }
        }

        private static BitmapSource LoadIcon(Process process)
        {
            try
            {
                var path = process.MainModule?.FileName;
                if (string.IsNullOrEmpty(path)) return null;

                using var icon = Icon.ExtractAssociatedIcon(path);
                if (icon == null) return null;

                var source = Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            catch (Exception)
            {
                // Elevated or exited process: no icon, the card shows a placeholder.
                return null;
            }
        }
    }
}
