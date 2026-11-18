using System;
using System.Collections.Generic;
using System.Linq;
using CodescoveryCaptureManager.Domain.Structs;

namespace CodescoveryCaptureManager.Domain.Profiles
{
    public static class WindowRuleFactory
    {
        // Dirty-document markers editors prepend to the title; they change while typing.
        private static readonly char[] VolatileTitlePrefix = { '*', '●', ' ' };

        /// <summary>Default rule for a picked window: same process, title contains its current title.</summary>
        public static WindowRule FromWindow(CapturableWindow window)
        {
            var title = WindowTitles.TitleOf(window).TrimStart(VolatileTitlePrefix);
            return string.IsNullOrWhiteSpace(title)
                ? new WindowRule(WindowTitles.ProcessNameOf(window), TitleMatchMode.Any, string.Empty, false)
                : new WindowRule(WindowTitles.ProcessNameOf(window), TitleMatchMode.Contains, title, true);
        }

        public static IReadOnlyList<WindowRule> FromWindows(IEnumerable<CapturableWindow> windows)
            => windows.Select(FromWindow).Distinct().ToList();
    }
}
