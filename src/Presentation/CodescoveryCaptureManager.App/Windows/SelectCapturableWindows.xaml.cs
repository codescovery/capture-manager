using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using CodescoveryCaptureManager.App.Helpers;
using CodescoveryCaptureManager.Domain.Interfaces;
using CodescoveryCaptureManager.Domain.Services;

namespace CodescoveryCaptureManager.App.Windows
{
    /// <summary>
    /// Picker: shows every capturable window as a card with a live thumbnail; the selection
    /// becomes the monitored set when applied.
    /// </summary>
    public partial class SelectCapturableWindows : Window
    {
        private const int MaxParallelThumbnails = 4;

        private readonly IWindowsProcessServices _windowsProcessServices;
        private readonly ICapturableWindowsMonitoringService _capturableWindowsMonitoringService;
        private readonly WindowCaptureService _windowCaptureService;
        private readonly CancellationTokenSource _closing = new();
        private List<WindowPickerItem> _items = new();
        private ICollectionView _view;

        public SelectCapturableWindows(IWindowsProcessServices windowsProcessServices,
            ICapturableWindowsMonitoringService capturableWindowsMonitoringService,
            WindowCaptureService windowCaptureService)
        {
            _windowsProcessServices = windowsProcessServices;
            _capturableWindowsMonitoringService = capturableWindowsMonitoringService;
            _windowCaptureService = windowCaptureService;
            InitializeComponent();
        }

        public void Pick(params Window[] ignoredWindows)
        {
            Owner = ignoredWindows?.FirstOrDefault();
            var ignoredWindowsWithMe = (ignoredWindows ?? Array.Empty<Window>()).Append(this).ToArray();

            _items = BuildItems(ignoredWindowsWithMe);
            _view = CollectionViewSource.GetDefaultView(_items);
            _view.Filter = item => ((WindowPickerItem)item).Matches(SearchBox.Text);
            WindowCards.ItemsSource = _view;

            RefreshState();
            Show();
            _ = LoadVisualsAsync(_closing.Token);
        }

        private List<WindowPickerItem> BuildItems(Window[] ignoredWindows)
            => _windowsProcessServices.GetOpenedWindows(ignoredWindows)
                .Select(window => new WindowPickerItem(window, _capturableWindowsMonitoringService.IsMonitored(window.Handle)))
                .OrderByDescending(item => item.WasMonitored)
                .ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

        private async Task LoadVisualsAsync(CancellationToken cancellationToken)
        {
            using var limiter = new SemaphoreSlim(MaxParallelThumbnails);
            try
            {
                await Task.WhenAll(_items.Select(item => LoadItemVisualsAsync(item, limiter, cancellationToken)));
            }
            catch (OperationCanceledException)
            {
                // Picker closed before every thumbnail finished.
            }
        }

        private static async Task LoadItemVisualsAsync(WindowPickerItem item, SemaphoreSlim limiter, CancellationToken cancellationToken)
        {
            await limiter.WaitAsync(cancellationToken);
            try
            {
                item.IsMinimized = WindowThumbnailProvider.IsMinimized(item.Handle);
                var iconTask = WindowThumbnailProvider.LoadIconAsync(item.Window.Process);
                item.Thumbnail = await WindowThumbnailProvider.CaptureThumbnailAsync(item.Handle);
                item.Icon = await iconTask;
                item.IsLoadingThumbnail = false;
            }
            finally
            {
                limiter.Release();
            }
        }

        private void RefreshState()
        {
            var visibleCount = _view?.Cast<object>().Count() ?? 0;
            var selectedCount = _items.Count(item => item.IsSelected);

            EmptyState.Visibility = visibleCount == 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyState.Text = _items.Count == 0 ? "No windows found." : "No window matches your search.";
            SelectionSummary.Text = $"{selectedCount} of {_items.Count} selected";
            ApplyButton.IsEnabled = _items.Any(item => item.HasChanged);
        }

        private void SetVisibleSelection(bool isSelected)
        {
            foreach (var item in _view.Cast<WindowPickerItem>())
                item.IsSelected = isSelected;
            RefreshState();
        }

        private void Card_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is not WindowPickerItem item) return;
            item.IsSelected = !item.IsSelected;
            RefreshState();
        }

        private void SearchBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            _view?.Refresh();
            RefreshState();
        }

        private void SelectAll_OnClick(object sender, RoutedEventArgs e) => SetVisibleSelection(true);

        private void Clear_OnClick(object sender, RoutedEventArgs e) => SetVisibleSelection(false);

        private void Cancel_OnClick(object sender, RoutedEventArgs e) => Close();

        private void Apply_OnClick(object sender, RoutedEventArgs e)
        {
            ApplySelection();
            Close();
        }

        private void ApplySelection()
        {
            var changedItems = _items.Where(item => item.HasChanged).ToList();
            if (changedItems.Count == 0) return;

            foreach (var item in changedItems)
                ApplyItem(item);

            _windowCaptureService.StopCapture();
            _capturableWindowsMonitoringService.SyncWithForegroundWindow();
        }

        private void ApplyItem(WindowPickerItem item)
        {
            if (item.IsSelected)
                _capturableWindowsMonitoringService.AddMonitoringWindow(item.Window);
            else
                _capturableWindowsMonitoringService.RemoveMonitoringWindow(item.Handle);
        }

        private void Window_OnKeyDown(object sender, KeyEventArgs e)
        {
            var controlPressed = Keyboard.Modifiers == ModifierKeys.Control;
            if (controlPressed && e.Key == Key.Enter && ApplyButton.IsEnabled)
            {
                Apply_OnClick(sender, e);
                e.Handled = true;
            }
            else if (controlPressed && e.Key == Key.F)
            {
                SearchBox.Focus();
                e.Handled = true;
            }
        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        // The window is dark; ask DWM for a dark title bar (DWMWA_USE_IMMERSIVE_DARK_MODE = 20).
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var useDarkMode = 1;
            DwmSetWindowAttribute(new System.Windows.Interop.WindowInteropHelper(this).Handle, 20, ref useDarkMode, sizeof(int));
        }

        protected override void OnClosed(EventArgs e)
        {
            _closing.Cancel();
            _closing.Dispose();
            base.OnClosed(e);
        }
    }
}
