namespace PersonalFinanceManager.Data.Database
{
    public sealed class MigrationScriptLocator
    {
        private static readonly HashSet<string> _obsoleteScripts = new(StringComparer.OrdinalIgnoreCase)
        {
            "008_paypal_import_matching.sql"
        };

        public IReadOnlySet<string> ObsoleteScripts => _obsoleteScripts;

        public IReadOnlyList<string> GetMigrationScriptPaths()
        {
            string sqlDirectory = Path.Combine(AppContext.BaseDirectory, "sql");
            string migrationDirectory = Path.Combine(sqlDirectory, "migrations");
            string searchDirectory = Directory.Exists(migrationDirectory) ? migrationDirectory : sqlDirectory;

            if (!Directory.Exists(searchDirectory))
            {
                return Array.Empty<string>();
            }

            string[] scripts = Directory.GetFiles(searchDirectory, "*.sql", SearchOption.TopDirectoryOnly);
            Array.Sort(scripts, StringComparer.OrdinalIgnoreCase);
            return scripts;
        }
    }
}
