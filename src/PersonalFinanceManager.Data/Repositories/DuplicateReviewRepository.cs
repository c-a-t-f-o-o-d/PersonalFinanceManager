using System.Security.Cryptography;
using System.Text;
using PersonalFinanceManager.Core.Common;
using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Data.Sql;
using Microsoft.Data.SqlClient;

namespace PersonalFinanceManager.Data.Repositories
{
    public sealed class DuplicateReviewRepository
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly SqlResourceReader _sqlResources;

        public DuplicateReviewRepository(SqlConnectionFactory connectionFactory, SqlResourceReader? sqlResources = null)
        {
            _connectionFactory = connectionFactory;
            _sqlResources = sqlResources ?? new SqlResourceReader();
        }

        public async Task<IReadOnlyList<DuplicateReviewItem>> GetAsync(DuplicateReviewFilter filter, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            int maxRows = Math.Clamp(filter.MaxRows <= 0 ? 500 : filter.MaxRows, 1, 5000);
            List<DuplicateReviewItem> items = new();
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            items.AddRange(await GetImportDuplicateRowsAsync(connection, filter, maxRows, cancellationToken).ConfigureAwait(false));
            items.AddRange(await GetCrossAccountDuplicateRowsAsync(connection, filter, maxRows, cancellationToken).ConfigureAwait(false));
            return items.OrderByDescending(item => item.CreatedAtUtc).Take(maxRows).ToList();
        }

