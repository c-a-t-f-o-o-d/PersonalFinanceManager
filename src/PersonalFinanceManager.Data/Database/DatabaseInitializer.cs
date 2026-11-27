using PersonalFinanceManager.Data.Logging;
using PersonalFinanceManager.Data.Sql;
using Microsoft.Data.SqlClient;

namespace PersonalFinanceManager.Data.Database
{
    public sealed class DatabaseInitializer
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly FileLogger _logger;
        private readonly SqlResourceReader _sqlResources = new();
        private readonly MigrationScriptLocator _scriptLocator = new();
        private readonly SqlScriptRunner _scriptRunner = new();

        public DatabaseInitializer(SqlConnectionFactory connectionFactory, FileLogger logger)
        {
            _connectionFactory = connectionFactory;
            _logger = logger;
        }

        public async Task<DatabaseInitializationResult> InitializeAsync(CancellationToken cancellationToken = default)
        {
            DatabaseInitializationResult result = new();
            await EnsureDatabaseExistsAsync(cancellationToken).ConfigureAwait(false);
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureMigrationTrackingTableAsync(connection, cancellationToken).ConfigureAwait(false);
            HashSet<string> appliedScriptNames = await GetAppliedScriptNamesAsync(connection, cancellationToken).ConfigureAwait(false);
            HashSet<string> appliedSchemaVersions = await GetAppliedSchemaVersionsAsync(connection, cancellationToken).ConfigureAwait(false);

            foreach (string scriptPath in _scriptLocator.GetMigrationScriptPaths())
            {
                string scriptFileName = Path.GetFileName(scriptPath) ?? string.Empty;

                if (_scriptLocator.ObsoleteScripts.Contains(scriptFileName))
                {
                    DatabaseMigrationResult skipped = CreateResult(scriptFileName, null, "OBSOLETE", DatabaseMigrationStatus.ObsoleteSkipped, "Obsolete migration skipped.");
                    result.Add(skipped);
                    await RecordMigrationAsync(connection, skipped, cancellationToken).ConfigureAwait(false);
                    _logger.Info("Database", $"Skipped obsolete migration {scriptFileName}.");
                    continue;
                }

                string scriptText = await File.ReadAllTextAsync(scriptPath, cancellationToken).ConfigureAwait(false);
                DatabaseMigrationDescriptor descriptor = DatabaseMigrationDescriptor.FromFile(scriptPath, scriptText);

                if (appliedScriptNames.Contains(descriptor.ScriptName))
                {
                    DatabaseMigrationResult skipped = CreateResult(descriptor, DatabaseMigrationStatus.AlreadyApplied, "Migration already recorded as applied.");
                    result.Add(skipped);
                    _logger.Info("Database", $"Skipped already-applied migration {descriptor.ScriptName}.");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(descriptor.SchemaVersion) && appliedSchemaVersions.Contains(descriptor.SchemaVersion))
                {
                    DatabaseMigrationResult skipped = CreateResult(descriptor, DatabaseMigrationStatus.AlreadyApplied, "Migration schema marker already exists from a legacy run.");
                    result.Add(skipped);
                    await RecordMigrationAsync(connection, skipped, cancellationToken).ConfigureAwait(false);
                    appliedScriptNames.Add(descriptor.ScriptName);
                    _logger.Info("Database", $"Recorded legacy-applied migration {descriptor.ScriptName}.");
                    continue;
                }

                try
                {
                    await _scriptRunner.ExecuteScriptAsync(connection, descriptor.ScriptText, cancellationToken).ConfigureAwait(false);
                    DatabaseMigrationResult executed = CreateResult(descriptor, DatabaseMigrationStatus.Executed, "Migration executed.");
                    result.Add(executed);
                    await RecordMigrationAsync(connection, executed, cancellationToken).ConfigureAwait(false);
                    appliedScriptNames.Add(descriptor.ScriptName);

                    if (!string.IsNullOrWhiteSpace(descriptor.SchemaVersion))
                    {
                        appliedSchemaVersions.Add(descriptor.SchemaVersion);
                    }

                    _logger.Info("Database", $"Executed migration {descriptor.ScriptName}.");
                }
                catch (SqlException ex)
                {
                    DatabaseMigrationResult failed = CreateResult(descriptor, DatabaseMigrationStatus.Failed, ex.Message);
                    result.Add(failed);
                    await RecordMigrationAsync(connection, failed, cancellationToken).ConfigureAwait(false);
                    _logger.Error("Database", $"Migration {descriptor.ScriptName} failed.", ex);
                    throw new InvalidOperationException($"Database migration '{descriptor.ScriptName}' failed. {ex.Message}", ex);
                }
            }

            _logger.Info("Database", $"Schema initialization complete. {result.Summary}.");
            return result;
        }

