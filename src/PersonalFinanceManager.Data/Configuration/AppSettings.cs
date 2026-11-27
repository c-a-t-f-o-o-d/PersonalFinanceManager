using PersonalFinanceManager.Core.Application;
using System.Text.Json.Serialization;

namespace PersonalFinanceManager.Data.Configuration
{
    public sealed class AppSettings
    {
        public const string SetupRequiredProfileName = ApplicationIdentity.SetupRequiredProfileName;
        public const string LocalDbProfileName = ApplicationIdentity.LocalDbProfileName;
        public const string NetworkSqlServerTemplateProfileName = ApplicationIdentity.NetworkSqlServerTemplateProfileName;
        public const string CustomProfileName = ApplicationIdentity.CustomProfileName;
        public const string LocalDbConnectionString = ApplicationIdentity.LocalDbConnectionString;
        public const string NetworkSqlServerTemplateConnectionString = ApplicationIdentity.NetworkSqlServerTemplateConnectionString;
        public const string DefaultConnectionString = ApplicationIdentity.EmptyConnectionString;
        private const string _oldLocalDbConnectionString = @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=PersonalFinanceManager;Integrated Security=True;Persist Security Info=False;Pooling=False;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Application Name=PersonalFinanceManager";
        private const string _oldLocalHostConnectionString = "Data Source=LOCALHOST;Initial Catalog=PersonalFinanceManager;Integrated Security=True;Persist Security Info=False;Pooling=False;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=True;Application Name=PersonalFinanceManager";

        public string ConnectionProfileName { get; set; } = SetupRequiredProfileName;

        [JsonIgnore]
        public string ConnectionString { get; set; } = DefaultConnectionString;

        [JsonPropertyName("connectionString")]
        public string? LegacyPlainTextConnectionString { get; set; }

        public string? ProtectedConnectionString { get; set; }
        public string DefaultImportFolder { get; set; } = string.Empty;
        public string BackupFolder { get; set; } = string.Empty;
        public bool MaskAccountNumbers { get; set; } = true;

        public bool HasUsableConnectionString()
        {
            return !string.IsNullOrWhiteSpace(ConnectionString) && !ContainsPlaceholderSecret(ConnectionString);
        }

        public static string ResolveProfileName(string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return SetupRequiredProfileName;
            }

            string trimmed = connectionString.Trim();

            if (string.Equals(trimmed, LocalDbConnectionString, StringComparison.OrdinalIgnoreCase))
            {
                return LocalDbProfileName;
            }

            if (string.Equals(trimmed, NetworkSqlServerTemplateConnectionString, StringComparison.OrdinalIgnoreCase))
            {
                return NetworkSqlServerTemplateProfileName;
            }

            return CustomProfileName;
        }

        public static bool IsRetiredLocalDefault(string? connectionString)
        {
            string trimmed = connectionString?.Trim() ?? string.Empty;
            return string.Equals(trimmed, _oldLocalDbConnectionString, StringComparison.OrdinalIgnoreCase) || string.Equals(trimmed, _oldLocalHostConnectionString, StringComparison.OrdinalIgnoreCase);
        }

        public static bool ContainsPlaceholderSecret(string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return false;
            }

            return connectionString.Contains("YOUR_SQL_SERVER_OR_IP", StringComparison.OrdinalIgnoreCase) ||
                connectionString.Contains("YOUR_SQL_LOGIN", StringComparison.OrdinalIgnoreCase) ||
                connectionString.Contains("YOUR_SQL_PASSWORD", StringComparison.OrdinalIgnoreCase) ||
                connectionString.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase);
        }
    }
}
