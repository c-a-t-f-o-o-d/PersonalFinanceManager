using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Data.Sql;
using Microsoft.Data.SqlClient;

namespace PersonalFinanceManager.Data.Repositories
{
    public sealed class TransactionLinkRepository
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly SqlResourceReader _sqlResources;

        public TransactionLinkRepository(SqlConnectionFactory connectionFactory, SqlResourceReader? sqlResources = null)
        {
            _connectionFactory = connectionFactory;
            _sqlResources = sqlResources ?? new SqlResourceReader();
        }

        public async Task<IReadOnlyList<TransactionLinkRecord>> GetLinksAsync(TransactionLinkFilter filter, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            int maxRows = Math.Clamp(filter.MaxRows <= 0 ? 1000 : filter.MaxRows, 1, 10000);
            List<string> clauses = new();
            List<SqlParameter> parameters = new();

            if (filter.AccountId.HasValue)
            {
                clauses.Add("(source.AccountId = @AccountId OR linked.AccountId = @AccountId)");
                parameters.Add(SqlParameters.Int("@AccountId", filter.AccountId.Value));
            }

            if (!string.IsNullOrWhiteSpace(filter.LinkType))
            {
                clauses.Add("link.LinkType = @LinkType");
                parameters.Add(SqlParameters.NVarChar("@LinkType", filter.LinkType.Trim(), 100));
            }

            if (!string.IsNullOrWhiteSpace(filter.SearchText))
            {
                clauses.Add("(source.[Description] LIKE @SearchText OR linked.[Description] LIKE @SearchText OR sourceAccount.AccountNickname LIKE @SearchText OR linkedAccount.AccountNickname LIKE @SearchText OR link.Notes LIKE @SearchText OR link.LinkType LIKE @SearchText)");
                parameters.Add(SqlParameters.NVarChar("@SearchText", "%" + filter.SearchText.Trim() + "%", 1000));
            }

            string where = clauses.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", clauses);
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();

            command.CommandText = ReadSqlWithTopAndWhere("TransactionLinks/GetLinks.sql", maxRows, where);

            foreach (SqlParameter parameter in parameters)
            {
                command.Parameters.Add(parameter);
            }

            List<TransactionLinkRecord> links = new();
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                links.Add(MapLink(reader));
            }

