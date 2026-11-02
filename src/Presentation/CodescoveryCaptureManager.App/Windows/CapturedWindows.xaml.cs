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
using Windows.Graphics.Capture;
using Windows.UI.Composition;
using WindowsDesktop;
using CodescoveryCaptureManager.Domain.Helpers;
using CodescoveryCaptureManager.Domain.Interfaces;
using CodescoveryCaptureManager.Domain.Profiles;
using CodescoveryCaptureManager.Domain.Services;
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
        private bool _isMenuExpanded = true;
        private readonly IFocusBridge _focusBridge;
        private FocusFallbackMode _fallbackMode = FocusFallbackMode.Message;
        private IntPtr _fallbackWindowHandle;

        private enum FocusFallbackMode { Message, KeepLast, FixedWindow }

        private readonly IProfileService _profileService;

        public CapturedWindows(ICapturableWindowsMonitoringService capturableWindowsMonitoringService, IWindowsProcessServices windowsProcessServices, IFocusBridge focusBridge, IProfileService profileService)
        {

            InitializeComponent();
            _capturableWindowsMonitoringService = capturableWindowsMonitoringService;
            _windowsProcessServices = windowsProcessServices;
            _focusBridge = focusBridge;
            _profileService = profileService;
            _fallbackMode = Enum.TryParse<FocusFallbackMode>(_profileService.Settings.FallbackMode, out var savedMode) && savedMode != FocusFallbackMode.FixedWindow
                ? savedMode
                : FocusFallbackMode.Message;
            _profileService.Changed += UpdateProfileLabel;
            _focusBridge.Start();
            _compositor = new Compositor();
            _containerVisual = InitializeContainerVisual();
            _windowCaptureService = new WindowCaptureService(_compositor);
            _mainVirtualDesktop = GetMainVirtualDesktop();
            //HandleFooterMargin();
            SwitchMenuExpanderIcon();
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
            Dispatcher.BeginInvoke(UpdateEmptyState);
        }

        private void OnMonitoredWindowsBlurred()
        {
            _focusBridge.PublishBlurred();
            ApplyFocusFallback();
            UpdateEmptyState();
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

        private void SetFallback(FocusFallbackMode mode, IntPtr fixedWindowHandle = default)
        {
            _fallbackMode = mode;
            _fallbackWindowHandle = fixedWindowHandle;
            // A fixed window is a live handle: not persisted, the saved mode stays as it was.
            if (mode != FocusFallbackMode.FixedWindow)
                _profileService.UpdateSettings(settings => settings with { FallbackMode = mode.ToString() });
            if (_capturableWindowsMonitoringService.IsBlurred)
                OnMonitoredWindowsBlurred();
        }

        private void FallbackMessage_OnClick(object sender, RoutedEventArgs e) => SetFallback(FocusFallbackMode.Message);

        private void FallbackKeepLast_OnClick(object sender, RoutedEventArgs e) => SetFallback(FocusFallbackMode.KeepLast);

        private void FallbackWindow_OnClick(object sender, RoutedEventArgs e)
            => SetFallback(FocusFallbackMode.FixedWindow, (IntPtr)((MenuItem)sender).Tag);

        // Rebuilt each time the menu opens: one checkable entry per picked window plus the two fixed modes.
        private void ConfigureFallbackMenu()
        {
            foreach (var staleItem in FallbackMenu.Items.OfType<MenuItem>().Where(item => item.Tag is IntPtr).ToList())
                FallbackMenu.Items.Remove(staleItem);

            foreach (var window in _capturableWindowsMonitoringService.GetMonitoredWindows())
                FallbackMenu.Items.Add(CreateFallbackWindowItem(window));

            FallbackWindowsSeparator.Visibility = FallbackMenu.Items.OfType<MenuItem>().Any(item => item.Tag is IntPtr)
                ? Visibility.Visible
                : Visibility.Collapsed;
            FallbackMessage.IsChecked = _fallbackMode == FocusFallbackMode.Message;
            FallbackKeepLast.IsChecked = _fallbackMode == FocusFallbackMode.KeepLast;
        }

        private MenuItem CreateFallbackWindowItem(CapturableWindow window)
        {
            var item = new MenuItem
            {
                Header = $"Always show: {window.Name}",
                IsCheckable = true,
                IsChecked = _fallbackMode == FocusFallbackMode.FixedWindow && _fallbackWindowHandle == window.Handle,
                Tag = window.Handle,
            };
            item.Click += FallbackWindow_OnClick;
            return item;
        }

        private void UpdateEmptyState()
        {
            var hasMonitoredWindows = _capturableWindowsMonitoringService.HasMonitoredWindows();
            var isShowingWindow = _windowCaptureService?.IsCapturing() ?? false;

            EmptyState.Visibility = hasMonitoredWindows ? Visibility.Collapsed : Visibility.Visible;
            NoFocusState.Visibility = hasMonitoredWindows && !isShowingWindow ? Visibility.Visible : Visibility.Collapsed;
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

        private void ApplyProfile(Guid profileId)
        {
            try
            {
                var result = _profileService.Apply(profileId);
                _windowCaptureService?.StopCapture();
                _capturableWindowsMonitoringService.SyncWithForegroundWindow();
                UpdateEmptyState();
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

        private void UpdateProfileLabel()
            => ProfileLabel.Text = _profileService.Active?.Name ?? "Profiles";

        private void OnPickerClosed()
        {
            var isShowingWindow = _windowCaptureService?.IsCapturing() ?? false;
            if (_fallbackMode == FocusFallbackMode.FixedWindow && !isShowingWindow)
                TryShowFallbackWindow();
            UpdateEmptyState();
        }

        private void SettingsButton_OnClick(object sender, RoutedEventArgs e)
        {
            SettingsButton.ContextMenu.PlacementTarget = SettingsButton;
            SettingsButton.ContextMenu.IsOpen = true;
        }
        private void StopCapturing()
        {
            _capturableWindowsMonitoringService.StopMonitoring();
            _windowCaptureService?.StopCapture();
        }
        private void SetupEvents()
        {
            CurrentWindowTitle.Text = Title;
            CurrentWindowTitle.TextChanged += CurrentWindowTitleOnTextChanged;
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
                UpdateEmptyState();
            }
            catch (Exception ex)
            {
                _windowCaptureService?.StopCapture();
                Console.WriteLine($"Error capturing {window.Name}. {ex.Message}");
            }
        }


        private void CurrentWindowTitleOnTextChanged(object sender, TextChangedEventArgs e)
        {
            Title = CurrentWindowTitle.Text;
        }



        private void SettingsMenu_OnSubmenuOpened(object sender, RoutedEventArgs e)
        {
            //CurrentWindowTitle.Text = Title;
            //CreateOpenedWindowsMenuItems(_windowsProcessServices.GetOpenedWindows(this));
            ConfigureFallbackMenu();
            ConfigureVirtualDesktopActions();
            CurrentWindowTitleOnTextChanged(this,null);
        }

        private void ConfigureVirtualDesktopActions()
        {
            if (!VirtualDesktop.IsSupported) return;

            VirtualDesktopActions.IsEnabled = true;
            if (_virtualDesktop == null)
                MoveToVirtualDesktop.IsEnabled = true;
            else if (VirtualDesktop.GetDesktops().ToList().Exists(p => p.Id.Equals(_virtualDesktop.Id)))
                CloseVirtualDesktop.IsEnabled = true;
            else
                MoveToVirtualDesktop.IsEnabled = true;
        }

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
            if (_profileService.Active != null)
                ApplyProfile(_profileService.Active.Id);
            UpdateEmptyState();
        }
        private CompositionTarget InitializeDesktopWindowTarget()
        {
            var compositionTarget = _compositor.CreateDesktopWindowTarget(new WindowInteropHelper(this).Handle, true);
            return compositionTarget;
        }
        private void MenuExpander_OnClick(object sender, RoutedEventArgs e)
        {
            CapturedWindowsMenu.Visibility = CapturedWindowsMenu.Visibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;
            _isMenuExpanded = CapturedWindowsMenu.Visibility != Visibility.Collapsed;
            ShowToolbarButton.Visibility = _isMenuExpanded ? Visibility.Collapsed : Visibility.Visible;
            SwitchMenuExpanderIcon();
        }

        private void SwitchMenuExpanderIcon()
        {
            MenuExpander.Kind = _isMenuExpanded ? PackIconKind.ArrowUp : PackIconKind.ArrowDown;
        }

        private void CapturedWindows_OnClosing(object sender, CancelEventArgs e)
        {
            Dispose();
        }

        private void MoveToVirtualDesktop_OnClick(object sender, RoutedEventArgs e)
        {
            if (_virtualDesktop == null && !VirtualDesktop.IsSupported) return;
            _virtualDesktop = CreateNewDesktop();

            _virtualDesktop.SwitchAndMove(this);
            _mainVirtualDesktop.Switch();
            MoveToVirtualDesktop.IsEnabled = false;
        }

        private void CloseVirtualDesktop_OnClick(object sender, RoutedEventArgs e)
        {
            if (VirtualDesktop.GetDesktops().ToList().Exists(p => p.Id.Equals(_virtualDesktop.Id)))
                _virtualDesktop?.Remove(_mainVirtualDesktop);
            _virtualDesktop = null;
            CloseVirtualDesktop.IsEnabled = false;
        }

        private void WindowMinimizeButton_OnClick(object sender, RoutedEventArgs e)=> WindowState = WindowState.Minimized;

        private void MaximizeButton_OnClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void WindowCloseButton_OnClick(object sender, RoutedEventArgs e) => Close();

        private void CapturedWindows_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        // Toolbar background drags the window; clicks inside the title box must not.
        private void CapturedWindowsMenu_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = IsInsideTextBox(e.OriginalSource as DependencyObject);
        }

        private static bool IsInsideTextBox(DependencyObject element)
        {
            while (element != null)
            {
                if (element is TextBox) return true;
                element = element is System.Windows.Media.Visual
                    ? System.Windows.Media.VisualTreeHelper.GetParent(element)
                    : LogicalTreeHelper.GetParent(element);
            }
            return false;
        }

        private void CapturedWindows_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            MaximizeButton_OnClick(sender, e);
        }

        //private void CapturedWindows_OnStateChanged(object sender, EventArgs e)
        //{
        //    HandleFooterMargin();
        //}

        //private void HandleFooterMargin()
        //{
        //    FooterMenu.Margin = WindowState == WindowState.Maximized ? new Thickness(0, 0, 10, 50) : new Thickness(0, 0, 0, 0);
        //}
    }
}
