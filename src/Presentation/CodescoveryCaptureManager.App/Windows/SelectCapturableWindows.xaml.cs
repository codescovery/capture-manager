using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using CodescoveryCaptureManager.Domain.Entities;
using CodescoveryCaptureManager.Domain.Interfaces;
using CodescoveryCaptureManager.Domain.Services;
using CodescoveryCaptureManager.Domain.Structs;
using MaterialDesignThemes.Wpf;

namespace CodescoveryCaptureManager.App.Windows
{
    /// <summary>
    /// Interaction logic for SelectCapturableWindows.xaml
    /// </summary>
    public partial class SelectCapturableWindows : Window
    {
        private readonly IWindowsProcessServices _windowsProcessServices;
        private readonly ICapturableWindowsMonitoringService _capturableWindowsMonitoringService;
        private readonly WindowCaptureService _windowCaptureService;
        private ObservableCollection<CapturableWindow> _capturedWindows;

        public SelectCapturableWindows(IWindowsProcessServices windowsProcessServices,
            ICapturableWindowsMonitoringService capturableWindowsMonitoringService,
            WindowCaptureService windowCaptureService, ObservableCollection<CapturableWindow> capturedWindows = null)
        {
            _windowsProcessServices = windowsProcessServices;
            _capturableWindowsMonitoringService = capturableWindowsMonitoringService;
            _windowCaptureService = windowCaptureService;
            _capturedWindows = capturedWindows ?? new ObservableCollection<CapturableWindow>();
            InitializeComponent();
        }

        public  void Pick(Window ignoredWindow = null!)
        {
            CreateOpenedWindowsMenuItems(_windowsProcessServices.GetOpenedWindows(ignoredWindow));
            Show();
        }

        private void CreateOpenedWindowsMenuItems(IEnumerable<CapturableWindow> openedWindows)
        {

            const int maxItemsPerRow = 3;
            const int cardSize = 192;
            const int cardSizeHovered = 202;
            
            var dataGridRowNumber = 0;
            var dataGridRowColumnNumber = 0;
            CreateNewRowDefinition();
            foreach (var openedWindow in openedWindows)
            {
                if (dataGridRowColumnNumber == maxItemsPerRow)
                    dataGridRowColumnNumber = AddNewRowToGrid(ref dataGridRowNumber);

                var openedWindowCardStackPanel = new StackPanel();

                var openedWindowCard = new Card
                {

                    Width = cardSize,
                    Height = cardSize,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(10),
                    Cursor = Cursors.Hand

                };
                openedWindowCard.MouseEnter += (sender, args) =>
                {
                    openedWindowCard.Width = cardSizeHovered;
                    openedWindowCard.Height = cardSizeHovered;
                };
                openedWindowCard.MouseLeave += (sender, args) =>
                {
                    openedWindowCard.Width = cardSize;
                    openedWindowCard.Height = cardSize;
                };


                openedWindowCardStackPanel.Children.Add(new TextBlock
                {
                    Text = openedWindow.Name,
                    //TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    //Foreground = new SolidColorBrush(Colors.White),
                    
                });
                openedWindowCard.Content = openedWindowCardStackPanel;
                openedWindowCard.MouseLeftButtonDown += (sender, args) =>
                {
                    _windowCaptureService.StopCapture();
                    if (_capturableWindowsMonitoringService.IsMonitored(openedWindow.Handle))
                        _capturableWindowsMonitoringService.RemoveMonitoringWindow(openedWindow.Handle);
                    else
                        _capturableWindowsMonitoringService.AddMonitoringWindow(openedWindow);
                    _capturableWindowsMonitoringService.FocusFirstMonitoringWindow();
                };
                Grid.SetRow(openedWindowCard, dataGridRowNumber);
                Grid.SetColumn(openedWindowCard, dataGridRowColumnNumber);
                AvailableCapturableWindows.Children.Add(openedWindowCard);
                _capturedWindows.Add(openedWindow);
                dataGridRowColumnNumber++;
            }
        }

        private int AddNewRowToGrid(ref int dataGridRowNumber)
        {
            int dataGridRowColumnNumber;
            dataGridRowColumnNumber = 0;
            dataGridRowNumber++;
            CreateNewRowDefinition();
            return dataGridRowColumnNumber;
        }

        private  void CreateNewRowDefinition()
        {
            AvailableCapturableWindows.RowDefinitions.Add(new RowDefinition { Height = new GridLength(256) });
        }

        private void CancellButton_MouseLeftButtonDown(object sender, RoutedEventArgs routedEventArgs)
        {
            Close();
        }

        private void Apply_MouseLeftButtonDown(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }
    }
}
