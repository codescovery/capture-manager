using System;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CodescoveryCaptureManager.App.Helpers;
using CodescoveryCaptureManager.App.Windows;
using CodescoveryCaptureManager.Domain.Interfaces;
using H.NotifyIcon;

namespace CodescoveryCaptureManager.App.Tray
{
    /// <summary>Notification-area icon: keeps the app (capture + stream) running without its window.</summary>
    internal sealed class TrayIconController : IDisposable
    {
        private const string WebViewerUrl = "http://127.0.0.1:47800/";

        private readonly CapturedWindows _window;
        private readonly IProfileService _profiles;
        private readonly Action _exit;
        private readonly TaskbarIcon _icon;
        private readonly MenuItem _profilesMenu = new MenuItem { Header = "Profiles" };

        public TrayIconController(CapturedWindows window, IProfileService profiles, Action exit)
        {
            _window = window;
            _profiles = profiles;
            _exit = exit;
            _icon = new TaskbarIcon
            {
                Icon = LoadIcon(),
                ToolTipText = AppInfo.Name,
                NoLeftClickDelay = true,
                ContextMenu = BuildMenu(),
            };
            _icon.TrayMouseDoubleClick += (_, _) => _window.ShowFromTray();
            _icon.ForceCreate();
        }

        private static Icon LoadIcon()
        {
            var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/icon.ico"));
            using var stream = resource.Stream;
            return new Icon(stream);
        }

        private ContextMenu BuildMenu()
        {
            var menu = new ContextMenu();
            menu.Items.Add(Item("Open Capture Manager", () => _window.ShowFromTray(), bold: true));
            menu.Items.Add(Item("Open web viewer", () => AppInfo.OpenInBrowser(WebViewerUrl)));
            menu.Items.Add(new Separator());
            menu.Items.Add(_profilesMenu);
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("About", () => _window.ShowAbout()));
            menu.Items.Add(Item("Exit", _exit));
            menu.Opened += (_, _) => RebuildProfiles();
            return menu;
        }

        private void RebuildProfiles()
        {
            _profilesMenu.Items.Clear();
            var profiles = _profiles.Profiles;
            _profilesMenu.IsEnabled = profiles.Count > 0;
            foreach (var profile in profiles)
            {
                var item = Item(profile.Name, () => _window.ApplyProfile(profile.Id));
                item.IsCheckable = true;
                item.IsChecked = _profiles.Active?.Id == profile.Id;
                _profilesMenu.Items.Add(item);
            }
        }

        private static MenuItem Item(string header, Action onClick, bool bold = false)
        {
            var item = new MenuItem { Header = header, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal };
            item.Click += (_, _) => onClick();
            return item;
        }

        public void Dispose() => _icon.Dispose();
    }
}
