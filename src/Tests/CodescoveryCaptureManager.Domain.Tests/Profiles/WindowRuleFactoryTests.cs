using System;
using CodescoveryCaptureManager.Domain.Profiles;
using CodescoveryCaptureManager.Domain.Structs;
using Xunit;

namespace CodescoveryCaptureManager.Domain.Tests.Profiles
{
    public sealed class WindowRuleFactoryTests
    {
        private static CapturableWindow Window(string processName, string title)
            => new() { Handle = new IntPtr(1), ProcessName = processName, Name = $"{title} ({processName}.exe)" };

        [Fact]
        public void FromWindow_UsesProcessAndTitleContains()
        {
            var rule = WindowRuleFactory.FromWindow(Window("devenv", "MyApp - Microsoft Visual Studio"));

            Assert.Equal(new WindowRule("devenv", TitleMatchMode.Contains, "MyApp - Microsoft Visual Studio", true), rule);
        }

        [Fact]
        public void FromWindow_StripsDirtyDocumentMarker()
        {
            var rule = WindowRuleFactory.FromWindow(Window("notepad++", "*new 17 - Notepad++"));

            Assert.Equal("new 17 - Notepad++", rule.TitlePattern);
        }

        [Fact]
        public void FromWindow_EmptyTitle_FallsBackToAnyWithoutAutoAttach()
        {
            var rule = WindowRuleFactory.FromWindow(Window("tool", string.Empty));

            Assert.Equal(TitleMatchMode.Any, rule.TitleMatch);
            Assert.False(rule.AutoAttach);
        }

        [Fact]
        public void FromWindows_RemovesDuplicateRules()
        {
            var rules = WindowRuleFactory.FromWindows(new[] { Window("notepad", "a"), Window("notepad", "a") });

            Assert.Single(rules);
        }
    }
}
