namespace PersonalFinanceManager.Data.Database
{
    public enum DatabaseMigrationStatus
    {
        Executed,
        AlreadyApplied,
        ObsoleteSkipped,
        Failed
    }

    public sealed class DatabaseMigrationResult
    {
        public string ScriptName { get; set; } = string.Empty;
        public string? SchemaVersion { get; set; }
        public string ScriptHash { get; set; } = string.Empty;
        public DatabaseMigrationStatus Status { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
