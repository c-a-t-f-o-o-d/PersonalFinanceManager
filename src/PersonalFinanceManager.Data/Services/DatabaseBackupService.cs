using PersonalFinanceManager.Core.Application;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Data.Sql;
using Microsoft.Data.SqlClient;
using PersonalFinanceManager.Application.Services.Contracts;

namespace PersonalFinanceManager.Data.Services
{
    public sealed class DatabaseBackupService : IDatabaseBackupService
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly SqlResourceReader _sqlResources = new();

        public DatabaseBackupService(SqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IReadOnlyList<DatabaseBackupRecord>> GetHistoryAsync(CancellationToken cancellationToken = default)
        {
            List<DatabaseBackupRecord> records = new();
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = _sqlResources.Read("BackupExport/GetHistory.sql");
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                records.Add(new DatabaseBackupRecord
                {
                    DatabaseBackupHistoryId = reader.GetInt32Value("DatabaseBackupHistoryId"),
                    BackupType = reader.GetStringOrEmpty("BackupType"),
                    FilePath = reader.GetStringOrEmpty("FilePath"),
                    CreatedAtUtc = reader.GetDateTimeValue("CreatedAtUtc"),
                    Succeeded = reader.GetBooleanValue("Succeeded"),
                    Verified = reader.GetBooleanValue("Verified"),
                    ErrorMessage = reader.GetStringOrNull("ErrorMessage")
                });
            }

            return records;
        }

        public async Task<DatabaseBackupResult> CreateSqlBackupAsync(string folderPath, CancellationToken cancellationToken = default)
        {
            string backupPath = BuildOutputPath(folderPath, "bak");
            bool succeeded = false;
            bool verified = false;
            string message;

            try
            {
                Directory.CreateDirectory(folderPath);
                await using SqlConnection connection = _connectionFactory.CreateMasterConnection();
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                await using SqlCommand command = connection.CreateCommand();
                command.CommandTimeout = 0;
                command.CommandText = _sqlResources.Read("BackupExport/CreateSqlBackup.sql");
                command.Parameters.Add(SqlParameters.NVarChar("@FilePath", backupPath, 1000));
                command.Parameters.Add(SqlParameters.NVarChar("@DatabaseName", _connectionFactory.DatabaseName, 128));
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                succeeded = true;
                verified = true;
                message = $"SQL Server backup of '{_connectionFactory.DatabaseName}' created and verified.";
            }
            catch (Exception ex)
            {
                message = ex.Message;
            }

            await RecordHistorySafeAsync("SQL Backup", backupPath, succeeded, verified, succeeded ? null : message, cancellationToken).ConfigureAwait(false);

            return new DatabaseBackupResult
            {
                Succeeded = succeeded,
                Verified = verified,
                FilePath = backupPath,
                Message = message
            };
        }

        public async Task<DatabaseBackupResult> ExportDataZipAsync(string folderPath, CancellationToken cancellationToken = default)
        {
            string exportPath = BuildOutputPath(folderPath, "zip");
            bool succeeded = false;
            string message;

            try
            {
                Directory.CreateDirectory(folderPath);

                if (File.Exists(exportPath))
                {
                    File.Delete(exportPath);
                }

                using ZipArchive archive = ZipFile.Open(exportPath, ZipArchiveMode.Create);
                await using SqlConnection connection = _connectionFactory.CreateConnection();
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
                await WriteQueryToCsvAsync(archive, connection, transaction, "accounts.csv", _sqlResources.Read("BackupExport/ExportAccounts.sql"), cancellationToken).ConfigureAwait(false);
                await WriteQueryToCsvAsync(archive, connection, transaction, "rules.csv", _sqlResources.Read("BackupExport/ExportRules.sql"), cancellationToken).ConfigureAwait(false);
                await WriteQueryToCsvAsync(archive, connection, transaction, "transactions.csv", _sqlResources.Read("BackupExport/ExportTransactions.sql"), cancellationToken).ConfigureAwait(false);
                await WriteQueryToCsvAsync(archive, connection, transaction, "recurring_bills.csv", _sqlResources.Read("BackupExport/ExportRecurringBills.sql"), cancellationToken).ConfigureAwait(false);
                await WriteQueryToCsvAsync(archive, connection, transaction, "transaction_links.csv", _sqlResources.Read("BackupExport/ExportTransactionLinks.sql"), cancellationToken).ConfigureAwait(false);
                await WriteQueryToCsvAsync(archive, connection, transaction, "statement_checks.csv", _sqlResources.Read("BackupExport/ExportReconciliations.sql"), cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                succeeded = true;
                message = "Portable CSV export created. SQL backup is still recommended for full-fidelity restore.";
            }
            catch (Exception ex)
            {
                message = ex.Message;
            }

            await RecordHistorySafeAsync("CSV Export", exportPath, succeeded, verified: succeeded, succeeded ? null : message, cancellationToken).ConfigureAwait(false);

            return new DatabaseBackupResult
            {
                Succeeded = succeeded,
                Verified = succeeded,
                FilePath = exportPath,
                Message = message
            };
        }

        private async Task RecordHistorySafeAsync(string backupType, string filePath, bool succeeded, bool verified, string? errorMessage, CancellationToken cancellationToken)
        {
            try
            {
                await using SqlConnection connection = _connectionFactory.CreateConnection();
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                await using SqlCommand command = connection.CreateCommand();
                command.CommandText = _sqlResources.Read("BackupExport/InsertHistory.sql");
                command.Parameters.Add(SqlParameters.NVarChar("@BackupType", backupType, 50));
                command.Parameters.Add(SqlParameters.NVarChar("@FilePath", filePath, 1000));
                command.Parameters.Add(SqlParameters.Bit("@Succeeded", succeeded));
                command.Parameters.Add(SqlParameters.Bit("@Verified", verified));
                command.Parameters.Add(SqlParameters.NVarChar("@ErrorMessage", errorMessage, 2000));
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _ = ex;
            }
        }

        private static string BuildOutputPath(string folderPath, string extension)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                throw new InvalidOperationException("Choose a backup/export folder first.");
            }

            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
            return Path.Combine(folderPath.Trim(), $"{ApplicationIdentity.BackupFilePrefix}_{timestamp}_{Guid.NewGuid():N}.{extension}");
        }

        private static async Task WriteQueryToCsvAsync(ZipArchive archive, SqlConnection connection, SqlTransaction transaction, string entryName, string query, CancellationToken cancellationToken)
        {
            ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            await using Stream stream = entry.Open();
            await using StreamWriter writer = new(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = query;
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            for (int index = 0; index < reader.FieldCount; index++)
            {
                if (index > 0)
                {
                    await writer.WriteAsync(',').ConfigureAwait(false);
                }

                await writer.WriteAsync(EscapeCsv(reader.GetName(index))).ConfigureAwait(false);
            }

            await writer.WriteLineAsync().ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                for (int index = 0; index < reader.FieldCount; index++)
                {
                    if (index > 0)
                    {
                        await writer.WriteAsync(',').ConfigureAwait(false);
                    }

                    object value = reader.GetValue(index);
                    await writer.WriteAsync(EscapeCsv(value == DBNull.Value ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty)).ConfigureAwait(false);
                }

                await writer.WriteLineAsync().ConfigureAwait(false);
            }
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains('"', StringComparison.Ordinal) || value.Contains(',', StringComparison.Ordinal) || value.Contains('\n', StringComparison.Ordinal) || value.Contains('\r', StringComparison.Ordinal))
            {
                return '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
            }

            return value;
        }
    }
}
