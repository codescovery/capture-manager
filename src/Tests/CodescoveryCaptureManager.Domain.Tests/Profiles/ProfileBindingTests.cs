using System;
using CodescoveryCaptureManager.Domain.Profiles;
using CodescoveryCaptureManager.Domain.Structs;
using Xunit;

namespace CodescoveryCaptureManager.Domain.Tests.Profiles
{
    public sealed class ProfileBindingTests
    {
        private static CapturableWindow Window(int handle, string processName, string title)
            => new() { Handle = new IntPtr(handle), ProcessName = processName, Name = $"{title} ({processName}.exe)" };

        private static WindowRule Rule(string processName, string titlePart, bool autoAttach)
            => new(processName, TitleMatchMode.Contains, titlePart, autoAttach);

        [Fact]
        public void Decide_UnboundWindowMatchingAutoAttachRule_Attaches()
        {
            var rules = new[] { Rule("devenv", "MyApp", autoAttach: true) };
            var bound = new[] { Window(1, "devenv", "MyApp - Visual Studio") };

            var change = ProfileBinding.Decide(rules, Window(2, "devenv", "MyApp (2) - Visual Studio"), bound);

            Assert.Equal(BindingChange.Attach, change);
        }

        [Fact]
        public void Decide_RuleWithoutAutoAttach_ThatLostItsWindow_AttachesTheReplacement()
        {
            var rules = new[] { Rule("notepad", "notes", autoAttach: false) };

            var change = ProfileBinding.Decide(rules, Window(2, "notepad", "notes.txt"), Array.Empty<CapturableWindow>());

            Assert.Equal(BindingChange.Attach, change);
        }

        [Fact]
        public void Decide_RuleWithoutAutoAttach_ThatStillHasItsWindow_IgnoresASecondOne()
        {
            var rules = new[] { Rule("notepad", "notes", autoAttach: false) };
            var bound = new[] { Window(1, "notepad", "notes.txt") };

            var change = ProfileBinding.Decide(rules, Window(2, "notepad", "more notes.txt"), bound);

            Assert.Equal(BindingChange.None, change);
        }

        [Fact]
        public void Decide_UnboundWindowMatchingNoRule_DoesNothing()
        {
            var rules = new[] { Rule("notepad", "notes", autoAttach: true) };

            var change = ProfileBinding.Decide(rules, Window(2, "chrome", "notes"), Array.Empty<CapturableWindow>());

            Assert.Equal(BindingChange.None, change);
        }

        [Fact]
        public void Decide_BoundWindowWhoseTitleStoppedMatching_Detaches()
        {
            var rules = new[] { Rule("chrome", "Docs", autoAttach: true) };
            var bound = new[] { Window(3, "chrome", "Docs - Chrome") };

            var change = ProfileBinding.Decide(rules, Window(3, "chrome", "Bank - Chrome"), bound);

            Assert.Equal(BindingChange.Detach, change);
        }

        [Fact]
        public void Decide_BoundWindowStillMatching_DoesNothing()
        {
            var rules = new[] { Rule("chrome", "Docs", autoAttach: false) };
            var bound = new[] { Window(3, "chrome", "Docs - Chrome") };

            var change = ProfileBinding.Decide(rules, Window(3, "chrome", "Team Docs - Chrome"), bound);

            Assert.Equal(BindingChange.None, change);
        }
    }
}
