using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Windows.Graphics.Capture;
using Windows.UI.Composition;
using WindowsDesktop;
using CodescoveryCaptureManager.App.Helpers;
using CodescoveryCaptureManager.App.Tray;
using CodescoveryCaptureManager.Domain.Helpers;
using CodescoveryCaptureManager.Domain.Interfaces;
using CodescoveryCaptureManager.Domain.Profiles;
using CodescoveryCaptureManager.Domain.Services;
using CodescoveryCaptureManager.Domain.Streaming;
using CodescoveryCaptureManager.Domain.Structs;
using MaterialDesignThemes.Wpf;
using CompositionTarget = Windows.UI.Composition.CompositionTarget;
using ContainerVisual = Windows.UI.Composition.ContainerVisual;


namespace CodescoveryCaptureManager.App.Windows
{
    /// <summary>
    /// Interaction logic for CapturedWindows.xaml
    /// </summary>
    public partial class CapturedWindows : Window, IDisposable
    {
        private readonly ICapturableWindowsMonitoringService _capturableWindowsMonitoringService;
        private readonly IWindowsProcessServices _windowsProcessServices;
        private readonly Compositor _compositor;
        private readonly ContainerVisual _containerVisual;
        private readonly WindowCaptureService _windowCaptureService;
        private CompositionTarget _compositionTarget;
        private VirtualDesktop _virtualDesktop;
        private readonly VirtualDesktop _mainVirtualDesktop;
        private const string DefaultShareTitle = "Captured Window";
        private const double StatusRefreshSeconds = 1;
        private const double MaximizedFrameInset = 7;
        private readonly DispatcherTimer _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(StatusRefreshSeconds) };
        private bool _isCleanView;
        private string _shownWindowName;
        private string _shownWindowSize;
        private VideoStreamStats _lastStreamStats;
        private readonly IFocusBridge _focusBridge;
        private readonly IVideoStream _videoStream;
        private FocusFallbackMode _fallbackMode = FocusFallbackMode.Message;
        private IntPtr _fallbackWindowHandle;

        private readonly IProfileService _profileService;

        public CapturedWindows(ICapturableWindowsMonitoringService capturableWindowsMonitoringService, IWindowsProcessServices windowsProcessServices, IFocusBridge focusBridge, IProfileService profileService, IVideoStream videoStream)
        {

            InitializeComponent();
            _capturableWindowsMonitoringService = capturableWindowsMonitoringService;
            _windowsProcessServices = windowsProcessServices;
            _focusBridge = focusBridge;
            _videoStream = videoStream;
            _profileService = profileService;
            _fallbackMode = Enum.TryParse<FocusFallbackMode>(_profileService.Settings.FallbackMode, out var savedMode) && savedMode != FocusFallbackMode.FixedWindow
                ? savedMode
                : FocusFallbackMode.Message;
            _profileService.Changed += OnProfilesChanged;
            _focusBridge.Start();
            _compositor = new Compositor();
            _containerVisual = InitializeContainerVisual();
            _windowCaptureService = new WindowCaptureService(_compositor, _videoStream);
            _mainVirtualDesktop = GetMainVirtualDesktop();
            Title = _profileService.Settings.WindowTitle ?? Title;
            SetupEvents();
            StartCapturing();
        }



        private VirtualDesktop GetMainVirtualDesktop()
        {
            return !VirtualDesktop.IsSupported ? null : VirtualDesktop.Current;
        }
        private VirtualDesktop CreateNewDesktop()
        {
            return !VirtualDesktop.IsSupported ? null : VirtualDesktop.Create();
        }
        private ContainerVisual InitializeContainerVisual()
        {
            var containerVisual = _compositor.CreateContainerVisual();
            containerVisual.RelativeSizeAdjustment = Vector2.One;
            containerVisual.Size = new Vector2(0, 0);
            containerVisual.Offset = new Vector3(0, 0, 0);
            containerVisual.BorderMode = CompositionBorderMode.Soft;
            return containerVisual;
        }

        private void StartCapturing()
        {
            _ = RunMonitoringAsync();
        }

        private async Task RunMonitoringAsync()
        {
            try
            {
                await _capturableWindowsMonitoringService.StartMonitoring();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Monitoring stopped unexpectedly. {ex.Message}");
            }
        }

