using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using CodescoveryCaptureManager.Domain.Structs;

namespace CodescoveryCaptureManager.App.Windows
{
    /// <summary>One selectable card in the picker.</summary>
    internal sealed class WindowPickerItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        private BitmapSource _thumbnail;
        private BitmapSource _icon;
        private bool _isLoadingThumbnail = true;
        private bool _isMinimized;

        public WindowPickerItem(CapturableWindow window, bool wasMonitored)
        {
            Window = window;
            WasMonitored = wasMonitored;
            _isSelected = wasMonitored;
            ProcessName = window.Process?.ProcessName ?? string.Empty;
            Title = ExtractTitle(window);
        }

        public CapturableWindow Window { get; }
        public IntPtr Handle => Window.Handle;
        public bool WasMonitored { get; }
        public string Title { get; }
        public string ProcessName { get; }
        public bool HasChanged => IsSelected != WasMonitored;

        public bool IsSelected
        {
            get => _isSelected;
            set => Set(ref _isSelected, value);
        }

        public BitmapSource Thumbnail
        {
            get => _thumbnail;
            set => Set(ref _thumbnail, value);
        }

        public BitmapSource Icon
        {
            get => _icon;
            set => Set(ref _icon, value);
        }

        public bool IsMinimized
        {
            get => _isMinimized;
            set => Set(ref _isMinimized, value);
        }

        public bool IsLoadingThumbnail
        {
            get => _isLoadingThumbnail;
            set => Set(ref _isLoadingThumbnail, value);
        }

        public bool Matches(string filter)
            => string.IsNullOrWhiteSpace(filter)
               || Title.Contains(filter, StringComparison.OrdinalIgnoreCase)
               || ProcessName.Contains(filter, StringComparison.OrdinalIgnoreCase);

        // WindowsProcessServices names windows "title (process.exe)"; show the title alone.
        private static string ExtractTitle(CapturableWindow window)
        {
            var suffix = $" ({window.Process?.ProcessName}.exe)";
            return window.Name.EndsWith(suffix, StringComparison.Ordinal)
                ? window.Name[..^suffix.Length]
                : window.Name;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Set<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value)) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
