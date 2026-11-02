using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodescoveryCaptureManager.Domain.Interfaces;
using CodescoveryCaptureManager.Domain.Profiles;

namespace CodescoveryCaptureManager.Domain.Services
{
    /// <summary>JSON files in one directory: profiles.json and settings.json. Writes are atomic (temp file + move).</summary>
    public sealed class JsonProfileStore : IProfileStore
    {
        private const string ProfilesFileName = "profiles.json";
        private const string SettingsFileName = "settings.json";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
        };

        private readonly string _directory;

        public JsonProfileStore(string directory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(directory);
            _directory = directory;
        }

        /// <summary>Override for tests and portable use: environment variable CAPTURE_MANAGER_DATA_DIR.</summary>
        public const string DataDirectoryVariable = "CAPTURE_MANAGER_DATA_DIR";

        public static string DefaultDirectory
            => Environment.GetEnvironmentVariable(DataDirectoryVariable) is { Length: > 0 } overridden
                ? overridden
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CodescoveryCaptureManager");

        public IReadOnlyList<CaptureProfile> LoadProfiles()
            => Load(ProfilesFileName, new List<CaptureProfile>());

        public void SaveProfiles(IReadOnlyList<CaptureProfile> profiles)
            => Save(ProfilesFileName, profiles);

        public AppSettings LoadSettings()
            => Load(SettingsFileName, new AppSettings());

        public void SaveSettings(AppSettings settings)
            => Save(SettingsFileName, settings);

        private T Load<T>(string fileName, T fallback) where T : class
        {
            var path = Path.Combine(_directory, fileName);
            if (!File.Exists(path)) return fallback;

            try
            {
                return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions) ?? fallback;
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"Could not read {path}. {ex.Message}");
                KeepBackup(path);
                return fallback;
            }
        }

        private void Save(string fileName, object content)
        {
            try
            {
                Directory.CreateDirectory(_directory);
                var path = Path.Combine(_directory, fileName);
                var temporaryPath = path + ".tmp";
                File.WriteAllText(temporaryPath, JsonSerializer.Serialize(content, content.GetType(), JsonOptions));
                File.Move(temporaryPath, path, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"Could not save {fileName}. {ex.Message}");
            }
        }

        private static void KeepBackup(string path)
        {
            try
            {
                File.Copy(path, path + ".bak", overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"Could not back up {path}. {ex.Message}");
            }
        }
    }
}
