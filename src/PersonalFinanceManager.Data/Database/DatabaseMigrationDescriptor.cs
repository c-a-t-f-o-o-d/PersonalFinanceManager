using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PersonalFinanceManager.Data.Database
{
    public sealed class DatabaseMigrationDescriptor
    {
        private static readonly Regex _schemaVersionRegex = new(
            @"SchemaVersion\s*=\s*N?'(?<version>[^']+)'|INSERT\s+INTO\s+dbo\.SchemaMetadata\s*\(\s*SchemaVersion\s*\)\s*VALUES\s*\(\s*N?'(?<version>[^']+)'\s*\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private DatabaseMigrationDescriptor(string fullPath, string scriptText)
        {
            FullPath = fullPath;
            ScriptName = Path.GetFileName(fullPath) ?? fullPath;
            ScriptText = scriptText;
            SchemaVersion = ExtractSchemaVersion(scriptText);
            ScriptHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(scriptText)));
        }

        public string FullPath { get; }
        public string ScriptName { get; }
        public string ScriptText { get; }
        public string? SchemaVersion { get; }
        public string ScriptHash { get; }

        public static DatabaseMigrationDescriptor FromFile(string fullPath, string scriptText)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
            ArgumentNullException.ThrowIfNull(scriptText);
            return new DatabaseMigrationDescriptor(fullPath, scriptText);
        }

        public static string? ExtractSchemaVersion(string scriptText)
        {
            ArgumentNullException.ThrowIfNull(scriptText);

            MatchCollection matches = _schemaVersionRegex.Matches(scriptText);
            if (matches.Count == 0)
            {
                return null;
            }

            Match match = matches[^1];
            Group group = match.Groups["version"];
            return group.Success ? group.Value.Trim() : null;
        }
    }
}
