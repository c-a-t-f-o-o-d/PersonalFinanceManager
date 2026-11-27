using PersonalFinanceManager.Data.Sql;
using Microsoft.Data.SqlClient;

namespace PersonalFinanceManager.Data.Database
{
    public sealed class SchemaStatus
    {
        public bool Connected { get; set; }
        public string ServerVersion { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public string SchemaVersion { get; set; } = string.Empty;
        public string SchemaState { get; set; } = string.Empty;
        public int TableCount { get; set; }
        public int AppliedMigrationCount { get; set; }
        public int PendingMigrationCount { get; set; }
        public int FailedMigrationCount { get; set; }
        public string LatestMigration { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public sealed class SchemaStatusService
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly SqlResourceReader _sqlResources = new();
        private readonly MigrationScriptLocator _scriptLocator = new();

        public SchemaStatusService(SqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<SchemaStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            SchemaStatus status = new();

            try
            {
                await using SqlConnection connection = _connectionFactory.CreateConnection();
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                status.Connected = true;
                status.ServerVersion = connection.ServerVersion;
                status.DatabaseName = connection.Database;
                status.TableCount = await GetTableCountAsync(connection, cancellationToken).ConfigureAwait(false);
                status.SchemaVersion = await GetSchemaVersionAsync(connection, cancellationToken).ConfigureAwait(false);
                await PopulateMigrationStatusAsync(connection, status, cancellationToken).ConfigureAwait(false);
                status.Message = "Connected";
            }
            catch (SqlException ex)
            {
                status.Message = ex.Message;
            }
            catch (InvalidOperationException ex)
            {
                status.Message = ex.Message;
            }

            return status;
        }

        private async Task PopulateMigrationStatusAsync(SqlConnection connection, SchemaStatus status, CancellationToken cancellationToken)
        {
            bool metadataExists = await ObjectExistsAsync(connection, "dbo.SchemaMetadata", cancellationToken).ConfigureAwait(false);
            bool migrationTableExists = await ObjectExistsAsync(connection, "dbo.SchemaMigration", cancellationToken).ConfigureAwait(false);

            if (status.TableCount == 0)
            {
                status.SchemaState = "Empty database";
                status.PendingMigrationCount = CountAvailableMigrations();
                return;
            }

            if (!metadataExists)
            {
                status.SchemaState = "Schema metadata missing";
                status.PendingMigrationCount = CountAvailableMigrations();
                return;
            }

            HashSet<string> appliedScriptNames = migrationTableExists
                ? await GetAppliedScriptNamesAsync(connection, cancellationToken).ConfigureAwait(false)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            HashSet<string> appliedSchemaVersions = await GetAppliedSchemaVersionsAsync(connection, cancellationToken).ConfigureAwait(false);
            status.AppliedMigrationCount = appliedScriptNames.Count;
            status.FailedMigrationCount = migrationTableExists ? await GetFailedMigrationCountAsync(connection, cancellationToken).ConfigureAwait(false) : 0;
            status.LatestMigration = migrationTableExists ? await GetLatestMigrationAsync(connection, cancellationToken).ConfigureAwait(false) : "not tracked";
            status.PendingMigrationCount = CountPendingMigrations(appliedScriptNames, appliedSchemaVersions);

            if (!migrationTableExists)
            {
                status.SchemaState = status.PendingMigrationCount == 0 ? "Legacy schema, current" : "Legacy schema, pending migrations";
                return;
            }

            if (status.FailedMigrationCount > 0)
            {
                status.SchemaState = "Migration failure recorded";
                return;
            }

            status.SchemaState = status.PendingMigrationCount == 0 ? "Current" : "Pending migrations";
        }

        private int CountAvailableMigrations()
        {
            return _scriptLocator.GetMigrationScriptPaths()
                .Select(Path.GetFileName)
                .Count(scriptName => scriptName is not null && !_scriptLocator.ObsoleteScripts.Contains(scriptName));
        }

        private int CountPendingMigrations(HashSet<string> appliedScriptNames, HashSet<string> appliedSchemaVersions)
        {
            int pending = 0;

            foreach (string scriptPath in _scriptLocator.GetMigrationScriptPaths())
            {
                string scriptName = Path.GetFileName(scriptPath) ?? string.Empty;

                if (_scriptLocator.ObsoleteScripts.Contains(scriptName) || appliedScriptNames.Contains(scriptName))
                {
                    continue;
                }

                string scriptText = File.ReadAllText(scriptPath);
                string? schemaVersion = DatabaseMigrationDescriptor.ExtractSchemaVersion(scriptText);

                if (!string.IsNullOrWhiteSpace(schemaVersion) && appliedSchemaVersions.Contains(schemaVersion))
                {
                    continue;
                }

                pending++;
            }

            return pending;
        }

        private async Task<bool> ObjectExistsAsync(SqlConnection connection, string objectName, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = _sqlResources.Read("Schema/GetObjectId.sql");
            command.Parameters.Add(SqlParameters.NVarChar("@ObjectName", objectName, 256));
            object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return value is not null && value != DBNull.Value;
        }

        private async Task<int> GetTableCountAsync(SqlConnection connection, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = _sqlResources.Read("Schema/GetUserTableCount.sql");
            object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return value is null ? 0 : Convert.ToInt32(value);
        }

        private async Task<string> GetSchemaVersionAsync(SqlConnection connection, CancellationToken cancellationToken)
        {
            if (!await ObjectExistsAsync(connection, "dbo.SchemaMetadata", cancellationToken).ConfigureAwait(false))
            {
                return "none";
            }

            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = _sqlResources.Read("Schema/GetLatestSchemaVersion.sql");
            object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return value?.ToString() ?? "none";
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
            HashSet<string> values = new(StringComparer.OrdinalIgnoreCase);

            if (!await ObjectExistsAsync(connection, "dbo.SchemaMetadata", cancellationToken).ConfigureAwait(false))
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

        private async Task<int> GetFailedMigrationCountAsync(SqlConnection connection, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = _sqlResources.Read("Schema/GetFailedMigrationCount.sql");
            object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return value is null ? 0 : Convert.ToInt32(value);
        }

        private async Task<string> GetLatestMigrationAsync(SqlConnection connection, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = _sqlResources.Read("Schema/GetLatestMigration.sql");
            object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return value?.ToString() ?? "none";
        }
    }
}
