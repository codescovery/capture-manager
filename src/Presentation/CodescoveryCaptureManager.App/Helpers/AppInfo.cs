using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CodescoveryCaptureManager.App.Helpers
{
    /// <summary>Static facts about the app (version, links) shown in the About dialog and menu.</summary>
    internal static class AppInfo
    {
        public const string Name = "Capture Manager";
        public const string RepositoryUrl = "https://github.com/codescovery/capture-manager";
        public const string IssuesUrl = RepositoryUrl + "/issues";
        public const string ReleasesUrl = RepositoryUrl + "/releases";
        public const string LicenseUrl = RepositoryUrl + "/blob/main/LICENSE";

        public static string Version => ReadVersion();

        /// <summary>Opens a pre-filled "new issue" page. Only app version and OS are included, nothing about the user's windows.</summary>
        public static string NewIssueUrl()
            => $"{IssuesUrl}/new?body={Uri.EscapeDataString(BuildIssueTemplate())}";

        public static string DiagnosticInfo()
            => $"{Name} {Version}{Environment.NewLine}OS: {RuntimeInformation.OSDescription}{Environment.NewLine}Runtime: {RuntimeInformation.FrameworkDescription}";

        public static void OpenInBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not open {url}. {ex.Message}");
            }
        }

        private static string BuildIssueTemplate()
            => $"**What happened?**\n\n\n**What did you expect?**\n\n\n**Steps to reproduce**\n1. \n\n---\n{DiagnosticInfo()}";

        private static string ReadVersion()
        {
            var informational = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (string.IsNullOrWhiteSpace(informational)) return "dev";
            var buildMetadata = informational.IndexOf('+');
            return buildMetadata < 0 ? informational : informational.Substring(0, buildMetadata);
        }
    }
}
