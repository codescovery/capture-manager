using System.Collections.Generic;
using CodescoveryCaptureManager.Domain.Profiles;

namespace CodescoveryCaptureManager.Domain.Interfaces
{
    /// <summary>Persistence for profiles and small app settings. Never throws on a missing or corrupt file.</summary>
    public interface IProfileStore
    {
        IReadOnlyList<CaptureProfile> LoadProfiles();
        void SaveProfiles(IReadOnlyList<CaptureProfile> profiles);
        AppSettings LoadSettings();
        void SaveSettings(AppSettings settings);
    }
}
