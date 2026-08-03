namespace PersonalFinanceManager.Data.Database
{
    public sealed class DatabaseInitializationResult
    {
        private readonly List<DatabaseMigrationResult> _migrations = new();

        public IReadOnlyList<DatabaseMigrationResult> Migrations => _migrations;
        public int ExecutedCount => _migrations.Count(migration => migration.Status == DatabaseMigrationStatus.Executed);
        public int SkippedCount => _migrations.Count(migration => migration.Status == DatabaseMigrationStatus.AlreadyApplied || migration.Status == DatabaseMigrationStatus.ObsoleteSkipped);
        public int FailedCount => _migrations.Count(migration => migration.Status == DatabaseMigrationStatus.Failed);
        public string Summary => $"Executed {ExecutedCount}, skipped {SkippedCount}, failed {FailedCount}";

        public void Add(DatabaseMigrationResult migration)
        {
            ArgumentNullException.ThrowIfNull(migration);
            _migrations.Add(migration);
        }
    }
}
