using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CodescoveryCaptureManager.App.Helpers;
using CodescoveryCaptureManager.App.Tray;
using CodescoveryCaptureManager.Domain.Interfaces;
using CodescoveryCaptureManager.Domain.Structs;

namespace CodescoveryCaptureManager.App.Windows
{
    /// <summary>Every setting of the app in one place. Changes apply immediately; there is no Save button.</summary>
    public partial class SettingsWindow : Window
    {
        private const string CopyInfoLabel = "Copy version info";
        private const string CopyAddressLabel = "Copy address";
        private static readonly TimeSpan CopiedFeedback = TimeSpan.FromSeconds(1.5);

        private readonly CapturedWindows _captureWindow;
        private readonly IProfileService _profileService;
        private readonly IFocusBridge _focusBridge;
        private bool _isLoadingFallback;

        private SettingsWindow(CapturedWindows captureWindow, IProfileService profileService, IFocusBridge focusBridge)
        {
            InitializeComponent();
            _captureWindow = captureWindow;
            _profileService = profileService;
            _focusBridge = focusBridge;
            LoadGeneral();
            LoadFallback();
            LoadWebViewer();
            VersionText.Text = $"Version {AppInfo.Version}";
        }

        internal static void Show(CapturedWindows owner, IProfileService profileService, IFocusBridge focusBridge, SettingsSection section)
        {
            var window = new SettingsWindow(owner, profileService, focusBridge) { Owner = owner };
            window.Navigation.SelectedIndex = (int)section;
            window.ShowDialog();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            DarkTitleBar.Apply(this);
        }

        private void Navigation_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = (SettingsSection)Math.Max(0, Navigation.SelectedIndex);
            GeneralSection.Visibility = VisibleWhen(selected == SettingsSection.General);
            FocusSection.Visibility = VisibleWhen(selected == SettingsSection.Focus);
            WebViewerSection.Visibility = VisibleWhen(selected == SettingsSection.WebViewer);
            AboutSection.Visibility = VisibleWhen(selected == SettingsSection.About);
        }

        private static Visibility VisibleWhen(bool condition) => condition ? Visibility.Visible : Visibility.Collapsed;

        private void LoadGeneral()
        {
            ShareTitle.Text = _captureWindow.ShareTitle;
            RunInTray.IsChecked = !_profileService.Settings.ExitOnClose;
            StartWithWindows.IsChecked = StartupRegistration.IsEnabled();
            VirtualDesktopCard.Visibility = VisibleWhen(_captureWindow.CanUseVirtualDesktop);
            UpdateVirtualDesktopButton();
        }

        private void ShareTitle_OnLostFocus(object sender, RoutedEventArgs e)
        {
            _captureWindow.ShareTitle = ShareTitle.Text;
            ShareTitle.Text = _captureWindow.ShareTitle;
        }

        private void RunInTray_OnClick(object sender, RoutedEventArgs e)
        {
            var exitOnClose = RunInTray.IsChecked != true;
            _profileService.UpdateSettings(settings => settings with { ExitOnClose = exitOnClose });
        }

        private void StartWithWindows_OnClick(object sender, RoutedEventArgs e)
            => StartupRegistration.SetEnabled(StartWithWindows.IsChecked == true);

        private void VirtualDesktop_OnClick(object sender, RoutedEventArgs e)
        {
            if (_captureWindow.IsOnOwnVirtualDesktop) _captureWindow.CloseVirtualDesktop();
            else _captureWindow.MoveToVirtualDesktop();
            UpdateVirtualDesktopButton();
        }

        private void UpdateVirtualDesktopButton()
        {
            if (!_captureWindow.CanUseVirtualDesktop) return;
            VirtualDesktopButton.Content = _captureWindow.IsOnOwnVirtualDesktop ? "Bring back" : "Move";
        }

