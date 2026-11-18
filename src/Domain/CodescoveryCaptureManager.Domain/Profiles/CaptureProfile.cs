using System;
using System.Collections.Generic;

namespace CodescoveryCaptureManager.Domain.Profiles
{
    public sealed record CaptureProfile(Guid Id, string Name, IReadOnlyList<WindowRule> Rules)
    {
        public static CaptureProfile Create(string name, IReadOnlyList<WindowRule> rules)
            => new(Guid.NewGuid(), name, rules);
    }

    /// <summary>Outcome of applying a profile: how many windows were bound and which rules found none yet.</summary>
    public sealed record ProfileApplyResult(int BoundWindows, IReadOnlyList<WindowRule> WaitingRules);

    /// <summary><c>PauseHotkey</c>: text form of a <see cref="Hotkeys.Hotkey"/>; empty = no hotkey.</summary>
    public sealed record AppSettings(Guid? LastProfileId = null, string FallbackMode = "Message", bool ExitOnClose = false, string WindowTitle = null, string PauseHotkey = AppSettings.DefaultPauseHotkey)
    {
        public const string DefaultPauseHotkey = "Ctrl+Alt+P";
    }
}