            return links;
        }

        public async Task<IReadOnlyList<string>> GetLinkTypesAsync(CancellationToken cancellationToken = default)
        {
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();

            command.CommandText = _sqlResources.Read("TransactionLinks/GetLinkTypes.sql");

            List<string> values = new();
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                values.Add(reader.GetStringOrEmpty("LinkType"));
            }

            return values;
        }

        public async Task<IReadOnlyList<TransactionLinkTransactionOption>> GetTransactionOptionsAsync(int? accountId, string? searchText, int maxRows, CancellationToken cancellationToken = default)
        {
            int normalizedMaxRows = Math.Clamp(maxRows <= 0 ? 500 : maxRows, 1, 5000);
            List<string> clauses = new();
            List<SqlParameter> parameters = new();

            if (accountId.HasValue)
            {
                clauses.Add("t.AccountId = @AccountId");
                parameters.Add(SqlParameters.Int("@AccountId", accountId.Value));
            }

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                clauses.Add("(t.[Description] LIKE @SearchText OR t.[Type] LIKE @SearchText OR t.Category LIKE @SearchText OR t.SpendingClass LIKE @SearchText OR t.FundingSource LIKE @SearchText OR t.ExternalTransactionId LIKE @SearchText OR a.AccountNickname LIKE @SearchText)");
                parameters.Add(SqlParameters.NVarChar("@SearchText", "%" + searchText.Trim() + "%", 1000));
            }

            string where = clauses.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", clauses);
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();

            command.CommandText = ReadSqlWithTopAndWhere("TransactionLinks/GetTransactionOptions.sql", normalizedMaxRows, where);

            foreach (SqlParameter parameter in parameters)
            {
                command.Parameters.Add(parameter);
            }

            List<TransactionLinkTransactionOption> transactions = new();
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                transactions.Add(new TransactionLinkTransactionOption
                {
                    ImportedTransactionId = reader.GetInt64Value("ImportedTransactionId"),
                    AccountId = reader.GetInt32Value("AccountId"),
                    AccountNickname = reader.GetStringOrEmpty("AccountNickname"),
                    TransactionDate = reader.GetDateOnlyOrNull("TransactionDate") ?? DateOnly.MinValue,
                    Amount = reader.GetDecimalValue("Amount"),
                    Type = reader.GetStringOrEmpty("Type"),
                    Description = reader.GetStringOrEmpty("Description"),
                    Category = reader.GetStringOrNull("Category"),
                    CategoryType = reader.GetStringOrNull("SpendingClass"),
                    FundingSource = reader.GetStringOrNull("FundingSource"),
                    ExternalTransactionId = reader.GetStringOrNull("ExternalTransactionId")
                });
            }

            return transactions;
        }

        public async Task<long> SaveAsync(TransactionLinkEditModel model, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(model);
            model.LinkType = TransactionLinkTypes.Normalize(model.LinkType);
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);

            try
            {
                FinancialLinkState state = await FinancialLinkStore.LoadAsync(connection, transaction, _sqlResources, cancellationToken).ConfigureAwait(false);
                FinancialLinkStore.Validate(state, model);
                FinancialLink? previous = state.Links.FirstOrDefault(link => link.Id == model.TransactionLinkId);
                if (previous is not null && (previous.SourceId != model.SourceTransactionId || previous.LinkedId != model.LinkedTransactionId
                    || TransactionLinkTypes.Normalize(previous.Type) != model.LinkType))
                {
                    await RejectAsync(connection, transaction, previous.Id, cancellationToken).ConfigureAwait(false);
                }
                long linkId = model.TransactionLinkId == 0
                    ? await InsertAsync(connection, transaction, model, cancellationToken).ConfigureAwait(false)
                    : await UpdateAsync(connection, transaction, model, cancellationToken).ConfigureAwait(false);
                await using SqlCommand clear = connection.CreateCommand();
                clear.Transaction = transaction;
                clear.CommandText = _sqlResources.Read("TransactionLinks/ClearRejection.sql");
                clear.Parameters.Add(SqlParameters.BigInt("@SourceTransactionId", model.SourceTransactionId));
                clear.Parameters.Add(SqlParameters.BigInt("@LinkedTransactionId", model.LinkedTransactionId));
                clear.Parameters.Add(SqlParameters.NVarChar("@LinkType", model.LinkType, 100));
                await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return linkId;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }

        public async Task DeleteAsync(long transactionLinkId, CancellationToken cancellationToken = default)
        {
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
            try
            {
                await RejectAsync(connection, transaction, transactionLinkId, cancellationToken).ConfigureAwait(false);
                await using SqlCommand command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = _sqlResources.Read("TransactionLinks/DeleteLink.sql");
                command.Parameters.Add(SqlParameters.BigInt("@TransactionLinkId", transactionLinkId));
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }

        private async Task RejectAsync(SqlConnection connection, SqlTransaction transaction, long id, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = _sqlResources.Read("TransactionLinks/RejectLink.sql");
            command.Parameters.Add(SqlParameters.BigInt("@TransactionLinkId", id));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task<long> InsertAsync(SqlConnection connection, SqlTransaction transaction, TransactionLinkEditModel model, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = _sqlResources.Read("TransactionLinks/InsertLink.sql");
            AddSaveParameters(command, model);
            object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt64(value);
        }

        private async Task<long> UpdateAsync(SqlConnection connection, SqlTransaction transaction, TransactionLinkEditModel model, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = _sqlResources.Read("TransactionLinks/UpdateLink.sql");
            AddSaveParameters(command, model);
            command.Parameters.Add(SqlParameters.BigInt("@TransactionLinkId", model.TransactionLinkId));
            object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt64(value);
        }

        private static void AddSaveParameters(SqlCommand command, TransactionLinkEditModel model)
        {
            command.Parameters.Add(SqlParameters.BigInt("@SourceTransactionId", model.SourceTransactionId));
            command.Parameters.Add(SqlParameters.BigInt("@LinkedTransactionId", model.LinkedTransactionId));
            command.Parameters.Add(SqlParameters.NVarChar("@LinkType", model.LinkType.Trim(), 100));
            command.Parameters.Add(SqlParameters.Decimal("@AllocationAmount", model.AllocationAmount));
            command.Parameters.Add(SqlParameters.Int("@ConfidenceScore", model.ConfidenceScore));
            command.Parameters.Add(SqlParameters.NVarChar("@Notes", model.Notes, 2000));
        }

        private string ReadSqlWithTopAndWhere(string resourcePath, int maxRows, string whereClause)
        {
            return _sqlResources
                .Read(resourcePath)
                .Replace("/*TOP_ROWS*/", maxRows.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("/*WHERE_CLAUSE*/", whereClause, StringComparison.Ordinal);
        }

        private static TransactionLinkRecord MapLink(SqlDataReader reader)
        {
            return new TransactionLinkRecord
            {
                TransactionLinkId = reader.GetInt64Value("TransactionLinkId"),
                SourceTransactionId = reader.GetInt64Value("SourceTransactionId"),
                SourceAccountId = reader.GetInt32Value("SourceAccountId"),
                SourceAccountNickname = reader.GetStringOrEmpty("SourceAccountNickname"),
                SourceDate = reader.GetDateOnlyOrNull("SourceDate") ?? DateOnly.MinValue,
                SourceAmount = reader.GetDecimalValue("SourceAmount"),
                SourceType = reader.GetStringOrEmpty("SourceType"),
                SourceDescription = reader.GetStringOrEmpty("SourceDescription"),
                SourceCategory = reader.GetStringOrNull("SourceCategory"),
                SourceCategoryType = reader.GetStringOrNull("SourceCategoryType"),
                SourceFundingSource = reader.GetStringOrNull("SourceFundingSource"),
                SourceExternalTransactionId = reader.GetStringOrNull("SourceExternalTransactionId"),
                LinkedTransactionId = reader.GetInt64Value("LinkedTransactionId"),
                LinkedAccountId = reader.GetInt32Value("LinkedAccountId"),
                LinkedAccountNickname = reader.GetStringOrEmpty("LinkedAccountNickname"),
                LinkedDate = reader.GetDateOnlyOrNull("LinkedDate") ?? DateOnly.MinValue,
                LinkedAmount = reader.GetDecimalValue("LinkedAmount"),
                LinkedType = reader.GetStringOrEmpty("LinkedType"),
                LinkedDescription = reader.GetStringOrEmpty("LinkedDescription"),
                LinkedCategory = reader.GetStringOrNull("LinkedCategory"),
                LinkedCategoryType = reader.GetStringOrNull("LinkedCategoryType"),
                LinkedFundingSource = reader.GetStringOrNull("LinkedFundingSource"),
                LinkedExternalTransactionId = reader.GetStringOrNull("LinkedExternalTransactionId"),
                LinkType = reader.GetStringOrEmpty("LinkType"),
                AllocationAmount = reader.GetDecimalOrNull("AllocationAmount"),
                ConfidenceScore = reader.GetInt32Value("ConfidenceScore"),
                Notes = reader.GetStringOrNull("Notes"),
                CreatedAtUtc = reader.GetDateTimeValue("CreatedAtUtc"),
                UpdatedAtUtc = reader.GetDateTimeOrNull("UpdatedAtUtc")
            };
        }
    }
}
