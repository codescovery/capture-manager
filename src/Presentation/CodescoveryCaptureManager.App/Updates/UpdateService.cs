using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CodescoveryCaptureManager.App.Helpers;
using CodescoveryCaptureManager.Domain.Diagnostics;
using Velopack;
using Velopack.Sources;

namespace CodescoveryCaptureManager.App.Updates
{
    internal sealed class UpdateService
    {
        /// <summary>
        /// Folder or http(s) URL holding Velopack release files (<c>releases.win.json</c> + packages), used instead of the
        /// GitHub releases. For testing an update without publishing one.
        /// </summary>
        public const string UpdateSourceVariable = "CAPTURE_MANAGER_UPDATE_SOURCE";

        private readonly UpdateManager _updateManager = new(CreateSource());

        public async Task StageUpdateAsync(CancellationToken cancellationToken = default)
        {
            if (!_updateManager.IsInstalled)
            {
                AppLog.Info("Updates are off: not installed with the setup program.");
                return;
            }

            try
            {
                var update = await _updateManager.CheckForUpdatesAsync();
                if (update == null)
                {
                    AppLog.Info($"No update available (running {_updateManager.CurrentVersion}).");
                    return;
                }

                await _updateManager.DownloadUpdatesAsync(update, null, cancellationToken);
                // No restart: the user closed the app on purpose (measured: with the default, quitting reopened the window).
                _updateManager.WaitExitThenApplyUpdates(update, silent: true, restart: false);
                AppLog.Info($"Update {update.TargetFullRelease.Version} downloaded; it is applied when the app exits.");
            }
            catch (Exception ex)
            {
                AppLog.Warning($"Update check failed. {ex.Message}");
            }
        }

        private static IUpdateSource CreateSource()
        {
            var custom = Environment.GetEnvironmentVariable(UpdateSourceVariable);
            if (string.IsNullOrWhiteSpace(custom)) return new GithubSource(AppInfo.RepositoryUrl, null, false);

            var isWeb = Uri.TryCreate(custom, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
            return isWeb ? new SimpleWebSource(custom) : new SimpleFileSource(new DirectoryInfo(custom));
        }
    }
}
