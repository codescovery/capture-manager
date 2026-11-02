using System;
using System.Collections.Generic;
using System.Linq;
using CodescoveryCaptureManager.Domain.Interfaces;
using CodescoveryCaptureManager.Domain.Profiles;
using CodescoveryCaptureManager.Domain.Services;
using CodescoveryCaptureManager.Domain.Structs;
using Xunit;

namespace CodescoveryCaptureManager.Domain.Tests.Profiles
{
    public sealed class ProfileServiceTests
    {
        private sealed class InMemoryStore : IProfileStore
        {
            public List<CaptureProfile> Profiles { get; } = new();
            public AppSettings Settings { get; private set; } = new();
            public IReadOnlyList<CaptureProfile> LoadProfiles() => Profiles.ToList();
            public void SaveProfiles(IReadOnlyList<CaptureProfile> profiles) { Profiles.Clear(); Profiles.AddRange(profiles); }
            public AppSettings LoadSettings() => Settings;
            public void SaveSettings(AppSettings settings) => Settings = settings;
        }

        private sealed class FakeWindows : IWindowsProcessServices
        {
            public List<CapturableWindow> Opened { get; } = new();
            public IReadOnlyList<CapturableWindow> GetOpenedWindows(params System.Windows.Window[] ignoredWindows) => Opened.ToList();
            public void Dispose() { }
        }

        private static CapturableWindow Window(int handle, string processName, string title)
            => new() { Handle = new IntPtr(handle), ProcessName = processName, Name = $"{title} ({processName}.exe)" };

        private static (ProfileService Service, FakeWindows Windows, CapturableWindowsMonitoringService Monitoring, InMemoryStore Store) Create()
        {
            var store = new InMemoryStore();
            var windows = new FakeWindows();
            var monitoring = new CapturableWindowsMonitoringService(new Dictionary<IntPtr, CapturableWindow>());
            return (new ProfileService(store, windows, monitoring), windows, monitoring, store);
        }

        [Fact]
        public void Apply_BindsMatchingWindowsAndReportsWaitingRules()
        {
            var (service, windows, monitoring, _) = Create();
            windows.Opened.Add(Window(1, "notepad", "a.txt"));
            windows.Opened.Add(Window(2, "chrome", "Mail"));
            var profile = service.Save(CaptureProfile.Create("p", new[]
            {
                new WindowRule("notepad", TitleMatchMode.Any, string.Empty, false),
                new WindowRule("devenv", TitleMatchMode.Contains, "MyApp", true),
            }), makeActive: false);

            var result = service.Apply(profile.Id);

            Assert.Equal(1, result.BoundWindows);
            Assert.Single(result.WaitingRules);
            Assert.Equal(new[] { new IntPtr(1) }, monitoring.GetMonitoredWindows().Select(w => w.Handle));
        }

        [Fact]
        public void Apply_ReplacesPreviouslyMonitoredWindows()
        {
            var (service, windows, monitoring, _) = Create();
            var old = Window(9, "chrome", "Old");
            windows.Opened.Add(old);
            windows.Opened.Add(Window(1, "notepad", "a.txt"));
            monitoring.AddMonitoringWindow(old);
            var profile = service.Save(CaptureProfile.Create("p", new[] { new WindowRule("notepad", TitleMatchMode.Any, string.Empty, false) }), false);

            service.Apply(profile.Id);

            Assert.False(monitoring.IsMonitored(new IntPtr(9)));
            Assert.True(monitoring.IsMonitored(new IntPtr(1)));
        }

        [Fact]
        public void Apply_MarksProfileActiveAndPersistsLastProfile()
        {
            var (service, _, _, store) = Create();
            var profile = service.Save(CaptureProfile.Create("p", Array.Empty<WindowRule>()), makeActive: false);

            service.Apply(profile.Id);

            Assert.Equal(profile.Id, service.Active.Id);
            Assert.Equal(profile.Id, store.Settings.LastProfileId);
        }

        [Fact]
        public void Apply_UnknownProfile_Throws()
        {
            var (service, _, _, _) = Create();

            Assert.Throws<KeyNotFoundException>(() => service.Apply(Guid.NewGuid()));
        }

        [Fact]
        public void Save_ExistingId_ReplacesProfile()
        {
            var (service, _, _, _) = Create();
            var profile = service.Save(CaptureProfile.Create("old", Array.Empty<WindowRule>()), false);

            service.Save(profile with { Name = "new" }, false);

            Assert.Equal("new", Assert.Single(service.Profiles).Name);
        }

        [Fact]
        public void Delete_ActiveProfile_ClearsActiveAndLastProfile()
        {
            var (service, _, _, store) = Create();
            var profile = service.Save(CaptureProfile.Create("p", Array.Empty<WindowRule>()), makeActive: true);

            service.Delete(profile.Id);

            Assert.Null(service.Active);
            Assert.Empty(service.Profiles);
            Assert.Null(store.Settings.LastProfileId);
        }

        [Fact]
        public void Constructor_RestoresActiveProfileFromSettings()
        {
            var store = new InMemoryStore();
            var profile = CaptureProfile.Create("p", Array.Empty<WindowRule>());
            store.Profiles.Add(profile);
            store.SaveSettings(new AppSettings(profile.Id));

            var service = new ProfileService(store, new FakeWindows(), new CapturableWindowsMonitoringService(new Dictionary<IntPtr, CapturableWindow>()));

            Assert.Equal(profile.Id, service.Active.Id);
        }
    }
}