        private void CapturableWindowsMonitoringServiceOnMonitoredWindowClosed(CapturableWindow closedcapturablewindow)
        {
            if (_windowCaptureService != null && _windowCaptureService.IsCapturing(closedcapturablewindow.Handle))
                _windowCaptureService.StopCapture();
            Dispatcher.BeginInvoke(RefreshView);
        }

        private void OnMonitoredWindowsBlurred()
        {
            _focusBridge.PublishBlurred();
            ApplyFocusFallback();
            RefreshView();
        }

        // What the view shows while no selected window is focused (user setting, see settings menu).
        private void ApplyFocusFallback()
        {
            if (_fallbackMode == FocusFallbackMode.KeepLast) return;
            if (_fallbackMode == FocusFallbackMode.FixedWindow && TryShowFallbackWindow()) return;
            _windowCaptureService?.StopCapture();
        }

        private bool TryShowFallbackWindow()
        {
            if (!_capturableWindowsMonitoringService.IsMonitored(_fallbackWindowHandle))
            {
                SetFallback(FocusFallbackMode.Message);
                return false;
            }
            ShowCapturedWindow(_capturableWindowsMonitoringService.GetCapturableWindow(_fallbackWindowHandle));
            return true;
        }

        internal FocusFallbackMode FallbackMode => _fallbackMode;

        internal IntPtr FallbackWindowHandle => _fallbackWindowHandle;

        internal IReadOnlyList<CapturableWindow> PickedWindows => _capturableWindowsMonitoringService.GetMonitoredWindows();

        /// <summary>Title of this window in the Teams / Meet / OBS share picker.</summary>
        internal string ShareTitle
        {
            get => Title;
            set
            {
                var title = string.IsNullOrWhiteSpace(value) ? DefaultShareTitle : value.Trim();
                Title = title;
                _profileService.UpdateSettings(settings => settings with { WindowTitle = title });
            }
        }

        internal void SetFallback(FocusFallbackMode mode, IntPtr fixedWindowHandle = default)
        {
            _fallbackMode = mode;
            _fallbackWindowHandle = fixedWindowHandle;
            // A fixed window is a live handle: not persisted, the saved mode stays as it was.
            if (mode != FocusFallbackMode.FixedWindow)
                _profileService.UpdateSettings(settings => settings with { FallbackMode = mode.ToString() });
            if (_capturableWindowsMonitoringService.IsBlurred)
                OnMonitoredWindowsBlurred();
        }

        // One place decides what the window says: overlays, status pill and status bar.
        private void RefreshView()
        {
            var hasMonitoredWindows = _capturableWindowsMonitoringService.HasMonitoredWindows();
            var isShowingWindow = _windowCaptureService?.IsCapturing() ?? false;

            // An active profile with no open window yet is set up: it is waiting, not empty.
            var isWaitingForProfile = !hasMonitoredWindows && _profileService.Active != null;
            var isSetUp = hasMonitoredWindows || isWaitingForProfile;

            EmptyState.Visibility = isSetUp ? Visibility.Collapsed : Visibility.Visible;
            NoFocusState.Visibility = isSetUp && !isShowingWindow ? Visibility.Visible : Visibility.Collapsed;
            ShowStatus(hasMonitoredWindows, isShowingWindow, isWaitingForProfile);
        }

        private void ShowStatus(bool hasMonitoredWindows, bool isShowingWindow, bool isWaitingForProfile)
        {
            if (isWaitingForProfile)
            {
                SetStatus("WarningBrush", "Waiting", $"Profile {_profileService.Active.Name}  \u00B7  its windows appear here when you open and focus them");
                return;
            }
            if (!hasMonitoredWindows)
            {
                SetStatus("FaintBrush", "Not set up", "Choose windows to get started.");
                return;
            }
            if (isShowingWindow)
            {
                SetStatus("BrandBrush", "Live", $"Showing {_shownWindowName}  \u00B7  {_shownWindowSize}");
                return;
            }
            var selected = _capturableWindowsMonitoringService.GetMonitoredWindows().Count;
            SetStatus("WarningBrush", "Paused", $"{selected} window{(selected == 1 ? string.Empty : "s")} selected  \u00B7  nothing is shown until you focus one");
        }

        private void SetStatus(string dotBrushKey, string pill, string detail)
        {
            StatusDot.Fill = (Brush)FindResource(dotBrushKey);
            StatusPillText.Text = pill;
            StatusText.Text = detail;
        }

