using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CodescoveryCaptureManager.App.Helpers;
using CodescoveryCaptureManager.App.Tray;
using CodescoveryCaptureManager.Domain.Hotkeys;
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
            ShowPauseHotkey(_captureWindow.IsPauseHotkeyActive);
        }

        private void ShowPauseHotkey(bool isActive)
        {
            var text = _captureWindow.PauseHotkeyText;
            PauseHotkey.Text = text;
            ClearPauseHotkey.IsEnabled = text.Length > 0;
            if (text.Length == 0) ShowPauseHotkeyHint("No hotkey. Click the box and press a combination such as Ctrl+Alt+P.");
            else if (isActive) ShowPauseHotkeyHint("Active. Click the box and press another combination to change it.");
            else ShowPauseHotkeyHint("Not active: another program already uses this combination. Choose another one.", isWarning: true);
        }

        private void ShowPauseHotkeyHint(string text, bool isWarning = false)
        {
            PauseHotkeyHint.Text = text;
            PauseHotkeyHint.Foreground = (System.Windows.Media.Brush)FindResource(isWarning ? "WarningBrush" : "MutedBrush");
        }

        // While the box records keys the current hotkey is off, otherwise pressing it again would pause.
        private void PauseHotkey_OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            _captureWindow.SuspendPauseHotkey();
            ShowPauseHotkeyHint("Press the new combination (Esc to cancel).");
        }

        private void PauseHotkey_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
            => ShowPauseHotkey(_captureWindow.RegisterPauseHotkey());

        private void PauseHotkey_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Tab) return;

            e.Handled = true;
            if (key == Key.Escape)
            {
                Keyboard.ClearFocus();
                return;
            }
            if (IsModifierKey(key)) return;

            var hotkey = new Hotkey(ReadModifiers(), KeyInterop.VirtualKeyFromKey(key));
            if (!hotkey.IsValid)
            {
                ShowPauseHotkeyHint("Use Ctrl, Alt or Win together with a letter, a digit or an F key.", isWarning: true);
                return;
            }
            ShowPauseHotkey(_captureWindow.SetPauseHotkey(hotkey.ToString()));
            _captureWindow.SuspendPauseHotkey();
        }

        private static bool IsModifierKey(Key key)
            => key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;

        private static HotkeyModifiers ReadModifiers()
        {
            var modifiers = HotkeyModifiers.None;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= HotkeyModifiers.Control;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= HotkeyModifiers.Alt;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= HotkeyModifiers.Shift;
            if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)) modifiers |= HotkeyModifiers.Windows;
            return modifiers;
        }

        private void ClearPauseHotkey_OnClick(object sender, RoutedEventArgs e)
            => ShowPauseHotkey(_captureWindow.SetPauseHotkey(string.Empty));

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
