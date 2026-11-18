using System;
using System.Collections.Generic;
using CodescoveryCaptureManager.Domain.Profiles;

namespace CodescoveryCaptureManager.Domain.Interfaces
{
    public interface IProfileService
    {
        IReadOnlyList<CaptureProfile> Profiles { get; }
        /// <summary>Profile whose rules the monitored set currently follows; null when the user picked windows by hand.</summary>
        CaptureProfile Active { get; }
        AppSettings Settings { get; }
        event Action Changed;

        /// <summary>Adds or replaces (same Id) a profile and persists it.</summary>
        CaptureProfile Save(CaptureProfile profile, bool makeActive);
        void Delete(Guid profileId);
        /// <summary>Replaces the monitored set with the windows the profile's rules match right now.</summary>
        ProfileApplyResult Apply(Guid profileId);
        /// <summary>The user changed the monitored set by hand: no profile is active any more.</summary>
        void ClearActive();
        void UpdateSettings(Func<AppSettings, AppSettings> change);
    }
}
