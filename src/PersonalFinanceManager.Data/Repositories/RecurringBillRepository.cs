using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Core.Planning;
using PersonalFinanceManager.Data.Sql;
using Microsoft.Data.SqlClient;

namespace PersonalFinanceManager.Data.Repositories
{
    public sealed class RecurringBillRepository
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly SqlResourceReader _sqlResources = new();

        public RecurringBillRepository(SqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IReadOnlyList<RecurringBill>> GetBillsAsync(CancellationToken cancellationToken = default)
        {
            List<RecurringBill> bills = new();
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = _sqlResources.Read("RecurringBills/GetBills.sql");
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                bills.Add(MapBill(reader));
            }

            return bills;
        }

        public async Task<RecurringBill?> GetByIdAsync(int recurringBillId, CancellationToken cancellationToken = default)
        {
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = _sqlResources.Read("RecurringBills/GetBillById.sql");
            command.Parameters.Add(SqlParameters.Int("@RecurringBillId", recurringBillId));
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? MapBill(reader) : null;
        }

        public async Task<int> SaveAsync(RecurringBill bill, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(bill);
            Validate(bill);
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);

            if (bill.IsActive)
            {
                await ValidateMatchingIdentitiesAsync(connection, transaction, bill, cancellationToken).ConfigureAwait(false);
            }

            int id = bill.RecurringBillId;
            byte[] rowVersion;

