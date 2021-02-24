using System.IO;
using System.Reflection;

namespace CodescoveryCaptureManager.App.Helpers
{
    internal static class Assets
    {
        public static Stream GetIconImageStream() => Assembly.GetExecutingAssembly()
            .GetManifestResourceStream($"CodescoveryCaptureManager.App.Assets.icon.ico");
    }
}
