using System.Text.Json;
using PersonalFinanceManager.Data.Configuration;

namespace PersonalFinanceManager.Tests.Configuration
{
    [TestClass]
    public sealed class AppearanceSettingsTests
    {
        private string _directory = null!;
        private SettingsService _settings = null!;

        [TestInitialize]
        public void CreateSettingsDirectory()
        {
            _directory = Path.Combine(Path.GetTempPath(), "PersonalFinanceManager.SettingsTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _settings = new SettingsService(_directory);
        }

        [TestCleanup]
        public void RemoveSettingsDirectory()
        {
            Directory.Delete(_directory, true);
        }

        [TestMethod]
        public void ExistingSettingsWithoutAppearanceUseLightMode()
        {
            File.WriteAllText(_settings.UserSettingsPath, "{\"maskAccountNumbers\":false,\"defaultImportFolder\":\"statements\"}");
            AppSettings settings = _settings.Load();
            Assert.IsFalse(settings.UseDarkMode);
            Assert.IsFalse(settings.MaskAccountNumbers);
            Assert.AreEqual("statements", settings.DefaultImportFolder);
        }

        [TestMethod]
        public void SavingSettingsPreservesAppearanceAndOtherPreferences()
        {
            _settings.Save(new AppSettings
            {
                UseDarkMode = true,
                DefaultImportFolder = " statements ",
                BackupFolder = " backups ",
                MaskAccountNumbers = false
            });

            AppSettings reloaded = _settings.Load();
            Assert.IsTrue(reloaded.UseDarkMode);
            Assert.AreEqual("statements", reloaded.DefaultImportFolder);
            Assert.AreEqual("backups", reloaded.BackupFolder);
            Assert.IsFalse(reloaded.MaskAccountNumbers);

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(_settings.UserSettingsPath));
            Assert.IsTrue(document.RootElement.GetProperty("useDarkMode").GetBoolean());
            Assert.IsFalse(document.RootElement.TryGetProperty("connectionString", out _));
        }

        [TestMethod]
        public void AppearanceChangeDoesNotRewriteProtectedConnectionOrUnrelatedSettings()
        {
            const string original = """
                {
                    "protectedConnectionString": "opaque-protected-connection",
                    "connectionProfileName": "Custom",
                    "defaultImportFolder": "statements",
                    "maskAccountNumbers": false,
                    "futureSetting": { "mode": "preserve" }
                }
                """;

            File.WriteAllText(_settings.UserSettingsPath, original);
            _settings.SaveAppearancePreference(true);

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(_settings.UserSettingsPath));
            JsonElement root = document.RootElement;
            Assert.IsTrue(root.GetProperty("useDarkMode").GetBoolean());
            Assert.AreEqual("opaque-protected-connection", root.GetProperty("protectedConnectionString").GetString());
            Assert.AreEqual("Custom", root.GetProperty("connectionProfileName").GetString());
            Assert.AreEqual("statements", root.GetProperty("defaultImportFolder").GetString());
            Assert.IsFalse(root.GetProperty("maskAccountNumbers").GetBoolean());
            Assert.AreEqual("preserve", root.GetProperty("futureSetting").GetProperty("mode").GetString());
            Assert.IsFalse(root.TryGetProperty("connectionString", out _));
        }

        [TestMethod]
        public void AppearanceCanBeSavedBeforeDatabaseSetupAndChangedBack()
        {
            _settings.SaveAppearancePreference(true);
            AppSettings dark = _settings.Load();
            Assert.IsTrue(dark.UseDarkMode);
            Assert.IsFalse(dark.HasUsableConnectionString());
            Assert.IsTrue(dark.MaskAccountNumbers);

            _settings.SaveAppearancePreference(false);
            Assert.IsFalse(_settings.Load().UseDarkMode);
        }

        [TestMethod]
        public void InvalidSettingsDocumentIsNotReplacedByAppearanceChange()
        {
            const string original = "[]";
            File.WriteAllText(_settings.UserSettingsPath, original);
            Assert.ThrowsExactly<InvalidDataException>(() => _settings.SaveAppearancePreference(true));
            Assert.AreEqual(original, File.ReadAllText(_settings.UserSettingsPath));
        }
    }
}
