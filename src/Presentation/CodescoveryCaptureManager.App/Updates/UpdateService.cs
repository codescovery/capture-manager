using System;
using System.Threading;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace CodescoveryCaptureManager.App.Updates
{
    internal sealed class UpdateService
    {
        private const string RepositoryUrl = "https://github.com/codescovery/captureManager";

        private readonly UpdateManager _updateManager = new(new GithubSource(RepositoryUrl, null, false));

        public async Task StageUpdateAsync(CancellationToken cancellationToken = default)
        {
            if (!_updateManager.IsInstalled) return;

            try
            {
                var update = await _updateManager.CheckForUpdatesAsync();
                if (update == null) return;

                await _updateManager.DownloadUpdatesAsync(update, null, cancellationToken);
                _updateManager.WaitExitThenApplyUpdates(update);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Update check failed. {ex.Message}");
            }
        }
    }
}
