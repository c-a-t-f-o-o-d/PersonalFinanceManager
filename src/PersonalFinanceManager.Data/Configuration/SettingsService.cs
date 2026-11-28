using PersonalFinanceManager.Core.Application;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace PersonalFinanceManager.Data.Configuration
{
    public sealed class SettingsService
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        public SettingsService()
            : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ApplicationIdentity.LocalApplicationDataFolderName))
        {
        }

        public SettingsService(string configurationDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(configurationDirectory);
            ConfigurationDirectory = Path.GetFullPath(configurationDirectory);
        }

        public string ConfigurationDirectory { get; }
        public string UserSettingsPath => Path.Combine(ConfigurationDirectory, "appsettings.Local.json");

        public AppSettings Load()
        {
            AppSettings settings = LoadDefaults();

            if (File.Exists(UserSettingsPath))
            {
                string json = File.ReadAllText(UserSettingsPath);
                AppSettings? userSettings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);

                if (userSettings is not null)
                {
                    settings = userSettings;
                }
            }

            Normalize(settings);
            return settings;
        }

        public void Save(AppSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            Normalize(settings);
            Directory.CreateDirectory(ConfigurationDirectory);
            string tempPath = UserSettingsPath + ".tmp";
            AppSettings persistedSettings = CreatePersistedSettings(settings);
            string json = JsonSerializer.Serialize(persistedSettings, _jsonOptions);
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, UserSettingsPath, true);
        }

        public void SaveAppearancePreference(bool useDarkMode)
        {
            if (!File.Exists(UserSettingsPath))
            {
                AppSettings settings = LoadDefaults();
                settings.UseDarkMode = useDarkMode;
                Save(settings);
                return;
            }

            JsonObject document = JsonNode.Parse(File.ReadAllText(UserSettingsPath)) as JsonObject
                ?? throw new InvalidDataException("The settings file must contain a JSON object.");
            document["useDarkMode"] = useDarkMode;
            string tempPath = UserSettingsPath + ".tmp";
            File.WriteAllText(tempPath, document.ToJsonString(_jsonOptions));
            File.Move(tempPath, UserSettingsPath, true);
        }

        private static AppSettings LoadDefaults()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

            if (!File.Exists(path))
            {
                return new AppSettings();
            }

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
        }

        private static AppSettings CreatePersistedSettings(AppSettings settings)
        {
            AppSettings persistedSettings = new()
            {
                BackupFolder = settings.BackupFolder,
                ConnectionProfileName = settings.ConnectionProfileName,
                DefaultImportFolder = settings.DefaultImportFolder,
                MaskAccountNumbers = settings.MaskAccountNumbers,
                UseDarkMode = settings.UseDarkMode
            };

            if (!string.IsNullOrWhiteSpace(settings.ConnectionString))
            {
                persistedSettings.ProtectedConnectionString = SettingsDataProtector.Protect(settings.ConnectionString.Trim());
            }

            return persistedSettings;
        }

        private static void Normalize(AppSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.ConnectionString))
            {
                settings.ConnectionString = TryGetConnectionString(settings);
            }

            if (AppSettings.IsRetiredLocalDefault(settings.ConnectionString))
            {
                settings.ConnectionString = AppSettings.LocalDbConnectionString;
            }

            settings.ConnectionString = settings.ConnectionString?.Trim() ?? string.Empty;
            settings.ConnectionProfileName = AppSettings.ResolveProfileName(settings.ConnectionString);
            settings.LegacyPlainTextConnectionString = null;
            settings.DefaultImportFolder = settings.DefaultImportFolder?.Trim() ?? string.Empty;
            settings.BackupFolder = settings.BackupFolder?.Trim() ?? string.Empty;
        }

        private static string TryGetConnectionString(AppSettings settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.ProtectedConnectionString))
            {
                try
                {
                    return SettingsDataProtector.Unprotect(settings.ProtectedConnectionString);
                }
                catch (Exception)
                {
                    return string.Empty;
                }
            }

            if (!string.IsNullOrWhiteSpace(settings.LegacyPlainTextConnectionString))
            {
                return settings.LegacyPlainTextConnectionString;
            }

            return AppSettings.DefaultConnectionString;
        }
    }
}
