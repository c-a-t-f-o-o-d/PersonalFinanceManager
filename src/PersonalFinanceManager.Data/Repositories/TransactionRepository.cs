using System.Security.Cryptography;
using System.Text;
using PersonalFinanceManager.Core.Common;
using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Core.Rules;
using PersonalFinanceManager.Data.Sql;
using Microsoft.Data.SqlClient;

namespace PersonalFinanceManager.Data.Repositories
{
    public sealed class TransactionRepository
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly SqlResourceReader _sqlResources;

        public TransactionRepository(SqlConnectionFactory connectionFactory, SqlResourceReader? sqlResources = null)
        {
            _connectionFactory = connectionFactory;
            _sqlResources = sqlResources ?? new SqlResourceReader();
        }

        public async Task<IReadOnlyList<ImportedTransaction>> GetAsync(TransactionFilter filter, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<string> clauses = new();
            List<SqlParameter> parameters = new();

            if (filter.AccountId.HasValue)
            {
                clauses.Add("t.AccountId = @AccountId");
                parameters.Add(SqlParameters.Int("@AccountId", filter.AccountId.Value));
            }

            if (filter.FromDate.HasValue)
            {
                clauses.Add("t.TransactionDate >= @FromDate");
                parameters.Add(SqlParameters.Date("@FromDate", filter.FromDate.Value));
            }

            if (filter.ToDate.HasValue)
            {
                clauses.Add("t.TransactionDate <= @ToDate");
                parameters.Add(SqlParameters.Date("@ToDate", filter.ToDate.Value));
            }

            if (!string.IsNullOrWhiteSpace(filter.SearchText))
            {
                clauses.Add("(t.[Description] LIKE @SearchText OR t.Merchant LIKE @SearchText OR t.Category LIKE @SearchText OR t.SpendingClass LIKE @SearchText OR t.[Type] LIKE @SearchText)");
                parameters.Add(SqlParameters.NVarChar("@SearchText", "%" + filter.SearchText.Trim() + "%", 1000));
            }

            if (!string.IsNullOrWhiteSpace(filter.Type))
            {
                clauses.Add("t.[Type] = @Type");
                parameters.Add(SqlParameters.NVarChar("@Type", filter.Type.Trim(), 100));
            }

            if (!string.IsNullOrWhiteSpace(filter.Category))
            {
                clauses.Add("t.Category = @Category");
                parameters.Add(SqlParameters.NVarChar("@Category", filter.Category.Trim(), 200));
            }

            if (!string.IsNullOrWhiteSpace(filter.SpendingClass))
            {
                clauses.Add("t.SpendingClass = @SpendingClass");
                parameters.Add(SqlParameters.NVarChar("@SpendingClass", filter.SpendingClass.Trim(), 100));
            }

            if (filter.UncategorizedOnly)
            {
                clauses.Add("((t.Category IS NULL OR LTRIM(RTRIM(t.Category)) = '') OR (t.SpendingClass IS NULL OR LTRIM(RTRIM(t.SpendingClass)) = ''))");
            }

            if (filter.IsIncome.HasValue)
            {
                clauses.Add("t.IsIncome = @IsIncome");
                parameters.Add(SqlParameters.Bit("@IsIncome", filter.IsIncome.Value));
            }

            if (filter.IsTransfer.HasValue)
            {
                clauses.Add("t.IsTransfer = @IsTransfer");
                parameters.Add(SqlParameters.Bit("@IsTransfer", filter.IsTransfer.Value));
            }

            if (filter.IsManual.HasValue)
            {
                clauses.Add("t.IsManual = @IsManual");
                parameters.Add(SqlParameters.Bit("@IsManual", filter.IsManual.Value));
            }

            if (!string.IsNullOrWhiteSpace(filter.IncomeOwner))
            {
                clauses.Add("ISNULL(NULLIF(LTRIM(RTRIM(t.IncomeOwner)), N''), N'Unassigned') = @IncomeOwner");
                parameters.Add(SqlParameters.NVarChar("@IncomeOwner", filter.IncomeOwner.Trim(), 50));
            }

            int maxRows = Math.Clamp(filter.MaxRows <= 0 ? 1000 : filter.MaxRows, 1, 10000);
            string where = clauses.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", clauses);

            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();

            command.CommandText = ReadSql("Transactions/GetTransactions.sql")
                .Replace("/*MAX_ROWS*/", maxRows.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("/*WHERE_CLAUSE*/", where, StringComparison.Ordinal);

            foreach (SqlParameter parameter in parameters)
            {
                command.Parameters.Add(parameter);
            }

            List<ImportedTransaction> transactions = new();
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                transactions.Add(Map(reader));
            }

            return transactions;
        }

