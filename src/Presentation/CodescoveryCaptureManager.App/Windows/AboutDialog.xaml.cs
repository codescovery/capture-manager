using System;
using System.Threading.Tasks;
using System.Windows;
using CodescoveryCaptureManager.App.Helpers;

namespace CodescoveryCaptureManager.App.Windows
{
    /// <summary>Version, links to the repository / issues / releases, and a copyable diagnostic summary.</summary>
    public partial class AboutDialog : Window
    {
        private const string CopyLabel = "Copy version info";

        public AboutDialog()
        {
            InitializeComponent();
            VersionText.Text = $"Version {AppInfo.Version}";
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            DarkTitleBar.Apply(this);
        }

        public static void Show(Window owner)
            => new AboutDialog { Owner = owner }.ShowDialog();

        private void Repository_OnClick(object sender, RoutedEventArgs e) => AppInfo.OpenInBrowser(AppInfo.RepositoryUrl);

        private void ReportIssue_OnClick(object sender, RoutedEventArgs e) => AppInfo.OpenInBrowser(AppInfo.NewIssueUrl());

        private void Releases_OnClick(object sender, RoutedEventArgs e) => AppInfo.OpenInBrowser(AppInfo.ReleasesUrl);

        private void License_OnClick(object sender, RoutedEventArgs e) => AppInfo.OpenInBrowser(AppInfo.LicenseUrl);

        private void Close_OnClick(object sender, RoutedEventArgs e) => Close();

        private async void CopyInfo_OnClick(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(AppInfo.DiagnosticInfo());
            CopyInfoText.Text = "Copied";
            await Task.Delay(TimeSpan.FromSeconds(1.5));
            CopyInfoText.Text = CopyLabel;
        }
    }
}
