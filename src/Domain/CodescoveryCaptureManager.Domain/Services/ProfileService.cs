using System;
using System.Collections.Generic;
using System.Linq;
using CodescoveryCaptureManager.Domain.Interfaces;
using CodescoveryCaptureManager.Domain.Profiles;
using CodescoveryCaptureManager.Domain.Structs;

namespace CodescoveryCaptureManager.Domain.Services
{
    internal sealed class ProfileService : IProfileService
    {
        private readonly IProfileStore _store;
        private readonly IWindowsProcessServices _windowsProcessServices;
        private readonly ICapturableWindowsMonitoringService _monitoringService;
        private readonly List<CaptureProfile> _profiles;
        private AppSettings _settings;

        public ProfileService(IProfileStore store, IWindowsProcessServices windowsProcessServices,
            ICapturableWindowsMonitoringService monitoringService)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _windowsProcessServices = windowsProcessServices ?? throw new ArgumentNullException(nameof(windowsProcessServices));
            _monitoringService = monitoringService ?? throw new ArgumentNullException(nameof(monitoringService));
            _profiles = _store.LoadProfiles().ToList();
            _settings = _store.LoadSettings();
            Active = _profiles.FirstOrDefault(profile => profile.Id == _settings.LastProfileId);
        }

        public IReadOnlyList<CaptureProfile> Profiles => _profiles.ToList();

        public CaptureProfile Active { get; private set; }

        public AppSettings Settings => _settings;

        public event Action Changed;

        public CaptureProfile Save(CaptureProfile profile, bool makeActive)
        {
            ArgumentNullException.ThrowIfNull(profile);
            var existingIndex = _profiles.FindIndex(existing => existing.Id == profile.Id);
            if (existingIndex >= 0)
                _profiles[existingIndex] = profile;
            else
                _profiles.Add(profile);

            _store.SaveProfiles(_profiles);
            if (makeActive)
                SetActive(profile);
            Changed?.Invoke();
            return profile;
        }

        public void Delete(Guid profileId)
        {
            if (_profiles.RemoveAll(profile => profile.Id == profileId) == 0) return;

            _store.SaveProfiles(_profiles);
            if (Active?.Id == profileId)
                SetActive(null);
            Changed?.Invoke();
        }

        public ProfileApplyResult Apply(Guid profileId)
        {
            var profile = _profiles.FirstOrDefault(candidate => candidate.Id == profileId)
                          ?? throw new KeyNotFoundException($"Profile {profileId} not found.");

            var openedWindows = GetOpenedForeignWindows();
            var boundWindows = WindowRuleMatcher.Resolve(profile.Rules, openedWindows);
            ReplaceMonitoredWindows(boundWindows);
            SetActive(profile);
            Changed?.Invoke();
            return new ProfileApplyResult(boundWindows.Count, WindowRuleMatcher.FindWaitingRules(profile.Rules, openedWindows));
        }

        public void ClearActive()
        {
            if (Active == null) return;

            SetActive(null);
            Changed?.Invoke();
        }

        public void UpdateSettings(Func<AppSettings, AppSettings> change)
        {
            ArgumentNullException.ThrowIfNull(change);
            _settings = change(_settings);
            _store.SaveSettings(_settings);
        }

        // The app's own windows are never profile material.
        private IReadOnlyList<CapturableWindow> GetOpenedForeignWindows()
            => _windowsProcessServices.GetOpenedWindows()
                .Where(window => !IsOwnWindow(window))
                .ToList();

        private static readonly string OwnProcessName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;

        private static bool IsOwnWindow(CapturableWindow window)
            => string.Equals(WindowTitles.ProcessNameOf(window), OwnProcessName, StringComparison.OrdinalIgnoreCase);

        private void ReplaceMonitoredWindows(IReadOnlyList<CapturableWindow> desiredWindows)
        {
            var desiredHandles = desiredWindows.Select(window => window.Handle).ToHashSet();
            foreach (var staleWindow in _monitoringService.GetMonitoredWindows().Where(window => !desiredHandles.Contains(window.Handle)))
                _monitoringService.RemoveMonitoringWindow(staleWindow.Handle);

            foreach (var window in desiredWindows)
                _monitoringService.AddMonitoringWindow(window);
        }

        private void SetActive(CaptureProfile profile)
        {
            Active = profile;
            UpdateSettings(settings => settings with { LastProfileId = profile?.Id });
        }
    }
}
