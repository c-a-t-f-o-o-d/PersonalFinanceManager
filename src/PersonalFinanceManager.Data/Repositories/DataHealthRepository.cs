using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Data.Sql;
using Microsoft.Data.SqlClient;

namespace PersonalFinanceManager.Data.Repositories
{
    public sealed class DataHealthRepository
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly SqlResourceReader _sqlResources;

        public DataHealthRepository(SqlConnectionFactory connectionFactory, SqlResourceReader? sqlResources = null)
        {
            _connectionFactory = connectionFactory;
            _sqlResources = sqlResources ?? new SqlResourceReader();
        }

        public async Task<IReadOnlyList<DataHealthCheckResult>> GetChecksAsync(CancellationToken cancellationToken = default)
        {
            List<DataHealthCheckResult> checks = new();
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            foreach (DataHealthCheckDefinition definition in GetCheckDefinitions())
            {
                checks.Add(await CountAsync(connection, definition, cancellationToken).ConfigureAwait(false));
            }

            return checks;
        }

        public async Task<int> SyncTransactionOwnersFromAccountsAsync(CancellationToken cancellationToken = default)
        {
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();

            command.CommandText = _sqlResources.Read("DataHealth/SyncTransactionOwnersFromAccounts.sql");

            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<DataHealthDetailRow>> GetDetailsAsync(string issueCode, CancellationToken cancellationToken = default)
        {
            string? resourcePath = GetDetailResourcePath(issueCode);

            if (resourcePath is null)
            {
                return Array.Empty<DataHealthDetailRow>();
            }

            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            return await QueryAsync(connection, _sqlResources.Read(resourcePath), cancellationToken).ConfigureAwait(false);
        }

        private static IReadOnlyList<DataHealthCheckDefinition> GetCheckDefinitions()
        {
            return new[]
            {
                new DataHealthCheckDefinition(
                    DataHealthIssueCodes.LegacyLinkReview,
                    "Reconciliation", "Warning", "Earlier transaction matches require confirmation",
                    "Open Transaction Links and filter ManualReview. Verify the two statements and restore the appropriate relationship or delete the match. Unconfirmed entries remain visible in spending; reconcile them before relying on totals.",
                    "DataHealth/Counts/LegacyLinkReview.sql"),
                new DataHealthCheckDefinition(
                    DataHealthIssueCodes.BalanceSnapshotDate,
                    "Accounts", "Warning", "Saved balances need a current statement date",
                    "Enter the balance as-of date and refresh snapshots older than 31 days. Reconcile statement activity; runway uses saved balances and does not advance them automatically from imports.",
                    "DataHealth/Counts/BalanceSnapshotDate.sql"),
                new DataHealthCheckDefinition(
                    DataHealthIssueCodes.LegacyLinkTreatment,
                    "Reconciliation", "Warning", "Legacy generated transfer classifications without a current link",
                    "Review the original statement and recategorize these rows. Older versions overwrote their categories; the original choices cannot be recovered automatically.",
                    "DataHealth/Counts/LegacyLinkTreatment.sql"),
                new DataHealthCheckDefinition(
                    DataHealthIssueCodes.IncomeMissingOwner,
                    "Income",
                    "Warning",
                    "Income transactions without owner",
                    "Classify each income row as Me, Spouse, Joint, or Household. Use Sync Owners From Accounts when account ownership is correct.",
                    "DataHealth/Counts/IncomeMissingOwner.sql"),
                new DataHealthCheckDefinition(
                    DataHealthIssueCodes.MissingCategorization,
                    "Categorization",
                    "Warning",
                    "Transactions missing category or category type",
                    "Use Transactions, Rules, or quick categorization to finish these before relying on reports.",
                    "DataHealth/Counts/MissingCategorization.sql"),
                new DataHealthCheckDefinition(
                    DataHealthIssueCodes.SameAccountDuplicates,
                    "Duplicates",
                    "Warning",
                    "Exact same-account duplicate groups",
                    "Open Duplicate Review and import history if this is non-zero. Memo is included, so rows with different non-empty memo text are no longer counted as exact duplicates.",
                    "DataHealth/Counts/SameAccountDuplicates.sql"),
                new DataHealthCheckDefinition(
                    DataHealthIssueCodes.CrossAccountDuplicates,
                    "Duplicates",
                    "Warning",
                    "Same transaction under different accounts",
                    "Use Duplicate Review > Scan Cross-Account to verify whether the account was selected incorrectly during import. Memo differences are considered when memo is present.",
                    "DataHealth/Counts/CrossAccountDuplicates.sql"),
                new DataHealthCheckDefinition(
                    DataHealthIssueCodes.UnlinkedTransfers,
                    "Transfers",
                    "Info",
                    "Transfer-looking rows without transaction links",
                    "This may be normal, but linked transfers produce more reliable Expense Analysis and Money In/Out numbers.",
                    "DataHealth/Counts/UnlinkedTransfers.sql"),
                new DataHealthCheckDefinition(
                    DataHealthIssueCodes.MissingBalances,
                    "Accounts",
                    "Warning",
                    "Active accounts missing balance",
                    "Enter a current balance for cash accounts. For credit cards, loans, and mortgages, either current balance or principal balance is accepted.",
                    "DataHealth/Counts/MissingBalances.sql"),
                new DataHealthCheckDefinition(
                    DataHealthIssueCodes.MissingInterestRates,
                    "Accounts",
                    "Info",
                    "Active savings or credit accounts missing interest/APR",
                    "Interest rates improve Salary Needed, Runway, and savings/interest analysis.",
                    "DataHealth/Counts/MissingInterestRates.sql"),
                new DataHealthCheckDefinition(
                    DataHealthIssueCodes.RequiredBillsMissingAmount,
                    "Bills",
                    "Warning",
                    "Active required bills missing amount",
                    "Salary Needed and Runway need an amount for required bills.",
                    "DataHealth/Counts/RequiredBillsMissingAmount.sql")
            };
        }

        private static string? GetDetailResourcePath(string issueCode)
        {
            return issueCode switch
            {
                DataHealthIssueCodes.LegacyLinkReview => "DataHealth/Details/LegacyLinkReview.sql",
                DataHealthIssueCodes.BalanceSnapshotDate => "DataHealth/Details/BalanceSnapshotDate.sql",
                DataHealthIssueCodes.LegacyLinkTreatment => "DataHealth/Details/LegacyLinkTreatment.sql",
                DataHealthIssueCodes.IncomeMissingOwner => "DataHealth/Details/IncomeMissingOwner.sql",
                DataHealthIssueCodes.MissingCategorization => "DataHealth/Details/MissingCategorization.sql",
                DataHealthIssueCodes.SameAccountDuplicates => "DataHealth/Details/SameAccountDuplicates.sql",
                DataHealthIssueCodes.CrossAccountDuplicates => "DataHealth/Details/CrossAccountDuplicates.sql",
                DataHealthIssueCodes.UnlinkedTransfers => "DataHealth/Details/UnlinkedTransfers.sql",
                DataHealthIssueCodes.MissingBalances => "DataHealth/Details/MissingBalances.sql",
                DataHealthIssueCodes.MissingInterestRates => "DataHealth/Details/MissingInterestRates.sql",
                DataHealthIssueCodes.RequiredBillsMissingAmount => "DataHealth/Details/RequiredBillsMissingAmount.sql",
                _ => null
            };
        }

        private static async Task<IReadOnlyList<DataHealthDetailRow>> QueryAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = sql;
            List<DataHealthDetailRow> rows = new();
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new DataHealthDetailRow
                {
                    GroupKey = reader.GetStringOrEmpty("GroupKey"),
                    Source = reader.GetStringOrEmpty("Source"),
                    TransactionId = reader.GetInt64OrNull("TransactionId"),
                    AccountId = reader.GetInt32OrNull("AccountId"),
                    Account = reader.GetStringOrNull("Account"),
                    AccountType = reader.GetStringOrNull("AccountType"),
                    AccountOwner = reader.GetStringOrNull("AccountOwner"),
                    Date = reader.GetDateOnlyOrNull("Date"),
                    Amount = reader.GetDecimalOrNull("Amount"),
                    Type = reader.GetStringOrNull("Type"),
                    Description = reader.GetStringOrNull("Description"),
                    Category = reader.GetStringOrNull("Category"),
                    CategoryType = reader.GetStringOrNull("CategoryType"),
                    IncomeOwner = reader.GetStringOrNull("IncomeOwner"),
                    Memo = reader.GetStringOrNull("Memo"),
                    Details = reader.GetStringOrNull("Details")
                });
            }

            return rows;
        }

        private async Task<DataHealthCheckResult> CountAsync(SqlConnection connection, DataHealthCheckDefinition definition, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = _sqlResources.Read(definition.CountSqlResourcePath);
            object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

            return new DataHealthCheckResult
            {
                IssueCode = definition.IssueCode,
                Area = definition.Area,
                Severity = definition.Severity,
                Issue = definition.Issue,
                Count = Convert.ToInt32(value),
                Recommendation = definition.Recommendation
            };
        }

        private sealed record DataHealthCheckDefinition(
            string IssueCode,
            string Area,
            string Severity,
            string Issue,
            string Recommendation,
            string CountSqlResourcePath);
    }
}
