using System;
using CodescoveryCaptureManager.Domain.Profiles;
using CodescoveryCaptureManager.Domain.Structs;
using Xunit;

namespace CodescoveryCaptureManager.Domain.Tests.Profiles
{
    public sealed class WindowRuleMatcherTests
    {
        private static CapturableWindow Window(string processName, string title)
            => new() { Handle = new IntPtr(1), ProcessName = processName, Name = $"{title} ({processName}.exe)" };

        [Fact]
        public void IsMatch_ContainsRule_MatchesTitleFragmentIgnoringCase()
        {
            var rule = new WindowRule("devenv", TitleMatchMode.Contains, "myapp", true);

            Assert.True(WindowRuleMatcher.IsMatch(rule, Window("devenv", "MyApp - Microsoft Visual Studio")));
        }

        [Fact]
        public void IsMatch_DifferentProcess_DoesNotMatch()
        {
            var rule = new WindowRule("devenv", TitleMatchMode.Any, string.Empty, false);

            Assert.False(WindowRuleMatcher.IsMatch(rule, Window("notepad", "MyApp")));
        }

        [Fact]
        public void IsMatch_ProcessNameIgnoresCase()
        {
            var rule = new WindowRule("Notepad", TitleMatchMode.Any, string.Empty, false);

            Assert.True(WindowRuleMatcher.IsMatch(rule, Window("notepad", "a.txt")));
        }

        [Fact]
        public void IsMatch_ExactRule_RequiresWholeTitle()
        {
            var rule = new WindowRule("notepad", TitleMatchMode.Exact, "a.txt - Notepad", true);

            Assert.True(WindowRuleMatcher.IsMatch(rule, Window("notepad", "a.txt - Notepad")));
            Assert.False(WindowRuleMatcher.IsMatch(rule, Window("notepad", "a.txt - Notepad 2")));
        }

        [Fact]
        public void IsMatch_RegexRule_MatchesPattern()
        {
            var rule = new WindowRule("chrome", TitleMatchMode.Regex, @"^Inbox \(\d+\)", true);

            Assert.True(WindowRuleMatcher.IsMatch(rule, Window("chrome", "Inbox (12) - Mail")));
            Assert.False(WindowRuleMatcher.IsMatch(rule, Window("chrome", "Sent - Mail")));
        }

        [Fact]
        public void IsMatch_InvalidRegex_DoesNotThrowAndDoesNotMatch()
        {
            var rule = new WindowRule("chrome", TitleMatchMode.Regex, "([unclosed", true);

            Assert.False(WindowRuleMatcher.IsMatch(rule, Window("chrome", "anything")));
        }

        [Fact]
        public void Resolve_ReturnsOnlyWindowsMatchedByAnyRule()
        {
            var rules = new[]
            {
                new WindowRule("devenv", TitleMatchMode.Contains, "MyApp", true),
                new WindowRule("notepad", TitleMatchMode.Any, string.Empty, false),
            };
            var windows = new[]
            {
                Window("devenv", "MyApp - Visual Studio"),
                Window("devenv", "Other - Visual Studio"),
                Window("notepad", "x.txt"),
                Window("chrome", "Mail"),
            };

            var resolved = WindowRuleMatcher.Resolve(rules, windows);

            Assert.Equal(2, resolved.Count);
        }

        [Fact]
        public void FindWaitingRules_ReturnsRulesWithoutAnyWindow()
        {
            var matched = new WindowRule("notepad", TitleMatchMode.Any, string.Empty, false);
            var waiting = new WindowRule("devenv", TitleMatchMode.Contains, "MyApp", true);

            var result = WindowRuleMatcher.FindWaitingRules(new[] { matched, waiting }, new[] { Window("notepad", "x.txt") });

            Assert.Equal(new[] { waiting }, result);
        }
    }
}
