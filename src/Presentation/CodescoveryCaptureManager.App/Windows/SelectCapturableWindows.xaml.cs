using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CodescoveryCaptureManager.Domain.Interfaces;
using CodescoveryCaptureManager.Domain.Services;
using CodescoveryCaptureManager.Domain.Structs;
using MaterialDesignThemes.Wpf;
using Image = System.Windows.Controls.Image;
using CheckBox = System.Windows.Controls.CheckBox;
using Grid = System.Windows.Controls.Grid;
using RowDefinition = System.Windows.Controls.RowDefinition;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using System.Drawing.Imaging;
using System.Drawing;
using System;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using System.Windows.Controls;
using Windows.Media.AppRecording;
using Windows.Storage;
using CodescoveryCaptureManager.Domain.Helpers;
using System.Windows.Interop;
using CodescoveryCaptureManager.Domain.Interop;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Point = System.Drawing.Point;
using Size = System.Windows.Size;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Runtime.InteropServices;
using SharpDX;
using Color = Windows.UI.Color;
using Device = SharpDX.Direct3D11.Device;
using SharpDX.Win32;
using System.IO;
using System.Linq;
using System.Windows.Media.Media3D;
using Windows.System;
using SharpDX.Multimedia;
using Windows.UI.Xaml.Media;
using Stretch = System.Windows.Media.Stretch;

//using TextBlock = Windows.UI.Xaml.Controls.TextBlock;

namespace CodescoveryCaptureManager.App.Windows
{
    /// <summary>
    /// Interaction logic for SelectCapturableWindows.xaml
    /// </summary>
    public partial class SelectCapturableWindows : Window
    {
        public const int InitialColumnItemNumber = 0;
        public const int MaxItemsPerRow = 3;
        public const int CardSize = 192;
        public const int CardSizeHovered = 202;
        public const int StackPanelTitleMaxLength = 25;
        private readonly IWindowsProcessServices _windowsProcessServices;
        private readonly ICapturableWindowsMonitoringService _capturableWindowsMonitoringService;
        private readonly WindowCaptureService _windowCaptureService;
        private ObservableCollection<CapturableWindow> _capturedWindows;

        public SelectCapturableWindows(IWindowsProcessServices windowsProcessServices,
            ICapturableWindowsMonitoringService capturableWindowsMonitoringService,
            WindowCaptureService windowCaptureService
            , ObservableCollection<CapturableWindow> capturedWindows = null
            )
        {
            _windowsProcessServices = windowsProcessServices;
            _capturableWindowsMonitoringService = capturableWindowsMonitoringService;
            _windowCaptureService = windowCaptureService;
            _capturedWindows = capturedWindows ?? new ObservableCollection<CapturableWindow>();
            InitializeComponent();
        }

        public void Pick(params Window[] ignoredWindows)
        {
            var ignoredWindowsWithMe = ignoredWindows?.Append(this)?.ToArray() ?? new Window[]{this};
             
            CreateOpenedWindowsMenuItems(_windowsProcessServices.GetOpenedWindows(ignoredWindowsWithMe));
            Show();
        }

        private void CreateOpenedWindowsMenuItems(IEnumerable<CapturableWindow> openedWindows)
        {
            var dataGridRowNumber = 0;
            var dataGridRowColumnNumber = 0;
            CreateNewRowDefinition();
            foreach (var openedWindow in openedWindows)
            {
                if (dataGridRowColumnNumber == MaxItemsPerRow)
                    dataGridRowColumnNumber = AddNewRowToGrid(ref dataGridRowNumber);


                var isMonitored = _capturableWindowsMonitoringService.IsMonitored(openedWindow.Handle);
                var openedWindowCard = CreateCard(CardSize);
                var selectItemCheckbox = new CheckBox { IsEnabled = false, Margin = new Thickness(5, 0, 0, 0), IsChecked = isMonitored };
                var openedWindowCardStackPanel = CreateStackPanel(openedWindow, selectItemCheckbox);
                openedWindowCard.Content = openedWindowCardStackPanel;
                openedWindowCard.MouseLeftButtonDown += (sender, args) =>
                {
                    isMonitored = _capturableWindowsMonitoringService.IsMonitored(openedWindow.Handle);
                    if (_capturedWindows.Contains(openedWindow))
                        _capturedWindows.Remove(openedWindow);
                    else
                        _capturedWindows.Add(openedWindow);
                    selectItemCheckbox.IsChecked = !selectItemCheckbox.IsChecked;
                };
                Grid.SetRow(openedWindowCard, dataGridRowNumber);
                Grid.SetColumn(openedWindowCard, dataGridRowColumnNumber);
                AvailableCapturableWindows.Children.Add(openedWindowCard);

                dataGridRowColumnNumber++;
            }
        }