        private static DatabaseMigrationResult CreateResult(DatabaseMigrationDescriptor descriptor, DatabaseMigrationStatus status, string message)
        {
            return CreateResult(descriptor.ScriptName, descriptor.SchemaVersion, descriptor.ScriptHash, status, message);
        }

        private static DatabaseMigrationResult CreateResult(string scriptName, string? schemaVersion, string scriptHash, DatabaseMigrationStatus status, string message)
        {
            return new DatabaseMigrationResult
            {
                ScriptName = scriptName,
                SchemaVersion = schemaVersion,
                ScriptHash = scriptHash,
                Status = status,
                Message = message
            };
        }

        private async Task EnsureMigrationTrackingTableAsync(SqlConnection connection, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.CommandTimeout = 60;
            command.CommandText = _sqlResources.Read("Schema/EnsureMigrationTrackingTable.sql");
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task<HashSet<string>> GetAppliedScriptNamesAsync(SqlConnection connection, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = _sqlResources.Read("Schema/GetAppliedScriptNames.sql");
            HashSet<string> values = new(StringComparer.OrdinalIgnoreCase);
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                values.Add(reader.GetString(0));
            }

            return values;
        }

        private async Task<HashSet<string>> GetAppliedSchemaVersionsAsync(SqlConnection connection, CancellationToken cancellationToken)
        {
            await using SqlCommand exists = connection.CreateCommand();
            exists.CommandText = _sqlResources.Read("Schema/GetObjectId.sql");
            exists.Parameters.Add(SqlParameters.NVarChar("@ObjectName", "dbo.SchemaMetadata", 256));
            object? objectId = await exists.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            HashSet<string> values = new(StringComparer.OrdinalIgnoreCase);

            if (objectId is null || objectId == DBNull.Value)
            {
                return values;
            }

            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = _sqlResources.Read("Schema/GetSchemaVersions.sql");
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                values.Add(reader.GetString(0));
            }

            return values;
        }

        private async Task RecordMigrationAsync(SqlConnection connection, DatabaseMigrationResult migration, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.CommandTimeout = 60;
            command.CommandText = _sqlResources.Read("Schema/RecordMigration.sql");
            command.Parameters.Add(SqlParameters.NVarChar("@ScriptName", migration.ScriptName, 260));
            command.Parameters.Add(SqlParameters.NVarChar("@SchemaVersion", migration.SchemaVersion, 40));
            command.Parameters.Add(SqlParameters.NVarChar("@ScriptHash", migration.ScriptHash, 64));
            command.Parameters.Add(SqlParameters.NVarChar("@Status", migration.Status.ToString(), 30));
            command.Parameters.Add(SqlParameters.NVarChar("@Message", migration.Message, 1000));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task EnsureDatabaseExistsAsync(CancellationToken cancellationToken)
        {
            try
            {
                await using SqlConnection existingConnection = _connectionFactory.CreateConnection();
                await existingConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (SqlException) { }

            await using SqlConnection connection = _connectionFactory.CreateMasterConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();
            command.CommandTimeout = 60;
            command.CommandText = _sqlResources.Read("Schema/CreateDatabaseIfMissing.sql");
            command.Parameters.Add(SqlParameters.NVarChar("@DatabaseName", _connectionFactory.DatabaseName, 128));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
