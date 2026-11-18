using System;
using System.Collections.Generic;
using System.Linq;
using CodescoveryCaptureManager.Domain.Structs;

namespace CodescoveryCaptureManager.Domain.Profiles
{
    public enum BindingChange
    {
        None,
        /// <summary>The window matches the profile now and may be shown.</summary>
        Attach,
        /// <summary>The window was bound but no rule matches it any more (its title changed): it must stop being shown.</summary>
        Detach,
    }

    /// <summary>Keeps the monitored set following a profile's rules while windows open, restart and change title.</summary>
    public static class ProfileBinding
    {
        public static BindingChange Decide(IReadOnlyList<WindowRule> rules, CapturableWindow window, IReadOnlyList<CapturableWindow> boundWindows)
        {
            ArgumentNullException.ThrowIfNull(rules);
            ArgumentNullException.ThrowIfNull(boundWindows);

            var isBound = boundWindows.Any(bound => bound.Handle == window.Handle);
            var matchingRules = rules.Where(rule => WindowRuleMatcher.IsMatch(rule, window)).ToList();
            if (isBound)
                return matchingRules.Count == 0 ? BindingChange.Detach : BindingChange.None;

            return matchingRules.Any(rule => MayAttach(rule, boundWindows)) ? BindingChange.Attach : BindingChange.None;
        }

        // Auto-attach rules take every matching window. Other rules only take a replacement for the window they lost
        // (app restarted, or it was not open when the profile was applied): never a second window nobody picked.
        private static bool MayAttach(WindowRule rule, IReadOnlyList<CapturableWindow> boundWindows)
            => rule.AutoAttach || !boundWindows.Any(bound => WindowRuleMatcher.IsMatch(rule, bound));
    }
}