        private StackPanel CreateStackPanel(CapturableWindow window, CheckBox selectItemCheckbox)
        {
            var stackPanel = new StackPanel();
            var textStackPanel = new StackPanel { Orientation = Orientation.Horizontal };
            var stackPanelTitleMaxLength = WindowNameTitleMaxLengthExceedsLimits(window)
                ? StackPanelTitleMaxLength
                : window.Name.Length;
            textStackPanel.Children.Add(CreateStackPanelTitle(window, stackPanelTitleMaxLength));
            textStackPanel.Children.Add(selectItemCheckbox);
            stackPanel.Children.Add(textStackPanel);
            var previewImage = CreatePreviewImage(window.Handle);
            if (previewImage != null) stackPanel.Children.Add(previewImage);


            return stackPanel;
        }

        private static TextBlock CreateStackPanelTitle(CapturableWindow window, int stackPanelTitleMaxLength)
        {
            return new TextBlock
            {
                Text = $"{window.Name.Substring(0, stackPanelTitleMaxLength)}...",
                TextWrapping = TextWrapping.NoWrap,
                TextAlignment = TextAlignment.Center,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(Colors.White),

            };
        }

        private static bool WindowNameTitleMaxLengthExceedsLimits(CapturableWindow window)
        {
            return window.Name.Length > StackPanelTitleMaxLength;
        }

        private UIElement CreatePreviewImage(IntPtr hWnd)
        {
            var grid = new Grid();
            
            grid.RowDefinitions.Add(new RowDefinition{Height = GridLength.Auto});
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            NativeMethods.GetWindowRect(hWnd, out var rect);
            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;
            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bmp))
                graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, new System.Drawing.Size(width, height), CopyPixelOperation.SourceCopy);
            var memoryStream = new MemoryStream();
            bmp.Save(memoryStream, ImageFormat.Png);
            var image = new Image
            {
                Margin = new Thickness(10),
                Source = BitmapFrame.Create(memoryStream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad),
                Width = 192,
                Height = 108,
                Stretch = Stretch.UniformToFill
            };
            
            Grid.SetRow(image,1);
            grid.Children.Add(image);
            return grid;
        }
        private static Card CreateCard(int cardSize)
        {
            var card = new Card
            {

                Width = cardSize,
                Height = cardSize,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10),
                Cursor = Cursors.Hand

            };
            card.MouseEnter += (sender, args) =>
            {
                card.Width = CardSizeHovered;
                card.Height = CardSizeHovered;
            };
            card.MouseLeave += (sender, args) =>
            {
                card.Width = cardSize;
                card.Height = cardSize;
            };
            return card;
        }

        private int AddNewRowToGrid(ref int dataGridRowNumber)
        {
            dataGridRowNumber++;
            CreateNewRowDefinition();
            return InitialColumnItemNumber;
        }

        private void CreateNewRowDefinition()
        {
            AvailableCapturableWindows.RowDefinitions.Add(new RowDefinition { Height = new GridLength(256) });
        }

        private void CancellButton_MouseLeftButtonDown(object sender, RoutedEventArgs routedEventArgs)
        {
            Close();
        }

        private void Apply_MouseLeftButtonDown(object sender, RoutedEventArgs e)
        {
            foreach (var openedWindow in _capturedWindows)
            {
                _windowCaptureService.StopCapture();
                if (_capturableWindowsMonitoringService.IsMonitored(openedWindow.Handle))
                    _capturableWindowsMonitoringService.RemoveMonitoringWindow(openedWindow.Handle);
                else
                    _capturableWindowsMonitoringService.AddMonitoringWindow(openedWindow);
                _capturableWindowsMonitoringService.FocusFirstMonitoringWindow();
            }
            Close();
        }
    }
}