            if (id == 0)
            {
                (id, rowVersion) = await InsertAsync(connection, transaction, bill, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                rowVersion = await UpdateAsync(connection, transaction, bill, cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            bill.RecurringBillId = id;
            bill.RowVersion = rowVersion;
            return id;
        }

        public async Task DeleteAsync(int recurringBillId, CancellationToken cancellationToken = default)
        {
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = _sqlResources.Read("RecurringBills/DeleteBill.sql");
            command.Parameters.Add(SqlParameters.Int("@RecurringBillId", recurringBillId));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<RecurringBillCandidate>> GetMonthlyCandidatesAsync(int minimumTransactions, int minimumMonths, CancellationToken cancellationToken = default)
        {
            ReportRepository reports = new(_connectionFactory);
            return RecurringBillCandidateDetector.Detect(await reports.GetDataAsync(cancellationToken).ConfigureAwait(false), minimumTransactions, minimumMonths);
        }

        public async Task<int> CreateFromCandidateAsync(RecurringBillCandidate candidate, CancellationToken cancellationToken = default)
        {
            RecurringBill draft = RecurringBillCandidateDetector.CreateDraft(candidate, DateOnly.FromDateTime(DateTime.Today));
            return await SaveAsync(draft, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<string>> GetRequirementTypesAsync(CancellationToken cancellationToken = default)
        {
            List<string> values = new()
            {
                BillRequirementTypes.Need,
                BillRequirementTypes.Want,
                BillRequirementTypes.Debt,
                BillRequirementTypes.Savings,
                BillRequirementTypes.Investment,
                BillRequirementTypes.Transfer,
                BillRequirementTypes.Ignore
            };

            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = _sqlResources.Read("RecurringBills/GetRequirementTypes.sql");
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                string value = reader.GetStringOrEmpty("RequirementType");

                if (!values.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    values.Add(value);
                }
            }

            return values.OrderBy(value => value).ToList();
        }

        private async Task ValidateMatchingIdentitiesAsync(SqlConnection connection, SqlTransaction transaction, RecurringBill bill, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = _sqlResources.Read("RecurringBills/GetBillsForMatching.sql");
            command.Parameters.Add(SqlParameters.Int("@RecurringBillId", bill.RecurringBillId));
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                RecurringBill existing = MapBill(reader);

                if (RecurringBillMatcher.HasOverlappingIdentity(existing, bill))
                {
                    throw new InvalidOperationException($"The payment identity overlaps active bill '{existing.BillName}'. Add former or current names to that bill, deactivate the duplicate, or use distinct statement descriptions and accounts for separate obligations.");
                }
            }
        }

        private async Task<(int Id, byte[] RowVersion)> InsertAsync(SqlConnection connection, SqlTransaction transaction, RecurringBill bill, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = _sqlResources.Read("RecurringBills/InsertBill.sql");
            AddParameters(command, bill);
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            return (reader.GetInt32(0), reader.GetFieldValue<byte[]>(1));
        }

        private async Task<byte[]> UpdateAsync(SqlConnection connection, SqlTransaction transaction, RecurringBill bill, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = _sqlResources.Read("RecurringBills/UpdateBill.sql");
            AddParameters(command, bill);
            command.Parameters.Add(SqlParameters.Int("@RecurringBillId", bill.RecurringBillId));
            command.Parameters.Add(new SqlParameter("@RowVersion", System.Data.SqlDbType.Binary, 8) { Value = bill.RowVersion });

            if (await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not byte[] rowVersion)
            {
                throw new InvalidOperationException("This bill was changed or deleted after it was loaded. Your changes were not saved. Cancel this edit, refresh the bill list, and reopen it before saving again.");
            }

            return rowVersion;
        }

        private static void AddParameters(SqlCommand command, RecurringBill bill)
        {
            command.Parameters.Add(SqlParameters.Int("@AccountId", bill.AccountId));
            command.Parameters.Add(SqlParameters.NVarChar("@BillName", bill.BillName, 200));
            command.Parameters.Add(SqlParameters.NVarChar("@Payee", bill.Payee, 300));
            command.Parameters.Add(SqlParameters.NVarChar("@Category", bill.Category, 200));
            command.Parameters.Add(SqlParameters.NVarChar("@SpendingClass", bill.SpendingClass, 100));
            command.Parameters.Add(SqlParameters.NVarChar("@RequirementType", bill.RequirementType, 100));
            command.Parameters.Add(SqlParameters.Decimal("@TypicalAmount", bill.TypicalAmount));
            command.Parameters.Add(SqlParameters.Int("@DueDay", bill.DueDay));
            command.Parameters.Add(SqlParameters.Date("@NextDueDate", bill.NextDueDate));
            command.Parameters.Add(SqlParameters.NVarChar("@Frequency", bill.Frequency, 50));
            command.Parameters.Add(SqlParameters.Bit("@IsRequired", bill.IsRequired));
            command.Parameters.Add(SqlParameters.Bit("@IsActive", bill.IsActive));
            command.Parameters.Add(SqlParameters.NVarChar("@SourceDescription", bill.SourceDescription, 1000));
            command.Parameters.Add(SqlParameters.NVarChar("@PayeeAliasesJson", RecurringBillAliasJson.Write(bill.PayeeAliases), -1));
            command.Parameters.Add(SqlParameters.NVarChar("@SourceDescriptionAliasesJson", RecurringBillAliasJson.Write(bill.SourceDescriptionAliases), -1));
            command.Parameters.Add(SqlParameters.Date("@FirstSeenDate", bill.FirstSeenDate));
            command.Parameters.Add(SqlParameters.Date("@LastSeenDate", bill.LastSeenDate));
            command.Parameters.Add(SqlParameters.Int("@TransactionCount", bill.TransactionCount));
            command.Parameters.Add(SqlParameters.NVarChar("@Notes", bill.Notes, 1000));
        }

        private static RecurringBill MapBill(SqlDataReader reader)
        {
            return new RecurringBill
            {
                RecurringBillId = reader.GetInt32Value("RecurringBillId"),
                AccountId = reader.GetInt32OrNull("AccountId"),
                AccountNickname = reader.GetStringOrNull("AccountNickname"),
                BillName = reader.GetStringOrEmpty("BillName"),
                Payee = reader.GetStringOrEmpty("Payee"),
                Category = reader.GetStringOrNull("Category"),
                SpendingClass = reader.GetStringOrNull("SpendingClass"),
                RequirementType = reader.GetStringOrEmpty("RequirementType"),
                TypicalAmount = reader.GetDecimalValue("TypicalAmount"),
                DueDay = reader.GetInt32OrNull("DueDay"),
                NextDueDate = reader.GetDateOnlyOrNull("NextDueDate"),
                Frequency = reader.GetStringOrEmpty("Frequency"),
                IsRequired = reader.GetBooleanValue("IsRequired"),
                IsActive = reader.GetBooleanValue("IsActive"),
                SourceDescription = reader.GetStringOrNull("SourceDescription"),
                PayeeAliases = RecurringBillAliasJson.Read(reader.GetStringOrEmpty("PayeeAliasesJson")),
                SourceDescriptionAliases = RecurringBillAliasJson.Read(reader.GetStringOrEmpty("SourceDescriptionAliasesJson")),
                FirstSeenDate = reader.GetDateOnlyOrNull("FirstSeenDate"),
                LastSeenDate = reader.GetDateOnlyOrNull("LastSeenDate"),
                TransactionCount = reader.GetInt32Value("TransactionCount"),
                Notes = reader.GetStringOrNull("Notes"),
                CreatedAtUtc = reader.GetDateTimeValue("CreatedAtUtc"),
                UpdatedAtUtc = reader.GetDateTimeValue("UpdatedAtUtc"),
                RowVersion = reader.GetFieldValue<byte[]>(reader.GetOrdinal("RowVersion"))
            };
        }

        private static void Validate(RecurringBill bill)
        {
            if (bill.RecurringBillId != 0 && bill.RowVersion.Length != 8)
            {
                throw new InvalidOperationException("This bill needs to be reloaded before editing. Cancel this edit, refresh the bill list, and reopen it before saving again.");
            }

            if (string.IsNullOrWhiteSpace(bill.BillName))
            {
                throw new InvalidOperationException("Bill name is required.");
            }

            if (string.IsNullOrWhiteSpace(bill.Payee))
            {
                throw new InvalidOperationException("Payee is required.");
            }

            ValidateLength(bill.BillName, 200, "Bill name");
            ValidateLength(bill.Payee, 300, "Payee");
            ValidateLength(bill.SourceDescription, 1000, "Statement description");
            ValidateLength(bill.Category, 200, "Category");
            ValidateLength(bill.SpendingClass, 100, "Category type");
            ValidateLength(bill.RequirementType, 100, "Planning purpose");
            ValidateLength(bill.Notes, 1000, "Notes");
            bill.Frequency = BillSchedule.NormalizeFrequency(bill.Frequency);
            RecurringBillMatcher.NormalizeAliases(bill);

            if (bill.TypicalAmount < 0)
            {
                throw new InvalidOperationException("Typical amount cannot be negative.");
            }

            if (bill.DueDay.HasValue && (bill.DueDay.Value < 1 || bill.DueDay.Value > 31))
            {
                throw new InvalidOperationException("Due day must be between 1 and 31.");
            }
        }

        private static void ValidateLength(string? value, int maximumLength, string label)
        {
            if (value?.Length > maximumLength)
            {
                throw new InvalidOperationException($"{label} must be {maximumLength} characters or fewer.");
            }
        }
    }
}
