using System.Data;
using Microsoft.Data.SqlClient;
using PersonalFinanceManager.Core.Imports;
using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Data.Sql;

namespace PersonalFinanceManager.Data.Repositories
{
    public sealed class AccountReconciliationRepository
    {
        private readonly SqlConnectionFactory _connections;
        private readonly SqlResourceReader _sql = new();

        public AccountReconciliationRepository(SqlConnectionFactory connections)
        {
            _connections = connections;
        }

        public async Task<AccountReconciliation> CheckAsync(AccountReconciliationRequest request, bool record, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.FromDate > request.ToDate || request.AccountId <= 0)
            {
                throw new ArgumentException("Select an account and a valid statement date range.");
            }

            await using SqlConnection connection = _connections.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
            string accountType;
            await using (SqlCommand account = connection.CreateCommand())
            {
                account.Transaction = transaction;
                account.CommandText = _sql.Read("Accounts/GetAccountById.sql");
                account.Parameters.Add(SqlParameters.Int("@AccountId", request.AccountId));
                await using SqlDataReader reader = await account.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    throw new InvalidOperationException("The selected account no longer exists.");
                }

                accountType = reader.GetStringOrEmpty("AccountType");
            }

            List<ImportedTransaction> activity = new();
            bool hasWalletPurchaseActivity = false;
            await using (SqlCommand select = connection.CreateCommand())
            {
                select.Transaction = transaction;
                select.CommandText = _sql.Read("Accounts/GetStatementActivity.sql");
                select.Parameters.Add(SqlParameters.Int("@AccountId", request.AccountId));
                select.Parameters.Add(SqlParameters.Date("@FromDate", request.FromDate));
                select.Parameters.Add(SqlParameters.Date("@ToDate", request.ToDate));
                await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    hasWalletPurchaseActivity |= string.Equals(reader.GetStringOrNull("ImportProfileCode"), ImportProfileCodes.PayPalStatement, StringComparison.OrdinalIgnoreCase);
                    activity.Add(new ImportedTransaction
                    {
                        ImportedTransactionId = reader.GetInt64Value("ImportedTransactionId"),
                        AccountId = reader.GetInt32Value("AccountId"),
                        TransactionDate = reader.GetDateOnlyOrNull("TransactionDate") ?? DateOnly.MinValue,
                        PostingDate = reader.GetDateOnlyOrNull("PostingDate"),
                        Amount = reader.GetDecimalValue("Amount")
                    });
                }
            }

            if (AccountClassification.IsWallet(accountType) && hasWalletPurchaseActivity)
            {
                throw new InvalidOperationException("This period contains PayPal account-activity imports, which may omit wallet funding credits and other balance movements. A wallet balance cannot be verified from that purchase activity. Reconcile the funding bank or credit account, and use a complete normalized wallet balance ledger in a separate account if a wallet balance check is required.");
            }

            AccountReconciliation result = AccountReconciliationCalculator.Calculate(request, accountType, activity);
            if (record)
            {
                await using SqlCommand insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = _sql.Read("Accounts/InsertReconciliation.sql");
                insert.Parameters.Add(SqlParameters.Int("@AccountId", result.AccountId));
                insert.Parameters.Add(SqlParameters.Date("@FromDate", result.FromDate));
                insert.Parameters.Add(SqlParameters.Date("@ToDate", result.ToDate));
                insert.Parameters.Add(SqlParameters.Decimal("@OpeningBalance", result.OpeningBalance));
                insert.Parameters.Add(SqlParameters.Decimal("@ClosingBalance", result.ClosingBalance));
                insert.Parameters.Add(SqlParameters.Decimal("@ExpectedClosingBalance", result.ExpectedClosingBalance));
                insert.Parameters.Add(SqlParameters.Decimal("@NetActivity", result.NetActivity));
                insert.Parameters.Add(SqlParameters.Int("@TransactionCount", result.TransactionCount));
                insert.Parameters.Add(SqlParameters.Int("@DateFallbackCount", result.DateFallbackCount));
                insert.Parameters.Add(SqlParameters.Bit("@UsesLiabilityBalances", result.UsesLiabilityBalances));
                result.AccountReconciliationId = Convert.ToInt64(await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
                result.RecordedAtUtc = DateTime.UtcNow;
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }

        public async Task<IReadOnlyList<AccountReconciliation>> GetHistoryAsync(int accountId, CancellationToken cancellationToken = default)
        {
            await using SqlConnection connection = _connections.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = _sql.Read("Accounts/GetReconciliations.sql");
            command.Parameters.Add(SqlParameters.Int("@AccountId", accountId));
            List<AccountReconciliation> results = new();
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(new AccountReconciliation
                {
                    AccountReconciliationId = reader.GetInt64Value("AccountReconciliationId"),
                    AccountId = reader.GetInt32Value("AccountId"),
                    FromDate = reader.GetDateOnlyOrNull("FromDate") ?? DateOnly.MinValue,
                    ToDate = reader.GetDateOnlyOrNull("ToDate") ?? DateOnly.MinValue,
                    OpeningBalance = reader.GetDecimalValue("OpeningBalance"),
                    ClosingBalance = reader.GetDecimalValue("ClosingBalance"),
                    ExpectedClosingBalance = reader.GetDecimalValue("ExpectedClosingBalance"),
                    NetActivity = reader.GetDecimalValue("NetActivity"),
                    TransactionCount = reader.GetInt32Value("TransactionCount"),
                    DateFallbackCount = reader.GetInt32Value("DateFallbackCount"),
                    UsesLiabilityBalances = reader.GetBooleanValue("UsesLiabilityBalances"),
                    RecordedAtUtc = reader.GetDateTimeValue("RecordedAtUtc")
                });
            }

            return results;
        }
    }
}
