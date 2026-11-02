using System;
using System.IO;
using CodescoveryCaptureManager.Domain.Profiles;
using CodescoveryCaptureManager.Domain.Services;
using Xunit;

namespace CodescoveryCaptureManager.Domain.Tests.Profiles
{
    public sealed class JsonProfileStoreTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "ccm-tests-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        }

        [Fact]
        public void LoadProfiles_MissingFile_ReturnsEmpty()
        {
            Assert.Empty(new JsonProfileStore(_directory).LoadProfiles());
        }

        [Fact]
        public void SaveThenLoad_RoundTripsProfilesWithRules()
        {
            var store = new JsonProfileStore(_directory);
            var profile = CaptureProfile.Create("Standup", new[]
            {
                new WindowRule("devenv", TitleMatchMode.Contains, "MyApp", true),
                new WindowRule("notepad", TitleMatchMode.Regex, "^a", false),
            });

            store.SaveProfiles(new[] { profile });
            var loaded = new JsonProfileStore(_directory).LoadProfiles();

            var loadedProfile = Assert.Single(loaded);
            Assert.Equal(profile.Id, loadedProfile.Id);
            Assert.Equal("Standup", loadedProfile.Name);
            Assert.Equal(profile.Rules, loadedProfile.Rules);
        }

        [Fact]
        public void SaveProfiles_WritesEnumsAsStrings()
        {
            var store = new JsonProfileStore(_directory);
            store.SaveProfiles(new[] { CaptureProfile.Create("p", new[] { new WindowRule("a", TitleMatchMode.Exact, "t", true) }) });

            var json = File.ReadAllText(Path.Combine(_directory, "profiles.json"));

            Assert.Contains("\"Exact\"", json);
        }

        [Fact]
        public void LoadProfiles_CorruptFile_ReturnsEmptyAndKeepsBackup()
        {
            Directory.CreateDirectory(_directory);
            var path = Path.Combine(_directory, "profiles.json");
            File.WriteAllText(path, "{ not json");

            var loaded = new JsonProfileStore(_directory).LoadProfiles();

            Assert.Empty(loaded);
            Assert.True(File.Exists(path + ".bak"));
        }

        [Fact]
        public void SettingsRoundTrip_KeepsLastProfileAndFallbackMode()
        {
            var store = new JsonProfileStore(_directory);
            var settings = new AppSettings(Guid.NewGuid(), "KeepLast");

            store.SaveSettings(settings);

            Assert.Equal(settings, new JsonProfileStore(_directory).LoadSettings());
        }

        [Fact]
        public void LoadSettings_MissingFile_ReturnsDefaults()
        {
            var settings = new JsonProfileStore(_directory).LoadSettings();

            Assert.Null(settings.LastProfileId);
            Assert.Equal("Message", settings.FallbackMode);
        }
    }
}