        private void RefreshStreamStatus()
        {
            if (!_focusBridge.IsRunning)
            {
                ShowWebViewerUnavailable();
                return;
            }

            var viewers = _focusBridge.ViewerCount;
            ViewerCountText.Text = viewers == 1 ? "1 viewer" : $"{viewers} viewers";
            WebViewerText.Text = new Uri(_focusBridge.Url).Authority;
            StreamStatsText.Text = DescribeStream(_videoStream.Stats);
        }

        private void ShowWebViewerUnavailable()
        {
            ViewerCountText.Text = "0 viewers";
            WebViewerText.Text = "Web viewer unavailable";
            WebViewerButton.IsEnabled = false;
            WebViewerButton.ToolTip = "The web viewer port is in use by another program.";
            CopyWebViewerButton.Visibility = Visibility.Collapsed;
            EmptyStateWebViewerButton.Visibility = Visibility.Collapsed;
            StreamStatsText.Text = string.Empty;
        }

        private string DescribeStream(VideoStreamStats stats)
        {
            var framesPerSecond = stats.FramesPerSecondSince(_lastStreamStats, StatusRefreshSeconds);
            _lastStreamStats = stats;
            return stats.IsEncoding
                ? $"Streaming {stats.Width} \u00D7 {stats.Height}  \u00B7  {framesPerSecond:0} fps"
                : string.Empty;
        }

        private void OpenWebViewer_OnClick(object sender, RoutedEventArgs e) => AppInfo.OpenInBrowser(_focusBridge.Url);

        private async void CopyWebViewer_OnClick(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(_focusBridge.Url);
            CopyWebViewerIcon.Kind = PackIconKind.Check;
            await Task.Delay(TimeSpan.FromSeconds(1.5));
            CopyWebViewerIcon.Kind = PackIconKind.ContentCopy;
        }

        private void ProfilesButton_OnClick(object sender, RoutedEventArgs e)
        {
            ProfilesButton.ContextMenu.PlacementTarget = ProfilesButton;
            ProfilesButton.ContextMenu.IsOpen = true;
        }

        // Rebuilt on every open so it always reflects the saved profiles.
        private void ProfilesMenu_OnOpened(object sender, RoutedEventArgs e)
        {
            ProfilesMenu.Items.Clear();
            var profiles = _profileService.Profiles;
            if (profiles.Count == 0)
                ProfilesMenu.Items.Add(new MenuItem { Header = "No saved profiles", IsEnabled = false });

            foreach (var profile in profiles)
                ProfilesMenu.Items.Add(CreateProfileItem(profile));

            ProfilesMenu.Items.Add(new Separator());
            ProfilesMenu.Items.Add(CreateSaveCurrentItem());
            ProfilesMenu.Items.Add(CreateDeleteMenu(profiles));
        }

        private MenuItem CreateProfileItem(CaptureProfile profile)
        {
            var item = new MenuItem
            {
                Header = profile.Name,
                IsCheckable = true,
                IsChecked = _profileService.Active?.Id == profile.Id,
                ToolTip = DescribeRules(profile),
            };
            item.Click += (_, _) => ApplyProfile(profile.Id);
            return item;
        }

        private MenuItem CreateSaveCurrentItem()
        {
            var item = new MenuItem
            {
                Header = "Save current windows as profile...",
                IsEnabled = _capturableWindowsMonitoringService.HasMonitoredWindows(),
            };
            item.Click += (_, _) => SaveCurrentWindowsAsProfile();
            return item;
        }

        private MenuItem CreateDeleteMenu(IReadOnlyList<CaptureProfile> profiles)
        {
            var deleteMenu = new MenuItem { Header = "Delete profile", IsEnabled = profiles.Count > 0 };
            foreach (var profile in profiles)
            {
                var item = new MenuItem { Header = profile.Name };
                item.Click += (_, _) => ConfirmAndDelete(profile);
                deleteMenu.Items.Add(item);
            }
            return deleteMenu;
        }

        private static string DescribeRules(CaptureProfile profile)
            => string.Join(Environment.NewLine, profile.Rules.Select(rule =>
                rule.TitleMatch == TitleMatchMode.Any ? rule.ProcessName : $"{rule.ProcessName}: {rule.TitleMatch} \"{rule.TitlePattern}\""));

