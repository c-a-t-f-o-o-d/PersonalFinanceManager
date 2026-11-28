using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Core.Rules;
using PersonalFinanceManager.Data.Sql;
using Microsoft.Data.SqlClient;

namespace PersonalFinanceManager.Data.Repositories
{
    public sealed class RuleRepository
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly SqlResourceReader _sqlResources = new();

        public RuleRepository(SqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IReadOnlyList<FinancialRule>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            List<FinancialRule> rules = new();
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = _sqlResources.Read("Rules/GetRules.sql");
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rules.Add(Map(reader));
            }

            return rules;
        }

        public async Task<IReadOnlyList<FinancialRule>> GetActiveAsync(CancellationToken cancellationToken = default)
        {
            List<FinancialRule> rules = new();
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = _sqlResources.Read("Rules/GetActiveRules.sql");
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rules.Add(Map(reader));
            }

            return rules;
        }

        public Task<IReadOnlyList<string>> GetCategoryValuesAsync(CancellationToken cancellationToken = default)
        {
            return new TransactionRepository(_connectionFactory).GetCategoryValuesAsync(cancellationToken);
        }

        public Task<IReadOnlyList<string>> GetCategoryTypeValuesAsync(CancellationToken cancellationToken = default)
        {
            return new TransactionRepository(_connectionFactory).GetCategoryTypeValuesAsync(cancellationToken);
        }

        public async Task<int> SaveAsync(FinancialRule rule, CancellationToken cancellationToken = default)
        {
            if (rule.RuleId == 0)
            {
                return await InsertAsync(rule, cancellationToken).ConfigureAwait(false);
            }

            await UpdateAsync(rule, cancellationToken).ConfigureAwait(false);
            return rule.RuleId;
        }

        public async Task DeleteAsync(int ruleId, CancellationToken cancellationToken = default)
        {
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = _sqlResources.Read("Rules/DeleteRule.sql");
            command.Parameters.Add(SqlParameters.Int("@RuleId", ruleId));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task ReplaceWithWorkbookRowsAsync(IEnumerable<RuleImportRow> rows, CancellationToken cancellationToken = default)
        {
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await using (SqlCommand delete = connection.CreateCommand())
                {
                    delete.Transaction = transaction;
                    delete.CommandText = _sqlResources.Read("Rules/DeleteAllRules.sql");
                    await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                foreach (RuleImportRow row in rows)
                {
                    await using SqlCommand insert = connection.CreateCommand();
                    insert.Transaction = transaction;
                    insert.CommandText = _sqlResources.Read("Rules/InsertRule.sql");

                    AddParameters(insert, new FinancialRule
                    {
                        RuleName = row.RuleName,
                        Priority = row.Priority,
                        MatchDescription = row.Description,
                        MatchType = row.Type,
                        DescriptionMatchMode = row.DescriptionMatchMode,
                        TypeMatchMode = row.TypeMatchMode,
                        Category = row.Category,
                        SpendingClass = row.SpendingClass,
                        Merchant = row.Merchant,
                        IsIncome = row.IsIncome,
                        IsTransfer = row.IsTransfer,
                        IncomeOwner = null,
                        IsActive = row.Active,
                        Notes = row.Notes
                    });

                    await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }

        private async Task<int> InsertAsync(FinancialRule rule, CancellationToken cancellationToken)
        {
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = _sqlResources.Read("Rules/InsertRule.sql");
            AddParameters(command, rule);
            object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt32(value);
        }

        private async Task UpdateAsync(FinancialRule rule, CancellationToken cancellationToken)
        {
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = _sqlResources.Read("Rules/UpdateRule.sql");
            AddParameters(command, rule);
            command.Parameters.Add(SqlParameters.Int("@RuleId", rule.RuleId));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static void AddParameters(SqlCommand command, FinancialRule rule)
        {
            command.Parameters.Add(SqlParameters.NVarChar("@RuleName", rule.RuleName, 200));
            command.Parameters.Add(SqlParameters.Int("@Priority", rule.Priority));
            command.Parameters.Add(SqlParameters.NVarChar("@MatchDescription", rule.MatchDescription, 1000));
            string? normalizedMatchType = RuleTransactionTypeNormalizer.NormalizeRuleType(rule.MatchType);
            string typeMatchMode = RuleTransactionTypeNormalizer.IsNormalizedDirection(normalizedMatchType) ? RuleMatchModes.Exact : rule.TypeMatchMode;
            command.Parameters.Add(SqlParameters.NVarChar("@MatchType", normalizedMatchType, 100));
            command.Parameters.Add(SqlParameters.NVarChar("@DescriptionMatchMode", rule.DescriptionMatchMode, 30));
            command.Parameters.Add(SqlParameters.NVarChar("@TypeMatchMode", typeMatchMode, 30));
            command.Parameters.Add(SqlParameters.NVarChar("@Category", rule.Category, 200));
            command.Parameters.Add(SqlParameters.NVarChar("@SpendingClass", rule.SpendingClass, 100));
            command.Parameters.Add(SqlParameters.NVarChar("@Merchant", rule.Merchant, 200));
            command.Parameters.Add(SqlParameters.Bit("@IsIncome", rule.IsIncome));
            command.Parameters.Add(SqlParameters.Bit("@IsTransfer", rule.IsTransfer));
            command.Parameters.Add(SqlParameters.NVarChar("@IncomeOwner", OwnerValues.Normalize(rule.IncomeOwner), 50));
            command.Parameters.Add(SqlParameters.Bit("@IsActive", rule.IsActive));
            command.Parameters.Add(SqlParameters.NVarChar("@Notes", rule.Notes, 1000));
        }

        private static FinancialRule Map(SqlDataReader reader)
        {
            return new FinancialRule
            {
                RuleId = reader.GetInt32Value("RuleId"),
                RuleName = reader.GetStringOrEmpty("RuleName"),
                Priority = reader.GetInt32Value("Priority"),
                MatchDescription = reader.GetStringOrEmpty("MatchDescription"),
                MatchType = reader.GetStringOrNull("MatchType"),
                DescriptionMatchMode = reader.GetStringOrEmpty("DescriptionMatchMode"),
                TypeMatchMode = reader.GetStringOrEmpty("TypeMatchMode"),
                Category = reader.GetStringOrNull("Category"),
                SpendingClass = reader.GetStringOrNull("SpendingClass"),
                Merchant = reader.GetStringOrNull("Merchant"),
                IsIncome = reader.GetBooleanOrNull("IsIncome"),
                IsTransfer = reader.GetBooleanOrNull("IsTransfer"),
                IncomeOwner = reader.GetStringOrNull("IncomeOwner"),
                IsActive = reader.GetBooleanValue("IsActive"),
                Notes = reader.GetStringOrNull("Notes"),
                StarterRuleCode = reader.GetStringOrNull("StarterRuleCode"),
                CreatedAtUtc = reader.GetDateTimeValue("CreatedAtUtc"),
                UpdatedAtUtc = reader.GetDateTimeValue("UpdatedAtUtc")
            };
        }
    }
}