        public async Task<int> ScanCrossAccountDuplicatesAsync(CancellationToken cancellationToken = default)
        {
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();

            command.CommandText = _sqlResources.Read("DuplicateReview/ScanCrossAccountDuplicates.sql");

            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task KeepCrossAccountAsync(long crossAccountDuplicateReviewId, CancellationToken cancellationToken = default)
        {
            await UpdateCrossAccountStatusAsync(crossAccountDuplicateReviewId, "Kept", cancellationToken).ConfigureAwait(false);
        }

        public async Task DeleteCrossAccountCandidateAsync(long crossAccountDuplicateReviewId, CancellationToken cancellationToken = default)
        {
            await DeleteCrossAccountTransactionAsync(crossAccountDuplicateReviewId, deleteCandidate: true, cancellationToken).ConfigureAwait(false);
        }

        public async Task DeleteCrossAccountExistingAsync(long crossAccountDuplicateReviewId, CancellationToken cancellationToken = default)
        {
            await DeleteCrossAccountTransactionAsync(crossAccountDuplicateReviewId, deleteCandidate: false, cancellationToken).ConfigureAwait(false);
        }

        private async Task<IReadOnlyList<DuplicateReviewItem>> GetImportDuplicateRowsAsync(SqlConnection connection, DuplicateReviewFilter filter, int maxRows, CancellationToken cancellationToken)
        {
            if (IsCrossAccountOnlyStatus(filter.Status))
            {
                return Array.Empty<DuplicateReviewItem>();
            }

            List<string> clauses = new();
            List<SqlParameter> parameters = new();

            if (filter.AccountId.HasValue)
            {
                clauses.Add("b.AccountId = @AccountId");
                parameters.Add(SqlParameters.Int("@AccountId", filter.AccountId.Value));
            }

            AddImportStatusClause(filter.Status, clauses, parameters);

            if (!string.IsNullOrWhiteSpace(filter.SearchText))
            {
                clauses.Add("(r.[Description] LIKE @SearchText OR r.[Type] LIKE @SearchText OR r.Category LIKE @SearchText OR r.SpendingClass LIKE @SearchText OR r.FundingSource LIKE @SearchText OR existing.[Description] LIKE @SearchText OR existingAccount.AccountNickname LIKE @SearchText OR a.AccountNickname LIKE @SearchText)");
                parameters.Add(SqlParameters.NVarChar("@SearchText", "%" + filter.SearchText.Trim() + "%", 1000));
            }

            string where = clauses.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", clauses);
            await using SqlCommand command = connection.CreateCommand();

            command.CommandText = _sqlResources.Read("DuplicateReview/GetImportDuplicateRows.sql")
                .Replace("/*MAX_ROWS*/", maxRows.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("/*REVIEW_KIND*/", DuplicateReviewKinds.ImportCandidate, StringComparison.Ordinal)
                .Replace("/*WHERE_CLAUSE*/", where, StringComparison.Ordinal);

            foreach (SqlParameter parameter in parameters)
            {
                command.Parameters.Add(parameter);
            }

            List<DuplicateReviewItem> items = new();
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                items.Add(Map(reader));
            }

            return items;
        }

        private async Task<IReadOnlyList<DuplicateReviewItem>> GetCrossAccountDuplicateRowsAsync(SqlConnection connection, DuplicateReviewFilter filter, int maxRows, CancellationToken cancellationToken)
        {
            if (IsImportOnlyStatus(filter.Status))
            {
                return Array.Empty<DuplicateReviewItem>();
            }

            List<string> clauses = new();
            List<SqlParameter> parameters = new();

            if (filter.AccountId.HasValue)
            {
                clauses.Add("(candidate.AccountId = @AccountId OR existing.AccountId = @AccountId)");
                parameters.Add(SqlParameters.Int("@AccountId", filter.AccountId.Value));
            }

            AddCrossAccountStatusClause(filter.Status, clauses, parameters);

            if (!string.IsNullOrWhiteSpace(filter.SearchText))
            {
                clauses.Add("(candidate.[Description] LIKE @SearchText OR existing.[Description] LIKE @SearchText OR candidate.[Type] LIKE @SearchText OR existing.[Type] LIKE @SearchText OR candidate.Category LIKE @SearchText OR existing.Category LIKE @SearchText OR candidate.SpendingClass LIKE @SearchText OR existing.SpendingClass LIKE @SearchText OR candidateAccount.AccountNickname LIKE @SearchText OR existingAccount.AccountNickname LIKE @SearchText)");
                parameters.Add(SqlParameters.NVarChar("@SearchText", "%" + filter.SearchText.Trim() + "%", 1000));
            }

            string where = clauses.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", clauses);
            await using SqlCommand command = connection.CreateCommand();

            command.CommandText = _sqlResources.Read("DuplicateReview/GetCrossAccountDuplicateRows.sql")
                .Replace("/*MAX_ROWS*/", maxRows.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("/*REVIEW_KIND*/", DuplicateReviewKinds.CrossAccount, StringComparison.Ordinal)
                .Replace("/*STATUS_CROSS_ACCOUNT_REVIEW*/", DuplicateReviewStatuses.CrossAccountReview, StringComparison.Ordinal)
                .Replace("/*STATUS_CROSS_ACCOUNT_KEPT*/", DuplicateReviewStatuses.CrossAccountKept, StringComparison.Ordinal)
                .Replace("/*STATUS_CROSS_ACCOUNT_CANDIDATE_DELETED*/", DuplicateReviewStatuses.CrossAccountCandidateDeleted, StringComparison.Ordinal)
                .Replace("/*STATUS_CROSS_ACCOUNT_EXISTING_DELETED*/", DuplicateReviewStatuses.CrossAccountExistingDeleted, StringComparison.Ordinal)
                .Replace("/*WHERE_CLAUSE*/", where, StringComparison.Ordinal);

            foreach (SqlParameter parameter in parameters)
            {
                command.Parameters.Add(parameter);
            }

            List<DuplicateReviewItem> items = new();
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                items.Add(Map(reader));
            }

            return items;
        }

        private static void AddImportStatusClause(string? status, List<string> clauses, List<SqlParameter> parameters)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                clauses.Add("r.[Status] IN (N'DuplicateReview', N'ExactDuplicateSkipped', N'DuplicateAccepted', N'DuplicateIgnored')");
                return;
            }

