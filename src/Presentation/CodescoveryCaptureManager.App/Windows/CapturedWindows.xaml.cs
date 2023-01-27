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
using CodescoveryCaptureManager.Domain.Services;
using CodescoveryCaptureManager.Domain.Structs;
using CodescoveryCaptureManager.Domain.Extensions;
using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors.Core;
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
        public CapturedWindows(ICapturableWindowsMonitoringService capturableWindowsMonitoringService, IWindowsProcessServices windowsProcessServices)
        {

            InitializeComponent();
            _capturableWindowsMonitoringService = capturableWindowsMonitoringService;
            _windowsProcessServices = windowsProcessServices;
            _compositor = new Compositor();
            _containerVisual = InitializeContainerVisual();
            _windowCaptureService = new WindowCaptureService(_compositor);
            _mainVirtualDesktop = GetMainVirtualDesktop();
            HandleFooterMargin();
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
            _capturableWindowsMonitoringService.StartMonitoring();
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
        }

        private void CapturableWindowsMonitoringServiceOnMonitoredWindowFocused(CapturableWindow activecapturablewindow)
        {
            try
            {
                if (_windowCaptureService == null) return;

                var item = CaptureHelper.CreateItemForWindow(activecapturablewindow.Handle);
                if (item == null) return;
                if (_windowCaptureService.IsCapturing(activecapturablewindow.Handle)) return;

                _windowCaptureService.StartCapture(item, activecapturablewindow.Handle);
            }
            catch (Exception ex)
            {
                _windowCaptureService?.StopCapture();
                Console.WriteLine($"Error capturing {activecapturablewindow.Name}. {ex.Message}");
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
            ConfigureVirtualDesktopActions();
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

        public async void OpenCapturePicker(object sender, RoutedEventArgs e)
        {
            var selectCapturableWindows = new SelectCapturableWindows(_windowsProcessServices, _capturableWindowsMonitoringService, _windowCaptureService);
            selectCapturableWindows.Pick(this);
            //Console.WriteLine("OpenCapturePicker");
            //await Dispatcher.InvokeAsync(OpenCapturePicker);

        }

        //private async void OpenCapturePicker()
        //{
        //    var picker = new GraphicsCapturePicker();
        //    var windowHandle = this.GetHandler();
        //    picker.SetWindow(windowHandle);
        //    //var interop = (IInitializeWithWindow)(object)picker;
        //    //interop.Initialize(windowHandle);
        //    var item =  await picker.PickSingleItemAsync();
        //    if (item == null) return;
        //    var openedWindowMenuItem = new MenuItem
        //    {
        //        Header = item.DisplayName,
        //        IsCheckable = true,
        //        IsChecked = _capturableWindowsMonitoringService.IsMonitored(openedWindow.Handle),
        //        StaysOpenOnClick = true
        //    };
        //    _windowCaptureService.StopCapture();

        //    if (_capturableWindowsMonitoringService.IsMonitored(item.Handle))
        //        //                _capturableWindowsMonitoringService.RemoveMonitoringWindow(openedWindow.Handle);
        //        //            else
        //        //                _capturableWindowsMonitoringService.AddMonitoringWindow(openedWindow);
        //        //            _capturableWindowsMonitoringService.FocusFirstMonitoringWindow();
        //        _windowCaptureService.StartCapture(item, windowHandle);
        //}

        //private void CreateOpenedWindowsMenuItems(IEnumerable<CapturableWindow> openedWindows)
        //{

        //    SelectCapturingWindow.Items.Clear();
        //    SelectCapturingWindow.StaysOpenOnClick = true;
        //    foreach (var openedWindow in openedWindows)
        //    {
        //        var openedWindowMenuItem = new MenuItem
        //        {
        //            Header = openedWindow.Name,
        //            IsCheckable = true,
        //            IsChecked = _capturableWindowsMonitoringService.IsMonitored(openedWindow.Handle),
        //            StaysOpenOnClick = true
        //        };
        //        openedWindowMenuItem.Click += delegate (object sender, RoutedEventArgs args)
        //        {
        //            _windowCaptureService.StopCapture();
        //            if (_capturableWindowsMonitoringService.IsMonitored(openedWindow.Handle))
        //                _capturableWindowsMonitoringService.RemoveMonitoringWindow(openedWindow.Handle);
        //            else
        //                _capturableWindowsMonitoringService.AddMonitoringWindow(openedWindow);
        //            _capturableWindowsMonitoringService.FocusFirstMonitoringWindow();
        //        };
        //        SelectCapturingWindow.Items.Add(openedWindowMenuItem);
        //    }
        //}

        public void Dispose()
        {
            StopCapturing();
            _windowsProcessServices?.Dispose();
            _capturableWindowsMonitoringService?.Dispose();
        }

        private void CapturedWindows_OnLoaded(object sender, RoutedEventArgs e)
        {
            _compositionTarget = InitializeDesktopWindowTarget();
            _compositionTarget.Root = _containerVisual;
            _containerVisual.Children.InsertAtTop(_windowCaptureService.GetVisual());
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
            SwitchMenuExpanderIcon();
        }

        private void SwitchMenuExpanderIcon()
        {
            MenuExpander.Kind = _isMenuExpanded ? PackIconKind.ArrowUp : PackIconKind.ArrowDown;
        }

        private void CapturedWindows_OnClosing(object sender, CancelEventArgs e)
        {
            StopCapturing();
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
            DragMove();
        }

        private void CapturedWindowsMenu_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void CapturedWindows_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            MaximizeButton_OnClick(sender, e);
        }

        private void CapturedWindows_OnStateChanged(object sender, EventArgs e)
        {
            HandleFooterMargin();
        }

        private void HandleFooterMargin()
        {
            FooterMenu.Margin = WindowState == WindowState.Maximized ? new Thickness(0, 0, 10, 50) : new Thickness(0, 0, 0, 0);
        }
    }
}
