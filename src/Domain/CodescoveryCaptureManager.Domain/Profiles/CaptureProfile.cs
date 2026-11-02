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

    public sealed record AppSettings(Guid? LastProfileId = null, string FallbackMode = "Message");
}