            if (string.Equals(status, DuplicateReviewStatuses.NeedsReview, StringComparison.OrdinalIgnoreCase))
            {
                clauses.Add("r.[Status] = N'DuplicateReview'");
                return;
            }

            clauses.Add("r.[Status] = @ImportStatus");
            parameters.Add(SqlParameters.NVarChar("@ImportStatus", status.Trim(), 30));
        }

        private static void AddCrossAccountStatusClause(string? status, List<string> clauses, List<SqlParameter> parameters)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                clauses.Add("r.[Status] IN (N'Pending', N'Kept', N'CandidateDeleted', N'ExistingDeleted')");
                return;
            }

            string? databaseStatus = status.Trim() switch
            {
                DuplicateReviewStatuses.NeedsReview => "Pending",
                DuplicateReviewStatuses.CrossAccountReview => "Pending",
                DuplicateReviewStatuses.CrossAccountKept => "Kept",
                DuplicateReviewStatuses.CrossAccountCandidateDeleted => "CandidateDeleted",
                DuplicateReviewStatuses.CrossAccountExistingDeleted => "ExistingDeleted",
                _ => null
            };

            if (databaseStatus is null)
            {
                clauses.Add("1 = 0");
                return;
            }

            clauses.Add("r.[Status] = @CrossAccountStatus");
            parameters.Add(SqlParameters.NVarChar("@CrossAccountStatus", databaseStatus, 30));
        }

        private static bool IsImportOnlyStatus(string? status)
        {
            return string.Equals(status, DuplicateReviewStatuses.ImportDuplicateReview, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, DuplicateReviewStatuses.ExactDuplicateSkipped, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, DuplicateReviewStatuses.DuplicateAccepted, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, DuplicateReviewStatuses.DuplicateIgnored, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCrossAccountOnlyStatus(string? status)
        {
            return string.Equals(status, DuplicateReviewStatuses.CrossAccountReview, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, DuplicateReviewStatuses.CrossAccountKept, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, DuplicateReviewStatuses.CrossAccountCandidateDeleted, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, DuplicateReviewStatuses.CrossAccountExistingDeleted, StringComparison.OrdinalIgnoreCase);
        }

        private async Task UpdateCrossAccountStatusAsync(long crossAccountDuplicateReviewId, string status, CancellationToken cancellationToken)
        {
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();

            command.CommandText = _sqlResources.Read("DuplicateReview/UpdateCrossAccountStatus.sql");

            command.Parameters.Add(SqlParameters.NVarChar("@Status", status, 30));
            command.Parameters.Add(SqlParameters.BigInt("@CrossAccountDuplicateReviewId", crossAccountDuplicateReviewId));
            int affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            if (affected == 0)
            {
                throw new InvalidOperationException("The cross-account duplicate row is no longer pending.");
            }
        }

        private async Task DeleteCrossAccountTransactionAsync(long crossAccountDuplicateReviewId, bool deleteCandidate, CancellationToken cancellationToken)
        {
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await using SqlCommand select = connection.CreateCommand();
                select.Transaction = transaction;

                select.CommandText = _sqlResources.Read("DuplicateReview/GetCrossAccountTransactionIds.sql");

                select.Parameters.Add(SqlParameters.BigInt("@CrossAccountDuplicateReviewId", crossAccountDuplicateReviewId));
                long? candidateId;
                long? existingId;

                await using (SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        throw new InvalidOperationException("The cross-account duplicate row is no longer pending.");
                    }

                    candidateId = reader.GetInt64OrNull("CandidateTransactionId");
                    existingId = reader.GetInt64OrNull("ExistingTransactionId");
                }

                long? transactionId = deleteCandidate ? candidateId : existingId;

                if (!transactionId.HasValue)
                {
                    throw new InvalidOperationException("The selected cross-account duplicate transaction was already removed.");
                }

                await DeleteImportedTransactionAsync(connection, transaction, transactionId.Value, cancellationToken).ConfigureAwait(false);
                await MarkCrossAccountDeletedAsync(connection, transaction, crossAccountDuplicateReviewId, deleteCandidate, cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }

        private async Task DeleteImportedTransactionAsync(SqlConnection connection, SqlTransaction transaction, long importedTransactionId, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = _sqlResources.Read("Transactions/DeleteTransaction.sql");

            command.Parameters.Add(SqlParameters.BigInt("@ImportedTransactionId", importedTransactionId));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task MarkCrossAccountDeletedAsync(SqlConnection connection, SqlTransaction transaction, long crossAccountDuplicateReviewId, bool deletedCandidate, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = _sqlResources.Read(deletedCandidate
                ? "DuplicateReview/MarkCrossAccountCandidateDeleted.sql"
                : "DuplicateReview/MarkCrossAccountExistingDeleted.sql");

            command.Parameters.Add(SqlParameters.BigInt("@CrossAccountDuplicateReviewId", crossAccountDuplicateReviewId));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<long> ApproveAsync(long importRowId, CancellationToken cancellationToken = default)
        {
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                CandidateImportRow candidate = await GetCandidateAsync(connection, transaction, importRowId, cancellationToken).ConfigureAwait(false);

                if (string.Equals(candidate.Status, "DuplicateAccepted", StringComparison.OrdinalIgnoreCase) && candidate.AcceptedTransactionId.HasValue)
                {
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                    return candidate.AcceptedTransactionId.Value;
                }

                if (!IsRecoverableCandidate(candidate.Status))
                {
                    throw new InvalidOperationException("Only pending or exact-skipped duplicate rows can be accepted.");
                }

                long transactionId = await InsertApprovedTransactionAsync(connection, transaction, candidate, cancellationToken).ConfigureAwait(false);
                await UpdateImportRowStatusAsync(connection, transaction, importRowId, "DuplicateAccepted", "Approved and added as a transaction.", transactionId, cancellationToken).ConfigureAwait(false);
                await UpdateBatchCountsAsync(connection, transaction, candidate.ImportBatchId, cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return transactionId;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }

        public async Task IgnoreAsync(long importRowId, CancellationToken cancellationToken = default)
        {
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                CandidateImportRow candidate = await GetCandidateAsync(connection, transaction, importRowId, cancellationToken).ConfigureAwait(false);

                if (string.Equals(candidate.Status, "DuplicateIgnored", StringComparison.OrdinalIgnoreCase))
                {
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                    return;
                }

                if (!IsRecoverableCandidate(candidate.Status))
                {
                    throw new InvalidOperationException("Only pending or exact-skipped duplicate rows can be ignored.");
                }

                await UpdateImportRowStatusAsync(connection, transaction, importRowId, "DuplicateIgnored", "Ignored from duplicate review and not added as a transaction.", null, cancellationToken).ConfigureAwait(false);
                await UpdateBatchCountsAsync(connection, transaction, candidate.ImportBatchId, cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }

        private async Task<CandidateImportRow> GetCandidateAsync(SqlConnection connection, SqlTransaction transaction, long importRowId, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = _sqlResources.Read("DuplicateReview/GetCandidateImportRow.sql");

            command.Parameters.Add(SqlParameters.BigInt("@ImportRowId", importRowId));
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("The duplicate review row could not be found.");
            }

            DateOnly? date = reader.GetDateOnlyOrNull("TransactionDate");
            decimal? amount = reader.GetDecimalOrNull("Amount");
            string description = reader.GetStringOrEmpty("Description");
            string type = reader.GetStringOrEmpty("Type");

            if (!date.HasValue || !amount.HasValue || string.IsNullOrWhiteSpace(description) || string.IsNullOrWhiteSpace(type))
            {
                throw new InvalidOperationException("The duplicate review row is missing required transaction data.");
            }

            return new CandidateImportRow
            {
                ImportRowId = reader.GetInt64Value("ImportRowId"),
                ImportBatchId = reader.GetInt64Value("ImportBatchId"),
                Status = reader.GetStringOrEmpty("Status"),
                AcceptedTransactionId = reader.GetInt64OrNull("AcceptedTransactionId"),
                AccountId = reader.GetInt32Value("AccountId"),
                TransactionDate = date.Value,
                PostingDate = reader.GetDateOnlyOrNull("PostingDate"),
                TransactionTime = reader.GetTimeOnlyOrNull("TransactionTime"),
                Amount = amount.Value,
                Type = type,
                Description = description,
                ExternalTransactionId = reader.GetStringOrNull("ExternalTransactionId"),
                FundingSource = reader.GetStringOrNull("FundingSource"),
                SourceCategory = reader.GetStringOrNull("SourceCategory"),
                Memo = reader.GetStringOrNull("Memo"),
                AppliedRuleId = reader.GetInt32OrNull("AppliedRuleId"),
                Category = reader.GetStringOrNull("Category"),
                SpendingClass = reader.GetStringOrNull("SpendingClass"),
                Merchant = reader.GetStringOrNull("Merchant"),
                IsIncome = reader.GetBooleanOrNull("IsIncome"),
                IsTransfer = reader.GetBooleanOrNull("IsTransfer"),
                IncomeOwner = reader.GetStringOrNull("IncomeOwner")
            };
        }

        private async Task<long> InsertApprovedTransactionAsync(SqlConnection connection, SqlTransaction transaction, CandidateImportRow candidate, CancellationToken cancellationToken)
        {
            string rowHash = BuildRowHash(candidate);
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = _sqlResources.Read("DuplicateReview/InsertApprovedTransaction.sql");

            command.Parameters.Add(SqlParameters.Int("@AccountId", candidate.AccountId));
            command.Parameters.Add(SqlParameters.BigInt("@ImportBatchId", candidate.ImportBatchId));
            command.Parameters.Add(SqlParameters.Date("@TransactionDate", candidate.TransactionDate));
            command.Parameters.Add(SqlParameters.Date("@PostingDate", candidate.PostingDate));
            command.Parameters.Add(SqlParameters.Time("@TransactionTime", candidate.TransactionTime));
            command.Parameters.Add(SqlParameters.Decimal("@Amount", candidate.Amount));
            command.Parameters.Add(SqlParameters.NVarChar("@Type", candidate.Type, 100));
            command.Parameters.Add(SqlParameters.NVarChar("@Description", candidate.Description, 1000));
            command.Parameters.Add(SqlParameters.NVarChar("@ExternalTransactionId", candidate.ExternalTransactionId, 100));
            command.Parameters.Add(SqlParameters.NVarChar("@FundingSource", candidate.FundingSource, 200));
            command.Parameters.Add(SqlParameters.NVarChar("@SourceCategory", candidate.SourceCategory, 200));
            command.Parameters.Add(SqlParameters.NVarChar("@Memo", candidate.Memo, 1000));
            command.Parameters.Add(SqlParameters.Int("@AppliedRuleId", candidate.AppliedRuleId));
            command.Parameters.Add(SqlParameters.NVarChar("@Category", candidate.Category, 200));
            command.Parameters.Add(SqlParameters.NVarChar("@SpendingClass", candidate.SpendingClass, 100));
            command.Parameters.Add(SqlParameters.NVarChar("@Merchant", candidate.Merchant, 200));
            command.Parameters.Add(SqlParameters.Bit("@IsIncome", candidate.IsIncome ?? InferIncome(candidate)));
            command.Parameters.Add(SqlParameters.Bit("@IsTransfer", candidate.IsTransfer ?? InferTransfer(candidate)));
            command.Parameters.Add(SqlParameters.NVarChar("@IncomeOwner", OwnerValues.Normalize(candidate.IncomeOwner), 50));
            command.Parameters.Add(SqlParameters.NVarChar("@Notes", $"Approved from duplicate review import row {candidate.ImportRowId}.", 1000));
            command.Parameters.Add(SqlParameters.NVarChar("@RawRowHash", rowHash, 64));
            object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt64(value);
        }

        private async Task UpdateImportRowStatusAsync(SqlConnection connection, SqlTransaction transaction, long importRowId, string status, string note, long? acceptedTransactionId, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = _sqlResources.Read("DuplicateReview/UpdateImportRowStatus.sql");

            command.Parameters.Add(SqlParameters.NVarChar("@Status", status, 30));
            command.Parameters.Add(SqlParameters.NVarChar("@Note", note, 1000));
            command.Parameters.Add(SqlParameters.BigInt("@ImportRowId", importRowId));
            command.Parameters.Add(SqlParameters.BigInt("@AcceptedTransactionId", acceptedTransactionId));
            if (await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
            {
                throw new InvalidOperationException("The duplicate candidate was resolved by another operation.");
            }
        }

        private async Task UpdateBatchCountsAsync(SqlConnection connection, SqlTransaction transaction, long importBatchId, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = _sqlResources.Read("DuplicateReview/UpdateImportBatchCounts.sql");

            command.Parameters.Add(SqlParameters.BigInt("@ImportBatchId", importBatchId));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static DuplicateReviewItem Map(SqlDataReader reader)
        {
            return new DuplicateReviewItem
            {
                ReviewKind = reader.GetStringOrEmpty("ReviewKind"),
                CrossAccountDuplicateReviewId = reader.GetInt64OrNull("CrossAccountDuplicateReviewId"),
                ImportRowId = reader.GetInt64Value("ImportRowId"),
                ImportBatchId = reader.GetInt64Value("ImportBatchId"),
                RowNumber = reader.GetInt32Value("RowNumber"),
                Status = reader.GetStringOrEmpty("Status"),
                DuplicateConfidence = reader.GetStringOrNull("DuplicateConfidence"),
                DuplicateReason = reader.GetStringOrNull("DuplicateReason"),
                ImportProfileCode = reader.GetStringOrEmpty("ImportProfileCode"),
                OriginalFileName = reader.GetStringOrEmpty("OriginalFileName"),
                AccountId = reader.GetInt32Value("AccountId"),
                AccountNickname = reader.GetStringOrEmpty("AccountNickname"),
                CandidateTransactionId = reader.GetInt64OrNull("CandidateTransactionId"),
                CandidateAccountNickname = reader.GetStringOrNull("CandidateAccountNickname"),
                CandidateDate = reader.GetDateOnlyOrNull("CandidateDate"),
                CandidatePostingDate = reader.GetDateOnlyOrNull("CandidatePostingDate"),
                CandidateTime = reader.GetTimeOnlyOrNull("CandidateTime"),
                CandidateAmount = reader.GetDecimalOrNull("CandidateAmount"),
                CandidateType = reader.GetStringOrNull("CandidateType"),
                CandidateDescription = reader.GetStringOrNull("CandidateDescription"),
                CandidateExternalTransactionId = reader.GetStringOrNull("CandidateExternalTransactionId"),
                CandidateFundingSource = reader.GetStringOrNull("CandidateFundingSource"),
                CandidateSourceCategory = reader.GetStringOrNull("CandidateSourceCategory"),
                CandidateMemo = reader.GetStringOrNull("CandidateMemo"),
                CandidateCategory = reader.GetStringOrNull("CandidateCategory"),
                CandidateCategoryType = reader.GetStringOrNull("CandidateCategoryType"),
                CandidateMerchant = reader.GetStringOrNull("CandidateMerchant"),
                CandidateIsIncome = reader.GetBooleanOrNull("CandidateIsIncome"),
                CandidateIsTransfer = reader.GetBooleanOrNull("CandidateIsTransfer"),
                ExistingTransactionId = reader.GetInt64OrNull("ExistingTransactionId"),
                ExistingAccountNickname = reader.GetStringOrNull("ExistingAccountNickname"),
                ExistingDate = reader.GetDateOnlyOrNull("ExistingDate"),
                ExistingPostingDate = reader.GetDateOnlyOrNull("ExistingPostingDate"),
                ExistingTime = reader.GetTimeOnlyOrNull("ExistingTime"),
                ExistingAmount = reader.GetDecimalOrNull("ExistingAmount"),
                ExistingType = reader.GetStringOrNull("ExistingType"),
                ExistingDescription = reader.GetStringOrNull("ExistingDescription"),
                ExistingExternalTransactionId = reader.GetStringOrNull("ExistingExternalTransactionId"),
                ExistingFundingSource = reader.GetStringOrNull("ExistingFundingSource"),
                ExistingSourceCategory = reader.GetStringOrNull("ExistingSourceCategory"),
                ExistingMemo = reader.GetStringOrNull("ExistingMemo"),
                ExistingCategory = reader.GetStringOrNull("ExistingCategory"),
                ExistingCategoryType = reader.GetStringOrNull("ExistingCategoryType"),
                ExistingMerchant = reader.GetStringOrNull("ExistingMerchant"),
                ExistingIsIncome = reader.GetBooleanOrNull("ExistingIsIncome"),
                ExistingIsTransfer = reader.GetBooleanOrNull("ExistingIsTransfer"),
                CreatedAtUtc = reader.GetDateTimeValue("CreatedAtUtc")
            };
        }

        private static bool InferIncome(CandidateImportRow candidate)
        {
            return candidate.Amount > 0 && !InferTransfer(candidate);
        }

        private static bool IsRecoverableCandidate(string status) =>
            string.Equals(status, "DuplicateReview", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "ExactDuplicateSkipped", StringComparison.OrdinalIgnoreCase);

        private static bool InferTransfer(CandidateImportRow candidate)
        {
            return string.Equals(candidate.Category, "Transfer", StringComparison.OrdinalIgnoreCase) || string.Equals(candidate.SpendingClass, "PayPal Funding", StringComparison.OrdinalIgnoreCase) || string.Equals(candidate.SpendingClass, "Credit Card Payment", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildRowHash(CandidateImportRow transaction)
        {
            string text = string.Join("|",
                transaction.AccountId,
                transaction.TransactionDate,
                transaction.PostingDate,
                transaction.TransactionTime,
                transaction.Amount,
                TextNormalizer.NormalizeForComparison(transaction.Type),
                TextNormalizer.NormalizeForComparison(transaction.Description),
                TextNormalizer.NormalizeForComparison(transaction.FundingSource ?? string.Empty),
                TextNormalizer.NormalizeForComparison(transaction.ExternalTransactionId ?? string.Empty),
                TextNormalizer.NormalizeForComparison(transaction.SourceCategory ?? string.Empty),
                TextNormalizer.NormalizeForComparison(transaction.Memo ?? string.Empty));

            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
            return Convert.ToHexString(hash);
        }

        private sealed class CandidateImportRow
        {
            public long ImportRowId { get; set; }
            public long ImportBatchId { get; set; }
            public string Status { get; set; } = string.Empty;
            public long? AcceptedTransactionId { get; set; }
            public int AccountId { get; set; }
            public DateOnly TransactionDate { get; set; }
            public DateOnly? PostingDate { get; set; }
            public TimeOnly? TransactionTime { get; set; }
            public decimal Amount { get; set; }
            public string Type { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string? ExternalTransactionId { get; set; }
            public string? FundingSource { get; set; }
            public string? SourceCategory { get; set; }
            public string? Memo { get; set; }
            public int? AppliedRuleId { get; set; }
            public string? Category { get; set; }
            public string? SpendingClass { get; set; }
            public string? Merchant { get; set; }
            public bool? IsIncome { get; set; }
            public bool? IsTransfer { get; set; }
            public string? IncomeOwner { get; set; }
        }
    }
}