        public void ApplyProfile(Guid profileId)
        {
            try
            {
                var result = _profileService.Apply(profileId);
                _windowCaptureService?.StopCapture();
                _capturableWindowsMonitoringService.SyncWithForegroundWindow();
                RefreshView();
                ProfilesButton.ToolTip = DescribeApplyResult(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not apply profile. {ex.Message}");
            }
        }

        private static string DescribeApplyResult(ProfileApplyResult result)
            => result.WaitingRules.Count == 0
                ? $"{result.BoundWindows} window(s) bound."
                : $"{result.BoundWindows} window(s) bound; waiting for: {string.Join(", ", result.WaitingRules.Select(rule => rule.ProcessName))}.";

        private void SaveCurrentWindowsAsProfile()
        {
            var rules = WindowRuleFactory.FromWindows(_capturableWindowsMonitoringService.GetMonitoredWindows());
            var profile = SaveProfileDialog.Prompt(this, string.Empty, rules);
            if (profile != null)
                _profileService.Save(profile, makeActive: true);
        }

        private void ConfirmAndDelete(CaptureProfile profile)
        {
            var answer = MessageBox.Show(this, $"Delete profile \"{profile.Name}\"?", "Delete profile", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Yes)
                _profileService.Delete(profile.Id);
        }

        private void OnProfilesChanged()
        {
            UpdateProfileLabel();
            if (IsLoaded) RefreshView();
        }

        private void UpdateProfileLabel()
            => ProfileLabel.Text = _profileService.Active?.Name ?? "Profiles";

        private void OnPickerClosed()
        {
            var isShowingWindow = _windowCaptureService?.IsCapturing() ?? false;
            if (_fallbackMode == FocusFallbackMode.FixedWindow && !isShowingWindow)
                TryShowFallbackWindow();
            RefreshView();
        }

        private void SettingsButton_OnClick(object sender, RoutedEventArgs e) => ShowSettings(SettingsSection.General);

        private void ShowSettings(SettingsSection section)
            => SettingsWindow.Show(this, _profileService, _focusBridge, section);

        private void StopCapturing()
        {
            _capturableWindowsMonitoringService.StopMonitoring();
            _windowCaptureService?.StopCapture();
        }
        private void SetupEvents()
        {
            _statusTimer.Tick += (_, _) => RefreshStreamStatus();
            PreviewKeyDown += CapturedWindows_OnPreviewKeyDown;
            _capturableWindowsMonitoringService.MonitoredWindowFocused += CapturableWindowsMonitoringServiceOnMonitoredWindowFocused;
            _capturableWindowsMonitoringService.MonitoredWindowClosed += CapturableWindowsMonitoringServiceOnMonitoredWindowClosed;
            _capturableWindowsMonitoringService.MonitoredWindowsBlurred += OnMonitoredWindowsBlurred;
        }

        private void CapturableWindowsMonitoringServiceOnMonitoredWindowFocused(CapturableWindow activecapturablewindow)
        {
            if (_windowCaptureService == null) return;
            _focusBridge.Publish(activecapturablewindow);
            ShowCapturedWindow(activecapturablewindow);
        }

        private void ShowCapturedWindow(CapturableWindow window)
        {
            try
            {
                if (_windowCaptureService.IsCapturing(window.Handle)) return;

                var item = CaptureHelper.CreateItemForWindow(window.Handle);
                if (item == null) return;

                _windowCaptureService.StartCapture(item, window.Handle);
                _shownWindowName = window.Name;
                _shownWindowSize = $"{item.Size.Width} \u00D7 {item.Size.Height}";
                RefreshView();
            }
            catch (Exception ex)
            {
                _windowCaptureService?.StopCapture();
                Console.WriteLine($"Error capturing {window.Name}. {ex.Message}");
            }
        }


        internal bool CanUseVirtualDesktop => VirtualDesktop.IsSupported;

        internal bool IsOnOwnVirtualDesktop
            => _virtualDesktop != null && VirtualDesktop.GetDesktops().ToList().Exists(desktop => desktop.Id.Equals(_virtualDesktop.Id));

        public  void OpenCapturePicker(object sender, RoutedEventArgs e)
        {
            var selectCapturableWindows = new SelectCapturableWindows(_windowsProcessServices, _capturableWindowsMonitoringService, _windowCaptureService, _profileService);
            selectCapturableWindows.Closed += (_, _) => OnPickerClosed();
            selectCapturableWindows.Pick(this);
        }

        public void Dispose()
        {
            StopCapturing();
            _focusBridge?.Dispose();
            _windowsProcessServices?.Dispose();
            _capturableWindowsMonitoringService?.Dispose();
        }

        private void CapturedWindows_OnLoaded(object sender, RoutedEventArgs e)
        {
            _compositionTarget = InitializeDesktopWindowTarget();
            _compositionTarget.Root = _containerVisual;
            _containerVisual.Children.InsertAtTop(_windowCaptureService.GetVisual());
            UpdateProfileLabel();
            UpdateCaptureBounds();
            RefreshStreamStatus();
            _statusTimer.Start();
            if (_profileService.Active != null)
                ApplyProfile(_profileService.Active.Id);
            RefreshView();
        }
        private CompositionTarget InitializeDesktopWindowTarget()
        {
            var compositionTarget = _compositor.CreateDesktopWindowTarget(new WindowInteropHelper(this).Handle, true);
            return compositionTarget;
        }
        // Clean view: viewers of the shared window see only the captured content.
        private void CleanView_OnClick(object sender, RoutedEventArgs e) => SetCleanView(!_isCleanView);

        private void SetCleanView(bool isCleanView)
        {
            _isCleanView = isCleanView;
            var controls = isCleanView ? Visibility.Collapsed : Visibility.Visible;
            TitleBar.Visibility = controls;
            StatusBar.Visibility = controls;
            CleanViewBar.Visibility = isCleanView ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CapturedWindows_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.H || Keyboard.Modifiers != ModifierKeys.Control) return;
            SetCleanView(!_isCleanView);
            e.Handled = true;
        }

