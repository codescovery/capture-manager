using System;
using System.Text;
using CodescoveryCaptureManager.Domain.Interop;

namespace CodescoveryCaptureManager.Domain.Extensions
{
    public static class CommonExtensions
    {
        public static int ToInt(this double value) => Convert.ToInt32(value);
        public static string GetWindowTitle(IntPtr hWnd)
        {
            var length = NativeMethods.GetWindowTextLength(hWnd);
            var title = new StringBuilder(length);
            NativeMethods.GetWindowText(hWnd, title, length);
            return title.ToString();
        }
    }
}