using System.Security.Cryptography;
using PersonalFinanceManager.Core.Common;
using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Data.Security;
using PersonalFinanceManager.Data.Sql;
using Microsoft.Data.SqlClient;

namespace PersonalFinanceManager.Data.Repositories
{
    public sealed class AccountRepository
    {
        private readonly AccountDataProtector _protector;
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly SqlResourceReader _sqlResources = new();

        public AccountRepository(SqlConnectionFactory connectionFactory, AccountDataProtector protector)
        {
            _connectionFactory = connectionFactory;
            _protector = protector;
        }

        public async Task<IReadOnlyList<Account>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            List<Account> accounts = new();
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();

            command.CommandText = _sqlResources.Read("Accounts/GetAccounts.sql");

            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                accounts.Add(Map(reader, includeProtectedValue: false));
            }

            return accounts;
        }

        public async Task<Account?> GetByIdAsync(int accountId, bool includeAccountNumber = true, CancellationToken cancellationToken = default)
        {
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = _sqlResources.Read("Accounts/GetAccountById.sql");
            command.Parameters.Add(SqlParameters.Int("@AccountId", accountId));
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            Account account = Map(reader, includeProtectedValue: includeAccountNumber);
            return account;
        }

        public async Task<int> SaveAsync(Account account, CancellationToken cancellationToken = default)
        {
            if (account.AccountId == 0)
            {
                return await InsertAsync(account, cancellationToken).ConfigureAwait(false);
            }

            await UpdateAsync(account, cancellationToken).ConfigureAwait(false);
            return account.AccountId;
        }

        public async Task DeleteAsync(int accountId, CancellationToken cancellationToken = default)
        {
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = _sqlResources.Read("Accounts/DeleteAccount.sql");
            command.Parameters.Add(SqlParameters.Int("@AccountId", accountId));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task<int> InsertAsync(Account account, CancellationToken cancellationToken)
        {
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();

            command.CommandText = _sqlResources.Read("Accounts/InsertAccount.sql");

            AddParameters(command, account);
            object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return System.Convert.ToInt32(value);
        }

        private async Task UpdateAsync(Account account, CancellationToken cancellationToken)
        {
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();

            command.CommandText = _sqlResources.Read("Accounts/UpdateAccount.sql");

            AddParameters(command, account);
            command.Parameters.Add(SqlParameters.Int("@AccountId", account.AccountId));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private void AddParameters(SqlCommand command, Account account)
        {
            command.Parameters.Add(SqlParameters.NVarChar("@AccountNickname", account.AccountNickname, 200));
            command.Parameters.Add(SqlParameters.NVarChar("@Institution", account.Institution, 200));
            command.Parameters.Add(SqlParameters.NVarChar("@BaseUrl", account.BaseUrl, 1000));
            command.Parameters.Add(SqlParameters.NVarChar("@AccountSummaryUrl", account.AccountSummaryUrl, 1000));
            command.Parameters.Add(SqlParameters.NVarChar("@AccountType", account.AccountType, 100));
            command.Parameters.Add(SqlParameters.NVarChar("@AccountOwner", OwnerValues.Normalize(account.AccountOwner) ?? OwnerValues.Unassigned, 50));
            command.Parameters.Add(SqlParameters.Bit("@PreserveExistingAccountNumber", account.PreserveExistingAccountNumberOnSave && account.AccountId > 0));
            command.Parameters.Add(SqlParameters.VarBinary("@AccountNumberProtected", _protector.Protect(account.AccountNumber)));
            command.Parameters.Add(SqlParameters.NVarChar("@AccountNumberLastFour", TextNormalizer.LastFour(account.AccountNumber), 10));
            command.Parameters.Add(SqlParameters.Decimal("@PrincipalBalance", account.PrincipalBalance));
            command.Parameters.Add(SqlParameters.Decimal("@AvailableBalance", account.AvailableBalance));
            command.Parameters.Add(SqlParameters.Decimal("@CurrentBalance", account.CurrentBalance));
            command.Parameters.Add(SqlParameters.Decimal("@OriginalLoanAmount", account.OriginalLoanAmount));
            command.Parameters.Add(SqlParameters.Decimal("@CurrentInterestRate", account.CurrentInterestRate, 9, 5));
            command.Parameters.Add(SqlParameters.Decimal("@LastPaymentAmount", account.LastPaymentAmount));
            command.Parameters.Add(SqlParameters.Decimal("@NextPaymentAmount", account.NextPaymentAmount));
            command.Parameters.Add(SqlParameters.Date("@NextPaymentDueDate", account.NextPaymentDueDate));
            command.Parameters.Add(SqlParameters.Decimal("@NextPaymentInterest", account.NextPaymentInterest, 9, 5));
            command.Parameters.Add(SqlParameters.Decimal("@NextPaymentEscrowAmount", account.NextPaymentEscrowAmount));
            command.Parameters.Add(SqlParameters.Date("@LastEscrowAnalysisDate", account.LastEscrowAnalysisDate));
            command.Parameters.Add(SqlParameters.Decimal("@CurrentEscrowAccountBalance", account.CurrentEscrowAccountBalance));
            command.Parameters.Add(SqlParameters.Decimal("@CurrentEscrowPayment", account.CurrentEscrowPayment));
            command.Parameters.Add(SqlParameters.Decimal("@ProjectedEscrowPayment", account.ProjectedEscrowPayment));
            command.Parameters.Add(SqlParameters.Date("@ProjectedEscrowPaymentDate", account.ProjectedEscrowPaymentDate));
            command.Parameters.Add(SqlParameters.Decimal("@LastPaymentInterestAmount", account.LastPaymentInterestAmount));
            command.Parameters.Add(SqlParameters.Decimal("@LastPaymentEscrow", account.LastPaymentEscrow));
            command.Parameters.Add(SqlParameters.Decimal("@RegularMonthlyPayment", account.RegularMonthlyPayment));
            command.Parameters.Add(SqlParameters.Decimal("@LatePaymentFee", account.LatePaymentFee));
            command.Parameters.Add(SqlParameters.NVarChar("@EscrowDeletionFee", account.EscrowDeletionFee, 200));
            command.Parameters.Add(SqlParameters.NVarChar("@LateCharges", account.LateCharges, 200));
            command.Parameters.Add(SqlParameters.NVarChar("@LoanAssumptionFee", account.LoanAssumptionFee, 200));
            command.Parameters.Add(SqlParameters.NVarChar("@PartialReleaseFee", account.PartialReleaseFee, 200));
            command.Parameters.Add(SqlParameters.NVarChar("@PrincipalReductionReAmortizationFee", account.PrincipalReductionReAmortizationFee, 200));
            command.Parameters.Add(SqlParameters.Int("@LateAfterDays", account.LateAfterDays));
            command.Parameters.Add(SqlParameters.Date("@LastPaymentDateReceived", account.LastPaymentDateReceived));
            command.Parameters.Add(SqlParameters.NVarChar("@Recurrence", account.Recurrence, 100));
            command.Parameters.Add(SqlParameters.Decimal("@PaidInFullDiscount", account.PaidInFullDiscount));
            command.Parameters.Add(SqlParameters.Bit("@IsActive", account.IsActive));
        }

        private Account Map(SqlDataReader reader, bool includeProtectedValue)
        {
            byte[]? protectedAccountNumber = reader.GetBytesOrNull("AccountNumberProtected");
            ProtectedAccountNumber protectedValue = ReadProtectedAccountNumber(protectedAccountNumber, includeProtectedValue);

            return new Account
            {
                AccountId = reader.GetInt32Value("AccountId"),
                AccountNickname = reader.GetStringOrEmpty("AccountNickname"),
                Institution = reader.GetStringOrEmpty("Institution"),
                BaseUrl = reader.GetStringOrNull("BaseUrl"),
                AccountSummaryUrl = reader.GetStringOrNull("AccountSummaryUrl"),
                AccountType = reader.GetStringOrEmpty("AccountType"),
                AccountOwner = reader.GetStringOrNull("AccountOwner") ?? OwnerValues.Unassigned,
                AccountNumber = protectedValue.AccountNumber,
                AccountNumberLastFour = reader.GetStringOrNull("AccountNumberLastFour"),
                AccountNumberProtectedValueExists = protectedValue.ProtectedValueExists,
                AccountNumberCannotBeDecrypted = protectedValue.CannotBeDecrypted,
                PreserveExistingAccountNumberOnSave = protectedValue.CannotBeDecrypted,
                AccountNumberProtectionMessage = protectedValue.Message,
                PrincipalBalance = reader.GetDecimalOrNull("PrincipalBalance"),
                AvailableBalance = reader.GetDecimalOrNull("AvailableBalance"),
                CurrentBalance = reader.GetDecimalOrNull("CurrentBalance"),
                OriginalLoanAmount = reader.GetDecimalOrNull("OriginalLoanAmount"),
                CurrentInterestRate = reader.GetDecimalOrNull("CurrentInterestRate"),
                LastPaymentAmount = reader.GetDecimalOrNull("LastPaymentAmount"),
                NextPaymentAmount = reader.GetDecimalOrNull("NextPaymentAmount"),
                NextPaymentDueDate = reader.GetDateOnlyOrNull("NextPaymentDueDate"),
                NextPaymentInterest = reader.GetDecimalOrNull("NextPaymentInterest"),
                NextPaymentEscrowAmount = reader.GetDecimalOrNull("NextPaymentEscrowAmount"),
                LastEscrowAnalysisDate = reader.GetDateOnlyOrNull("LastEscrowAnalysisDate"),
                CurrentEscrowAccountBalance = reader.GetDecimalOrNull("CurrentEscrowAccountBalance"),
                CurrentEscrowPayment = reader.GetDecimalOrNull("CurrentEscrowPayment"),
                ProjectedEscrowPayment = reader.GetDecimalOrNull("ProjectedEscrowPayment"),
                ProjectedEscrowPaymentDate = reader.GetDateOnlyOrNull("ProjectedEscrowPaymentDate"),
                LastPaymentInterestAmount = reader.GetDecimalOrNull("LastPaymentInterestAmount"),
                LastPaymentEscrow = reader.GetDecimalOrNull("LastPaymentEscrow"),
                RegularMonthlyPayment = reader.GetDecimalOrNull("RegularMonthlyPayment"),
                LatePaymentFee = reader.GetDecimalOrNull("LatePaymentFee"),
                EscrowDeletionFee = reader.GetStringOrNull("EscrowDeletionFee"),
                LateCharges = reader.GetStringOrNull("LateCharges"),
                LoanAssumptionFee = reader.GetStringOrNull("LoanAssumptionFee"),
                PartialReleaseFee = reader.GetStringOrNull("PartialReleaseFee"),
                PrincipalReductionReAmortizationFee = reader.GetStringOrNull("PrincipalReductionReAmortizationFee"),
                LateAfterDays = reader.GetInt32OrNull("LateAfterDays"),
                LastPaymentDateReceived = reader.GetDateOnlyOrNull("LastPaymentDateReceived"),
                Recurrence = reader.GetStringOrNull("Recurrence"),
                PaidInFullDiscount = reader.GetDecimalOrNull("PaidInFullDiscount"),
                IsActive = reader.GetBooleanValue("IsActive"),
                CreatedAtUtc = reader.GetDateTimeValue("CreatedAtUtc"),
                UpdatedAtUtc = reader.GetDateTimeValue("UpdatedAtUtc")
            };
        }
        private ProtectedAccountNumber ReadProtectedAccountNumber(byte[]? protectedBytes, bool includeProtectedValue)
        {
            bool protectedValueExists = protectedBytes is { Length: > 0 };

            if (!includeProtectedValue || !protectedValueExists)
            {
                return new ProtectedAccountNumber(null, protectedValueExists, false, null);
            }

            try
            {
                return new ProtectedAccountNumber(_protector.Unprotect(protectedBytes), true, false, null);
            }
            catch (CryptographicException)
            {
                return new ProtectedAccountNumber(null, true, true, "The saved account number was encrypted by a different Windows user profile or machine. Leave the field blank to preserve it, or enter the account number again to replace it for this Windows profile.");
            }
        }

        private sealed record ProtectedAccountNumber(string? AccountNumber, bool ProtectedValueExists, bool CannotBeDecrypted, string? Message);

    }
}