        private void LoadFallback()
        {
            _isLoadingFallback = true;
            var pickedWindows = _captureWindow.PickedWindows;
            FallbackWindow.ItemsSource = pickedWindows;
            FallbackWindow.SelectedItem = pickedWindows
                .Cast<CapturableWindow?>()
                .FirstOrDefault(window => window.Value.Handle == _captureWindow.FallbackWindowHandle);

            FallbackFixed.IsEnabled = pickedWindows.Count > 0;
            FallbackHint.Visibility = VisibleWhen(pickedWindows.Count == 0);
            FallbackMessage.IsChecked = _captureWindow.FallbackMode == FocusFallbackMode.Message;
            FallbackKeepLast.IsChecked = _captureWindow.FallbackMode == FocusFallbackMode.KeepLast;
            FallbackFixed.IsChecked = _captureWindow.FallbackMode == FocusFallbackMode.FixedWindow;
            FallbackWindow.IsEnabled = FallbackFixed.IsChecked == true;
            _isLoadingFallback = false;
        }

        // Checked (not Click) so keyboard and UI Automation selection apply too.
        private void Fallback_OnChecked(object sender, RoutedEventArgs e) => ApplyFallback();

        private void FallbackWindow_OnSelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFallback();

        private void ApplyFallback()
        {
            if (_isLoadingFallback || !IsInitialized) return;

            FallbackWindow.IsEnabled = FallbackFixed.IsChecked == true;
            if (FallbackKeepLast.IsChecked == true)
            {
                _captureWindow.SetFallback(FocusFallbackMode.KeepLast);
                return;
            }
            if (FallbackFixed.IsChecked == true)
            {
                ApplyFixedFallback();
                return;
            }
            _captureWindow.SetFallback(FocusFallbackMode.Message);
        }

        // "Always show one window" only takes effect once a window is chosen in the list.
        private void ApplyFixedFallback()
        {
            if (FallbackWindow.SelectedItem == null && FallbackWindow.Items.Count > 0)
            {
                FallbackWindow.SelectedIndex = 0;
                return;
            }
            if (FallbackWindow.SelectedItem is CapturableWindow window)
                _captureWindow.SetFallback(FocusFallbackMode.FixedWindow, window.Handle);
        }

        private void LoadWebViewer()
        {
            WebViewerAddress.Text = _focusBridge.Url;
            if (_focusBridge.IsRunning)
            {
                var viewers = _focusBridge.ViewerCount;
                WebViewerState.Text = viewers == 1 ? "Running  ·  1 viewer connected" : $"Running  ·  {viewers} viewers connected";
                return;
            }

            WebViewerState.Text = "Not available: the port is in use by another program. Close it and restart Capture Manager.";
            WebViewerState.Foreground = (System.Windows.Media.Brush)FindResource("WarningBrush");
            OpenViewer.IsEnabled = false;
            CopyAddress.IsEnabled = false;
        }

        private void OpenViewer_OnClick(object sender, RoutedEventArgs e) => AppInfo.OpenInBrowser(_focusBridge.Url);

        private async void CopyAddress_OnClick(object sender, RoutedEventArgs e)
            => await CopyWithFeedbackAsync(CopyAddress, _focusBridge.Url, CopyAddressLabel);

        private async void CopyInfo_OnClick(object sender, RoutedEventArgs e)
            => await CopyWithFeedbackAsync(CopyInfo, AppInfo.DiagnosticInfo(), CopyInfoLabel);

        private static async Task CopyWithFeedbackAsync(Button button, string text, string label)
        {
            Clipboard.SetText(text);
            button.Content = "Copied";
            await Task.Delay(CopiedFeedback);
            button.Content = label;
        }

        private void Repository_OnClick(object sender, RoutedEventArgs e) => AppInfo.OpenInBrowser(AppInfo.RepositoryUrl);

        private void ReportIssue_OnClick(object sender, RoutedEventArgs e) => AppInfo.OpenInBrowser(AppInfo.NewIssueUrl());

        private void OpenLogs_OnClick(object sender, RoutedEventArgs e) => AppInfo.OpenLogsFolder();

        private void Releases_OnClick(object sender, RoutedEventArgs e) => AppInfo.OpenInBrowser(AppInfo.ReleasesUrl);

        private void License_OnClick(object sender, RoutedEventArgs e) => AppInfo.OpenInBrowser(AppInfo.LicenseUrl);
    }
}