        // The Composition visual is drawn over the WPF content, so it must cover the capture area only.
        private void UpdateCaptureBounds()
        {
            if (!IsLoaded) return;

            var dpi = VisualTreeHelper.GetDpi(this);
            var origin = ScreenCaptureGrid.TranslatePoint(new Point(0, 0), this);
            _containerVisual.RelativeSizeAdjustment = Vector2.Zero;
            _containerVisual.Offset = new Vector3((float)(origin.X * dpi.DpiScaleX), (float)(origin.Y * dpi.DpiScaleY), 0);
            _containerVisual.Size = new Vector2((float)(ScreenCaptureGrid.ActualWidth * dpi.DpiScaleX), (float)(ScreenCaptureGrid.ActualHeight * dpi.DpiScaleY));
        }

        private void ScreenCaptureGrid_OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdateCaptureBounds();

        // A maximized window with a custom frame overflows the screen by the resize border.
        private void CapturedWindows_OnStateChanged(object sender, EventArgs e)
        {
            var isMaximized = WindowState == WindowState.Maximized;
            RootBorder.Padding = new Thickness(isMaximized ? MaximizedFrameInset : 0);
            MaximizeGlyph.Text = isMaximized ? "\uE923" : "\uE922";
            WindowMaximizeButton.ToolTip = isMaximized ? "Restore" : "Maximize";
            Dispatcher.BeginInvoke(UpdateCaptureBounds, DispatcherPriority.Loaded);
        }

        private void CapturedWindows_OnClosing(object sender, CancelEventArgs e)
        {
            var exiting = ((App)Application.Current).ExitRequested || _profileService.Settings.ExitOnClose;
            if (!exiting)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            Dispose();
        }

        /// <summary>Starts with the window hidden (tray only). The window is shown once so Composition binds to its HWND.</summary>
        public void StartHidden()
        {
            ShowActivated = false;
            Show();
            Hide();
        }

        public void ShowFromTray()
        {
            if (!IsVisible) Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
        }

        public void ShowAbout()
        {
            ShowFromTray();
            ShowSettings(SettingsSection.About);
        }

        internal void MoveToVirtualDesktop()
        {
            if (_virtualDesktop == null && !VirtualDesktop.IsSupported) return;
            _virtualDesktop = CreateNewDesktop();

            _virtualDesktop.SwitchAndMove(this);
            _mainVirtualDesktop.Switch();
        }

        internal void CloseVirtualDesktop()
        {
            if (IsOnOwnVirtualDesktop)
                _virtualDesktop.Remove(_mainVirtualDesktop);
            _virtualDesktop = null;
        }

        private void WindowMinimizeButton_OnClick(object sender, RoutedEventArgs e)=> WindowState = WindowState.Minimized;

        private void MaximizeButton_OnClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void WindowCloseButton_OnClick(object sender, RoutedEventArgs e) => Close();

        // The capture area drags the window (the title bar may be hidden); double-click toggles maximize.
        private void ScreenCaptureGrid_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                MaximizeButton_OnClick(sender, e);
                return;
            }
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