        public async Task<IReadOnlyList<string>> GetCategoryValuesAsync(CancellationToken cancellationToken = default)
        {
            return await GetDistinctTextValuesAsync("Category", cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<string>> GetCategoryTypeValuesAsync(CancellationToken cancellationToken = default)
        {
            return await GetDistinctTextValuesAsync("SpendingClass", cancellationToken).ConfigureAwait(false);
        }

        public async Task<ImportedTransaction?> GetByIdAsync(long importedTransactionId, CancellationToken cancellationToken = default)
        {
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();

            command.CommandText = ReadSql("Transactions/GetTransactionById.sql");

            command.Parameters.Add(SqlParameters.BigInt("@ImportedTransactionId", importedTransactionId));
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? Map(reader) : null;
        }

        public async Task<long> SaveAsync(ImportedTransaction transaction, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            Validate(transaction);
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            if (transaction.ImportedTransactionId == 0)
            {
                return await InsertAsync(connection, transaction, _sqlResources, cancellationToken).ConfigureAwait(false);
            }

            await UpdateAsync(connection, transaction, _sqlResources, cancellationToken).ConfigureAwait(false);
            return transaction.ImportedTransactionId;
        }

        public async Task<int> UpdateCategorizationAsync(IEnumerable<long> importedTransactionIds, string? category, string? categoryType, CancellationToken cancellationToken = default)
        {
            List<long> ids = importedTransactionIds.Distinct().Where(id => id > 0).Take(1000).ToList();

            if (ids.Count == 0)
            {
                return 0;
            }

            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();
            List<string> idParameters = new();

            for (int index = 0; index < ids.Count; index++)
            {
                string name = "@Id" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                idParameters.Add(name);
                command.Parameters.Add(SqlParameters.BigInt(name, ids[index]));
            }

            command.CommandText = ReadSql("Transactions/UpdateCategorizationByIds.sql")
                .Replace("/*ID_LIST*/", string.Join(", ", idParameters), StringComparison.Ordinal);

            command.Parameters.Add(SqlParameters.NVarChar("@Category", category, 200));
            command.Parameters.Add(SqlParameters.NVarChar("@SpendingClass", categoryType, 100));
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<int> ApplyRuleAsync(FinancialRule rule, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(rule);

            if (string.IsNullOrWhiteSpace(rule.MatchDescription))
            {
                throw new InvalidOperationException("A rule must include description matching text before it can be applied.");
            }

            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();
            string descriptionClause = BuildTextMatchClause("t.[Description]", "@MatchDescription", "@MatchDescriptionPattern", rule.MatchDescription, rule.DescriptionMatchMode, command.Parameters);
            string typeClause = string.Empty;

            if (!string.IsNullOrWhiteSpace(rule.MatchType))
            {
                typeClause = " AND " + BuildTypeMatchClause(rule.MatchType, rule.TypeMatchMode, command.Parameters);
            }

            command.CommandText = ReadSql("Transactions/ApplyRule.sql")
                .Replace("/*DESCRIPTION_CLAUSE*/", descriptionClause, StringComparison.Ordinal)
                .Replace("/*TYPE_CLAUSE*/", typeClause, StringComparison.Ordinal);

            command.Parameters.Add(SqlParameters.Int("@RuleId", rule.RuleId == 0 ? null : rule.RuleId));
            command.Parameters.Add(SqlParameters.NVarChar("@Category", rule.Category, 200));
            command.Parameters.Add(SqlParameters.NVarChar("@SpendingClass", rule.SpendingClass, 100));
            command.Parameters.Add(SqlParameters.NVarChar("@Merchant", rule.Merchant, 200));
            command.Parameters.Add(SqlParameters.Bit("@IsIncome", rule.IsIncome));
            command.Parameters.Add(SqlParameters.Bit("@IsTransfer", rule.IsTransfer));
            command.Parameters.Add(SqlParameters.NVarChar("@IncomeOwner", OwnerValues.Normalize(rule.IncomeOwner), 50));
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task DeleteAsync(long importedTransactionId, CancellationToken cancellationToken = default)
        {
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
            try
            {
                await using SqlCommand command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = ReadSql("Transactions/DeleteTransaction.sql");
                command.Parameters.Add(SqlParameters.BigInt("@ImportedTransactionId", importedTransactionId));
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }

        private string ReadSql(string relativePath)
        {
            return _sqlResources.Read(relativePath);
        }

        private async Task<IReadOnlyList<string>> GetDistinctTextValuesAsync(string columnName, CancellationToken cancellationToken)
        {
            if (!string.Equals(columnName, "Category", StringComparison.Ordinal) && !string.Equals(columnName, "SpendingClass", StringComparison.Ordinal))
            {
                throw new ArgumentOutOfRangeException(nameof(columnName));
            }

            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();

            command.CommandText = ReadSql("Transactions/GetDistinctTextValues.sql")
                .Replace("/*COLUMN_NAME*/", columnName, StringComparison.Ordinal);

            List<string> values = new();
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                values.Add(reader.GetString(0));
            }

            return values;
        }

        private static async Task<long> InsertAsync(SqlConnection connection, ImportedTransaction transaction, SqlResourceReader sqlResources, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();

            command.CommandText = sqlResources.Read("Transactions/InsertTransaction.sql");

            AddSaveParameters(command, transaction);
            object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt64(value);
        }

        private static async Task UpdateAsync(SqlConnection connection, ImportedTransaction transaction, SqlResourceReader sqlResources, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();

            command.CommandText = sqlResources.Read("Transactions/UpdateTransaction.sql");

            AddSaveParameters(command, transaction);
            command.Parameters.Add(SqlParameters.BigInt("@ImportedTransactionId", transaction.ImportedTransactionId));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static void AddSaveParameters(SqlCommand command, ImportedTransaction transaction)
        {
            command.Parameters.Add(SqlParameters.Int("@AccountId", transaction.AccountId));
            command.Parameters.Add(SqlParameters.BigInt("@ImportBatchId", transaction.ImportBatchId));
            command.Parameters.Add(SqlParameters.Date("@TransactionDate", transaction.TransactionDate));
            command.Parameters.Add(SqlParameters.Time("@TransactionTime", transaction.TransactionTime));
            command.Parameters.Add(SqlParameters.Decimal("@Amount", transaction.Amount));
            command.Parameters.Add(SqlParameters.NVarChar("@Type", transaction.Type.Trim(), 100));
            command.Parameters.Add(SqlParameters.NVarChar("@Description", transaction.Description.Trim(), 1000));
            command.Parameters.Add(SqlParameters.Int("@AppliedRuleId", transaction.AppliedRuleId));
            command.Parameters.Add(SqlParameters.NVarChar("@Category", transaction.Category, 200));
            command.Parameters.Add(SqlParameters.NVarChar("@SpendingClass", transaction.SpendingClass, 100));
            command.Parameters.Add(SqlParameters.NVarChar("@Merchant", transaction.Merchant, 200));
            command.Parameters.Add(SqlParameters.Bit("@IsIncome", transaction.IsIncome));
            command.Parameters.Add(SqlParameters.Bit("@IsTransfer", transaction.IsTransfer));
            command.Parameters.Add(SqlParameters.Bit("@IsManual", transaction.IsManual));
            command.Parameters.Add(SqlParameters.Bit("@ClassificationIsManual", transaction.ClassificationIsManual));
            command.Parameters.Add(SqlParameters.NVarChar("@IncomeOwner", OwnerValues.Normalize(transaction.IncomeOwner), 50));
            command.Parameters.Add(SqlParameters.NVarChar("@Notes", transaction.Notes, 1000));
            command.Parameters.Add(SqlParameters.NVarChar("@RawRowHash", BuildRowHash(transaction), 64));
        }


        private static string BuildTypeMatchClause(string value, string? mode, SqlParameterCollection parameters)
        {
            string? normalizedType = RuleTransactionTypeNormalizer.NormalizeRuleType(value);

            if (string.Equals(normalizedType, RuleTransactionTypeNormalizer.Withdrawal, StringComparison.OrdinalIgnoreCase))
            {
                return "t.Amount < 0";
            }

            if (string.Equals(normalizedType, RuleTransactionTypeNormalizer.Deposit, StringComparison.OrdinalIgnoreCase))
            {
                return "t.Amount > 0";
            }

            return BuildTextMatchClause("t.[Type]", "@MatchType", "@MatchTypePattern", value, mode, parameters);
        }

        private static string BuildTextMatchClause(string columnExpression, string exactParameterName, string patternParameterName, string value, string? mode, SqlParameterCollection parameters)
        {
            string trimmedValue = value.Trim();
            string normalizedMode = string.IsNullOrWhiteSpace(mode) ? RuleMatchModes.Exact : mode.Trim();

            if (string.Equals(normalizedMode, RuleMatchModes.Contains, StringComparison.OrdinalIgnoreCase))
            {
                parameters.Add(SqlParameters.NVarChar(patternParameterName, "%" + EscapeLike(trimmedValue) + "%", 1000));
                return $"{columnExpression} LIKE {patternParameterName} ESCAPE '\\'";
            }

            if (string.Equals(normalizedMode, RuleMatchModes.StartsWith, StringComparison.OrdinalIgnoreCase))
            {
                parameters.Add(SqlParameters.NVarChar(patternParameterName, EscapeLike(trimmedValue) + "%", 1000));
                return $"{columnExpression} LIKE {patternParameterName} ESCAPE '\\'";
            }

            parameters.Add(SqlParameters.NVarChar(exactParameterName, trimmedValue, 1000));
            return $"LTRIM(RTRIM({columnExpression})) = {exactParameterName}";
        }

        private static string EscapeLike(string value)
        {
            return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal).Replace("[", "\\[", StringComparison.Ordinal);
        }

        private static void Validate(ImportedTransaction transaction)
        {
            if (transaction.AccountId <= 0)
            {
                throw new InvalidOperationException("A transaction must be assigned to an account.");
            }

            if (transaction.Amount < -9999999999999999.99m || transaction.Amount > 9999999999999999.99m || decimal.Round(transaction.Amount, 2) != transaction.Amount)
                throw new InvalidOperationException("Transaction amounts must fit within 16 whole-number digits and two decimal places.");

            if (transaction.TransactionDate == default)
            {
                throw new InvalidOperationException("A transaction date is required.");
            }

            if (string.IsNullOrWhiteSpace(transaction.Type))
            {
                throw new InvalidOperationException("A transaction type is required.");
            }

            if (string.IsNullOrWhiteSpace(transaction.Description))
            {
                throw new InvalidOperationException("A transaction description is required.");
            }
        }

        private static string BuildRowHash(ImportedTransaction transaction)
        {
            string text = string.Join("|", transaction.AccountId, transaction.TransactionDate, transaction.TransactionTime, transaction.Amount, TextNormalizer.NormalizeForComparison(transaction.Type), TextNormalizer.NormalizeForComparison(transaction.Description));
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
            return Convert.ToHexString(hash);
        }

        private static ImportedTransaction Map(SqlDataReader reader)
        {
            return new ImportedTransaction
            {
                ImportedTransactionId = reader.GetInt64Value("ImportedTransactionId"),
                AccountId = reader.GetInt32Value("AccountId"),
                AccountNickname = reader.GetStringOrNull("AccountNickname"),
                ImportBatchId = reader.GetInt64OrNull("ImportBatchId"),
                TransactionDate = reader.GetDateOnlyOrNull("TransactionDate") ?? DateOnly.MinValue,
                TransactionTime = reader.GetTimeOnlyOrNull("TransactionTime"),
                Amount = reader.GetDecimalValue("Amount"),
                Type = reader.GetStringOrEmpty("Type"),
                Description = reader.GetStringOrEmpty("Description"),
                AppliedRuleId = reader.GetInt32OrNull("AppliedRuleId"),
                Category = reader.GetStringOrNull("Category"),
                SpendingClass = reader.GetStringOrNull("SpendingClass"),
                Merchant = reader.GetStringOrNull("Merchant"),
                IsIncome = reader.GetBooleanValue("IsIncome"),
                IsTransfer = reader.GetBooleanValue("IsTransfer"),
                IsManual = reader.GetBooleanValue("IsManual"),
                ClassificationIsManual = reader.GetBooleanValue("ClassificationIsManual"),
                LegacyLinkTreatment = reader.GetBooleanValue("LegacyLinkTreatment"),
                IncomeOwner = reader.GetStringOrNull("IncomeOwner"),
                Notes = reader.GetStringOrNull("Notes"),
                CreatedAtUtc = reader.GetDateTimeValue("CreatedAtUtc"),
                UpdatedAtUtc = reader.GetDateTimeOrNull("UpdatedAtUtc")
            };
        }
    }
}
