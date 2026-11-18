using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CodescoveryCaptureManager.Domain.Structs;

namespace CodescoveryCaptureManager.Domain.Profiles
{
    public static class WindowRuleMatcher
    {
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(200);

        public static bool IsMatch(WindowRule rule, CapturableWindow window)
        {
            ArgumentNullException.ThrowIfNull(rule);
            if (!string.Equals(rule.ProcessName, WindowTitles.ProcessNameOf(window), StringComparison.OrdinalIgnoreCase))
                return false;

            return IsTitleMatch(rule, WindowTitles.TitleOf(window));
        }

        public static IReadOnlyList<CapturableWindow> Resolve(IReadOnlyList<WindowRule> rules, IEnumerable<CapturableWindow> windows)
            => windows.Where(window => rules.Any(rule => IsMatch(rule, window))).ToList();

        public static IReadOnlyList<WindowRule> FindWaitingRules(IReadOnlyList<WindowRule> rules, IReadOnlyList<CapturableWindow> windows)
            => rules.Where(rule => !windows.Any(window => IsMatch(rule, window))).ToList();

        private static bool IsTitleMatch(WindowRule rule, string title)
            => rule.TitleMatch switch
            {
                TitleMatchMode.Any => true,
                TitleMatchMode.Contains => title.Contains(rule.TitlePattern ?? string.Empty, StringComparison.OrdinalIgnoreCase),
                TitleMatchMode.Exact => string.Equals(title, rule.TitlePattern, StringComparison.OrdinalIgnoreCase),
                TitleMatchMode.Regex => IsRegexMatch(rule.TitlePattern, title),
                _ => false,
            };

        private static bool IsRegexMatch(string pattern, string title)
        {
            try
            {
                return Regex.IsMatch(title, pattern ?? string.Empty, RegexOptions.IgnoreCase, RegexTimeout);
            }
            catch (Exception ex) when (ex is ArgumentException or RegexMatchTimeoutException)
            {
                return false;
            }
        }
    }
}
