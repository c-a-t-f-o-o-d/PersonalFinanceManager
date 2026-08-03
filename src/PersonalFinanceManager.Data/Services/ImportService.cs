using System.Security.Cryptography;
using PersonalFinanceManager.Core.Common;
using PersonalFinanceManager.Core.Imports;
using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Core.Rules;
using PersonalFinanceManager.Data.Repositories;
using PersonalFinanceManager.Data.Sql;
using Microsoft.Data.SqlClient;

namespace PersonalFinanceManager.Data.Services
{
    public sealed class ImportService
    {
        private static readonly SqlResourceReader _sqlResources = new();

        private readonly IReadOnlyList<ImportProfileOption> _profileOptions =
        [
            new ImportProfileOption(ImportProfileCodes.AllyCheckingCsv, "Ally Checking CSV", "CSV files (*.csv)|*.csv|All files (*.*)|*.*"),
            new ImportProfileOption(ImportProfileCodes.AppleCardCsv, "Apple Card CSV", "CSV files (*.csv)|*.csv|All files (*.*)|*.*"),
            new ImportProfileOption(ImportProfileCodes.ChaseCreditCardCsv, "Chase / Amazon Credit Card CSV", "CSV files (*.csv)|*.csv|All files (*.*)|*.*"),
            new ImportProfileOption(ImportProfileCodes.PayPalStatement, "PayPal Account Statement PDF/Text", "PayPal statements (*.pdf;*.txt)|*.pdf;*.txt|All files (*.*)|*.*"),
            new ImportProfileOption(ImportProfileCodes.RegionsBankCsv, "Regions Bank Checking/Savings CSV", "CSV files (*.csv)|*.csv|All files (*.*)|*.*")
        ];

        private readonly Dictionary<string, ITransactionParser> _parsers;
        private readonly RuleMatcher _ruleMatcher = new();
        private readonly RuleRepository _ruleRepository;
        private readonly SqlConnectionFactory _connectionFactory;

        public ImportService(SqlConnectionFactory connectionFactory, RuleRepository ruleRepository)
        {
            _connectionFactory = connectionFactory;
            _ruleRepository = ruleRepository;

            _parsers = new Dictionary<string, ITransactionParser>(StringComparer.OrdinalIgnoreCase)
            {
                [ImportProfileCodes.AllyCheckingCsv] = new AllyTransactionParser(),
                [ImportProfileCodes.AppleCardCsv] = new AppleCardParser(),
                [ImportProfileCodes.ChaseCreditCardCsv] = new ChaseCreditCardParser(),
                [ImportProfileCodes.PayPalStatement] = new PayPalStatementParser(),
                [ImportProfileCodes.RegionsBankCsv] = new RegionsBankParser()
            };
        }

        public IReadOnlyList<ImportProfileOption> GetProfiles()
        {
            return _profileOptions;
        }

        public Task<ImportSummary> PreviewAsync(int accountId, string filePath, CancellationToken cancellationToken = default)
        {
            return PreviewAsync(accountId, ImportProfileCodes.AllyCheckingCsv, filePath, cancellationToken);
        }

        public async Task<ImportSummary> PreviewAsync(int accountId, string profileCode, string filePath, CancellationToken cancellationToken = default)
        {
            ITransactionParser parser = GetParser(profileCode);
            ParseResult parse = await parser.ParseAsync(filePath, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<FinancialRule> rules = await _ruleRepository.GetActiveAsync(cancellationToken).ConfigureAwait(false);
            List<ImportedTransaction> rows = new();

            foreach (ParsedTransaction parsed in parse.Rows)
            {
                FinancialRule? rule = _ruleMatcher.Match(rules, parsed);
                rows.Add(ToImportedTransaction(accountId, 0, parsed, rule));
            }

            return new ImportSummary
            {
                ParsedRows = parse.Rows.Count,
                ErrorRows = parse.Errors.Count,
                PreviewRows = rows,
                ParseErrors = parse.Errors
            };
        }

        public Task<ImportSummary> ImportAsync(int accountId, string filePath, CancellationToken cancellationToken = default)
        {
            return ImportAsync(accountId, ImportProfileCodes.AllyCheckingCsv, filePath, cancellationToken);
        }

        public async Task<ImportSummary> ImportAsync(int accountId, string profileCode, string filePath, CancellationToken cancellationToken = default)
        {
            ITransactionParser parser = GetParser(profileCode);
            ParseResult parse = await parser.ParseAsync(filePath, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<FinancialRule> rules = await _ruleRepository.GetActiveAsync(cancellationToken).ConfigureAwait(false);
            string fileHash = await ComputeSha256Async(filePath, cancellationToken).ConfigureAwait(false);
            FileInfo file = new(filePath);
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            string? accountOwner = await GetAccountOwnerAsync(connection, transaction, accountId, cancellationToken).ConfigureAwait(false);

            ImportSummary summary = new()
            {
                ParsedRows = parse.Rows.Count,
                ErrorRows = parse.Errors.Count,
                ParseErrors = parse.Errors,
                FileHashPreviouslyImported = await FileHashExistsAsync(connection, transaction, fileHash, cancellationToken).ConfigureAwait(false)
            };

            try
            {
                long batchId = await CreateBatchAsync(connection, transaction, accountId, profileCode, file.Name, fileHash, cancellationToken).ConfigureAwait(false);
                summary.ImportBatchId = batchId;

                foreach (ParseError error in parse.Errors)
                {
                    await InsertImportRowAsync(connection, transaction, batchId, error.RowNumber, "Error", error.Message, null, null, DuplicateClassification.None, cancellationToken).ConfigureAwait(false);
                }

                HashSet<string> currentBatchExactKeys = new(StringComparer.OrdinalIgnoreCase);

                foreach (ParsedTransaction parsed in parse.Rows)
                {
                    FinancialRule? rule = _ruleMatcher.Match(rules, parsed);
                    ImportedTransaction imported = ToImportedTransaction(accountId, batchId, parsed, rule, accountOwner);
                    string rowHash = BuildRowHash(imported);

                    DuplicateClassification duplicate = currentBatchExactKeys.Add(rowHash)
                        ? await ClassifyDuplicateAsync(connection, transaction, imported, rowHash, cancellationToken).ConfigureAwait(false)
                        : DuplicateClassification.Exact(null, "Exact duplicate row inside the same import file.");

                    if (duplicate.IsDuplicate)
                    {
                        await InsertImportRowAsync(connection, transaction, batchId, parsed.RowNumber, duplicate.Status, null, parsed, imported, duplicate, cancellationToken).ConfigureAwait(false);
                        summary.DuplicateRows++;

                        if (duplicate.IsExact)
                        {
                            summary.ExactDuplicateRows++;
                        }
                        else
                        {
                            summary.DuplicateReviewRows++;
                        }

                        continue;
                    }

                    await InsertImportRowAsync(connection, transaction, batchId, parsed.RowNumber, "Imported", null, parsed, imported, duplicate, cancellationToken).ConfigureAwait(false);
                    await InsertTransactionAsync(connection, transaction, imported, rowHash, cancellationToken).ConfigureAwait(false);
                    summary.InsertedRows++;
                }

                summary.LinkedFundingRows = await LinkPayPalFundingTransactionsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                summary.NormalizedLinkedFundingRows = await NormalizeLinkedPayPalFundingRowsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                summary.NormalizedLikelyPayPalFundingRows = await NormalizeLikelyPayPalFundingRowsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                summary.CreditCardFundingLinks = await LinkCreditCardPaymentFundingTransactionsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                summary.NormalizedCreditCardPaymentRows = await NormalizeLinkedCreditCardPaymentRowsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                summary.NormalizedLikelyCreditCardPaymentRows = await NormalizeLikelyCreditCardPaymentRowsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                summary.CreditCardPaymentAllocations = await AllocateCreditCardPaymentsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                summary.BankTransferLinks = await LinkBankTransferTransactionsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                summary.NormalizedBankTransferRows = await NormalizeLinkedBankTransferRowsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                await UpdateBatchCountsAsync(connection, transaction, batchId, summary, cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return summary;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }

        public async Task<int> ReapplyRulesAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<FinancialRule> rules = await _ruleRepository.GetActiveAsync(cancellationToken).ConfigureAwait(false);
            List<(long ImportedTransactionId, ParsedTransaction Transaction, FinancialRule? Rule)> updates = new();
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using (SqlCommand select = connection.CreateCommand())
            {
                select.CommandText = _sqlResources.Read("ImportService/ReapplyRulesSelectTransactions.sql");

                await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    ParsedTransaction transaction = new()
                    {
                        Date = reader.GetDateOnlyOrNull("TransactionDate") ?? DateOnly.MinValue,
                        PostingDate = reader.GetDateOnlyOrNull("PostingDate"),
                        Time = reader.GetTimeOnlyOrNull("TransactionTime"),
                        Amount = reader.GetDecimalValue("Amount"),
                        Type = reader.GetStringOrEmpty("Type"),
                        Description = reader.GetStringOrEmpty("Description"),
                        ExternalTransactionId = reader.GetStringOrNull("ExternalTransactionId"),
                        FundingSource = reader.GetStringOrNull("FundingSource"),
                        SourceCategory = reader.GetStringOrNull("SourceCategory"),
                        SourceCategoryType = InferCategoryTypeFromImportedValues(reader.GetStringOrNull("SourceCategory"), reader.GetStringOrNull("Type")),
                        Merchant = reader.GetStringOrNull("Merchant"),
                        Memo = reader.GetStringOrNull("Memo")
                    };

                    FinancialRule? rule = _ruleMatcher.Match(rules, transaction);
                    updates.Add((reader.GetInt64Value("ImportedTransactionId"), transaction, rule));
                }
            }

            using SqlTransaction sqlTransaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                foreach (var update in updates)
                {
                    await UpdateImportedTransactionRuleAsync(connection, sqlTransaction, update.ImportedTransactionId, update.Transaction, update.Rule, cancellationToken).ConfigureAwait(false);
                }

                await NormalizeLinkedPayPalFundingRowsAsync(connection, sqlTransaction, cancellationToken).ConfigureAwait(false);
                await NormalizeLikelyPayPalFundingRowsAsync(connection, sqlTransaction, cancellationToken).ConfigureAwait(false);
                await NormalizeLinkedCreditCardPaymentRowsAsync(connection, sqlTransaction, cancellationToken).ConfigureAwait(false);
                await NormalizeLikelyCreditCardPaymentRowsAsync(connection, sqlTransaction, cancellationToken).ConfigureAwait(false);
                await sqlTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return updates.Count;
            }
            catch
            {
                await sqlTransaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }

        public async Task<DataFlowRepairSummary> RepairDataFlowAsync(CancellationToken cancellationToken = default)
        {
            await using SqlConnection connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                DataFlowRepairSummary summary = new()
                {
                    LinkedFundingRows = await LinkPayPalFundingTransactionsAsync(connection, transaction, cancellationToken).ConfigureAwait(false),
                    NormalizedLinkedFundingRows = await NormalizeLinkedPayPalFundingRowsAsync(connection, transaction, cancellationToken).ConfigureAwait(false),
                    NormalizedLikelyPayPalFundingRows = await NormalizeLikelyPayPalFundingRowsAsync(connection, transaction, cancellationToken).ConfigureAwait(false),
                    CreditCardFundingLinks = await LinkCreditCardPaymentFundingTransactionsAsync(connection, transaction, cancellationToken).ConfigureAwait(false),
                    NormalizedCreditCardPaymentRows = await NormalizeLinkedCreditCardPaymentRowsAsync(connection, transaction, cancellationToken).ConfigureAwait(false),
                    NormalizedLikelyCreditCardPaymentRows = await NormalizeLikelyCreditCardPaymentRowsAsync(connection, transaction, cancellationToken).ConfigureAwait(false),
                    CreditCardPaymentAllocations = await AllocateCreditCardPaymentsAsync(connection, transaction, cancellationToken).ConfigureAwait(false),
                    BankTransferLinks = await LinkBankTransferTransactionsAsync(connection, transaction, cancellationToken).ConfigureAwait(false),
                    NormalizedBankTransferRows = await NormalizeLinkedBankTransferRowsAsync(connection, transaction, cancellationToken).ConfigureAwait(false),
                    PotentialDuplicateGroups = await CountPotentialDuplicateGroupsAsync(connection, transaction, cancellationToken).ConfigureAwait(false)
                };

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return summary;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }

        private static async Task<string?> GetAccountOwnerAsync(SqlConnection connection, SqlTransaction transaction, int accountId, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = _sqlResources.Read("ImportService/GetAccountOwner.sql");
            command.Parameters.Add(SqlParameters.Int("@AccountId", accountId));
            object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return value == null || value == DBNull.Value ? null : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        private ITransactionParser GetParser(string profileCode)
        {
            if (_parsers.TryGetValue(profileCode, out ITransactionParser? parser))
            {
                return parser;
            }

            throw new InvalidOperationException($"Unsupported import profile '{profileCode}'.");
        }

        private static ImportedTransaction ToImportedTransaction(int accountId, long batchId, ParsedTransaction parsed, FinancialRule? rule, string? accountOwner = null)
        {
            bool buyerCreditPayment = parsed.Type.Contains("Buyer Credit Payment", StringComparison.OrdinalIgnoreCase);
            bool creditCardPayment = IsCreditCardPayment(parsed);
            bool creditCardReturn = IsCreditCardReturnOrCredit(parsed);
            bool creditCardFee = string.Equals(parsed.Type, "Fee", StringComparison.OrdinalIgnoreCase);
            bool creditCardAdjustment = string.Equals(parsed.Type, "Other", StringComparison.OrdinalIgnoreCase) && parsed.Amount > 0;
            bool bankTransfer = IsParsedTransfer(parsed);
            bool isIncome = rule?.IsIncome ?? (parsed.Amount > 0 && !buyerCreditPayment && !creditCardPayment && !creditCardReturn && !creditCardAdjustment && !bankTransfer);
            bool isTransfer = rule?.IsTransfer ?? (buyerCreditPayment || creditCardPayment || bankTransfer);

            return new ImportedTransaction
            {
                AccountId = accountId,
                ImportBatchId = batchId,
                TransactionDate = parsed.Date,
                PostingDate = parsed.PostingDate,
                TransactionTime = parsed.Time,
                Amount = parsed.Amount,
                Type = parsed.Type,
                Description = parsed.Description,
                ExternalTransactionId = parsed.ExternalTransactionId,
                FundingSource = parsed.FundingSource,
                SourceCategory = parsed.SourceCategory,
                Memo = parsed.Memo,
                AppliedRuleId = rule?.RuleId,
                Category = rule?.Category ?? GetDefaultCategory(parsed, buyerCreditPayment, creditCardPayment, creditCardReturn, bankTransfer),
                SpendingClass = rule?.SpendingClass ?? GetDefaultCategoryType(parsed, buyerCreditPayment, creditCardPayment, creditCardReturn, creditCardFee, bankTransfer),
                Merchant = rule?.Merchant ?? parsed.Merchant,
                IsIncome = isIncome,
                IsTransfer = isTransfer,
                IncomeOwner = ResolveIncomeOwner(rule?.IncomeOwner, accountOwner, isIncome)
            };
        }

        private static string? ResolveIncomeOwner(string? ruleIncomeOwner, string? accountOwner, bool isIncome)
        {
            string? normalizedRuleOwner = OwnerValues.Normalize(ruleIncomeOwner);

            if (!string.IsNullOrWhiteSpace(normalizedRuleOwner) && !string.Equals(normalizedRuleOwner, OwnerValues.Unassigned, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedRuleOwner;
            }

            string? normalizedAccountOwner = OwnerValues.Normalize(accountOwner);

            if (OwnerValues.IsAssignedOwner(normalizedAccountOwner))
            {
                return normalizedAccountOwner;
            }

            return null;
        }

        private static string? GetDefaultCategory(ParsedTransaction parsed, bool buyerCreditPayment, bool creditCardPayment, bool creditCardReturn, bool bankTransfer)
        {
            if (buyerCreditPayment || creditCardPayment || bankTransfer)
            {
                return "Transfer";
            }

            if (creditCardReturn)
            {
                return string.IsNullOrWhiteSpace(parsed.SourceCategory) ? "Refund" : parsed.SourceCategory.Trim();
            }

            return string.IsNullOrWhiteSpace(parsed.SourceCategory) ? null : parsed.SourceCategory.Trim();
        }

        private static string? GetDefaultCategoryType(ParsedTransaction parsed, bool buyerCreditPayment, bool creditCardPayment, bool creditCardReturn, bool creditCardFee, bool bankTransfer)
        {
            if (buyerCreditPayment)
            {
                return "PayPal Smart Connect Payment";
            }

            if (creditCardPayment)
            {
                return "Credit Card Payment";
            }

            if (bankTransfer)
            {
                return string.IsNullOrWhiteSpace(parsed.SourceCategoryType) ? "Bank Transfer" : parsed.SourceCategoryType.Trim();
            }

            if (creditCardReturn)
            {
                return string.IsNullOrWhiteSpace(parsed.SourceCategoryType) ? "Credit Card Refund" : parsed.SourceCategoryType.Trim();
            }

            if (creditCardFee)
            {
                return "Credit Card Fee";
            }

            if (!string.IsNullOrWhiteSpace(parsed.SourceCategoryType))
            {
                return parsed.SourceCategoryType.Trim();
            }

            return InferCategoryTypeFromImportedValues(parsed.SourceCategory, parsed.Type);
        }

        private static bool IsCreditCardPayment(ParsedTransaction parsed)
        {
            return string.Equals(parsed.Type, "Payment", StringComparison.OrdinalIgnoreCase) && parsed.Amount > 0;
        }

        private static bool IsParsedTransfer(ParsedTransaction parsed)
        {
            return string.Equals(parsed.SourceCategory, "Transfer", StringComparison.OrdinalIgnoreCase) || parsed.Type.Contains("Transfer", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCreditCardReturnOrCredit(ParsedTransaction parsed)
        {
            if (string.Equals(parsed.Type, "Return", StringComparison.OrdinalIgnoreCase) || string.Equals(parsed.Type, "Credit", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(parsed.Type, "Other", StringComparison.OrdinalIgnoreCase) && parsed.Amount > 0;
        }

        private static string? InferCategoryTypeFromImportedValues(string? category, string? type)
        {
            string normalizedCategory = TextNormalizer.NormalizeForComparison(category ?? string.Empty);
            string normalizedType = TextNormalizer.NormalizeForComparison(type ?? string.Empty);

            if (normalizedType == "PAYMENT" || normalizedCategory == "TRANSFER")
            {
                return "Credit Card Payment";
            }

            if (normalizedCategory == "DINING" || normalizedCategory == "RESTAURANTS")
            {
                return "Restaurant";
            }

            if (normalizedCategory == "REFUND" || normalizedType is "RETURN" or "CREDIT")
            {
                return "Credit Card Refund";
            }

            return null;
        }

        private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
        {
            await using FileStream stream = File.OpenRead(filePath);
            byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
            return Convert.ToHexString(hash);
        }

        private static async Task<bool> FileHashExistsAsync(SqlConnection connection, SqlTransaction transaction, string fileHash, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = _sqlResources.Read("ImportService/FileHashExists.sql");
            command.Parameters.Add(SqlParameters.NVarChar("@FileSha256", fileHash, 64));
            object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt32(value) > 0;
        }

        private static async Task<long> CreateBatchAsync(SqlConnection connection, SqlTransaction transaction, int accountId, string profileCode, string fileName, string fileHash, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = _sqlResources.Read("ImportService/CreateBatch.sql");

            command.Parameters.Add(SqlParameters.Int("@AccountId", accountId));
            command.Parameters.Add(SqlParameters.NVarChar("@ImportProfileCode", profileCode, 100));
            command.Parameters.Add(SqlParameters.NVarChar("@OriginalFileName", fileName, 260));
            command.Parameters.Add(SqlParameters.NVarChar("@FileSha256", fileHash, 64));
            object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt64(value);
        }

        private static async Task<DuplicateClassification> ClassifyDuplicateAsync(SqlConnection connection, SqlTransaction transaction, ImportedTransaction imported, string rowHash, CancellationToken cancellationToken)
        {
            long? exactMatchId = await FindExactDuplicateTransactionIdAsync(connection, transaction, imported, rowHash, cancellationToken).ConfigureAwait(false);

            if (exactMatchId.HasValue)
            {
                return DuplicateClassification.Exact(exactMatchId, "All import fields match an existing transaction.");
            }

            if (!string.IsNullOrWhiteSpace(imported.ExternalTransactionId))
            {
                long? externalMatchId = await FindQuestionableExternalIdMatchAsync(connection, transaction, imported, cancellationToken).ConfigureAwait(false);

                if (externalMatchId.HasValue)
                {
                    return DuplicateClassification.Review(externalMatchId, "Same external transaction id, but one or more imported fields differ.");
                }
            }

            (long? MatchId, string Reason) broadMatch = await FindQuestionableDateAmountMatchAsync(connection, transaction, imported, cancellationToken).ConfigureAwait(false);

            if (broadMatch.MatchId.HasValue)
            {
                return DuplicateClassification.Review(broadMatch.MatchId, broadMatch.Reason);
            }

            long? crossAccountMatchId = await FindCrossAccountExactDataMatchAsync(connection, transaction, imported, cancellationToken).ConfigureAwait(false);

            if (crossAccountMatchId.HasValue)
            {
                return DuplicateClassification.Review(crossAccountMatchId, "Same transaction date, amount, type, and description already exists under a different account. Verify whether the wrong account was selected during import or whether both entries are legitimate.");
            }

            return DuplicateClassification.None;
        }

        private static async Task<long?> FindExactDuplicateTransactionIdAsync(SqlConnection connection, SqlTransaction transaction, ImportedTransaction imported, string rowHash, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = _sqlResources.Read("ImportService/FindExactDuplicateTransactionId.sql");

            AddDuplicateParameters(command, imported, rowHash);
            object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return value is null or DBNull ? null : Convert.ToInt64(value);
        }

        private static async Task<long?> FindQuestionableExternalIdMatchAsync(SqlConnection connection, SqlTransaction transaction, ImportedTransaction imported, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = _sqlResources.Read("ImportService/FindQuestionableExternalIdMatch.sql");

            command.Parameters.Add(SqlParameters.Int("@AccountId", imported.AccountId));
            command.Parameters.Add(SqlParameters.BigInt("@ImportBatchId", imported.ImportBatchId));
            command.Parameters.Add(SqlParameters.NVarChar("@ExternalTransactionId", imported.ExternalTransactionId, 100));
            object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return value is null or DBNull ? null : Convert.ToInt64(value);
        }

        private static async Task<(long? MatchId, string Reason)> FindQuestionableDateAmountMatchAsync(SqlConnection connection, SqlTransaction transaction, ImportedTransaction imported, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = _sqlResources.Read("ImportService/FindQuestionableDateAmountMatch.sql");

            command.Parameters.Add(SqlParameters.Int("@AccountId", imported.AccountId));
            command.Parameters.Add(SqlParameters.BigInt("@ImportBatchId", imported.ImportBatchId));
            command.Parameters.Add(SqlParameters.Date("@TransactionDate", imported.TransactionDate));
            command.Parameters.Add(SqlParameters.Decimal("@Amount", imported.Amount));
            command.Parameters.Add(SqlParameters.NVarChar("@Type", imported.Type, 100));
            command.Parameters.Add(SqlParameters.NVarChar("@Description", imported.Description, 1000));
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return (null, string.Empty);
            }

            return (reader.GetInt64Value("ImportedTransactionId"), reader.GetStringOrEmpty("DuplicateReason"));
        }

        private static async Task<long?> FindCrossAccountExactDataMatchAsync(SqlConnection connection, SqlTransaction transaction, ImportedTransaction imported, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = _sqlResources.Read("ImportService/FindCrossAccountExactDataMatch.sql");

            command.Parameters.Add(SqlParameters.Int("@AccountId", imported.AccountId));
            command.Parameters.Add(SqlParameters.BigInt("@ImportBatchId", imported.ImportBatchId));
            command.Parameters.Add(SqlParameters.Date("@TransactionDate", imported.TransactionDate));
            command.Parameters.Add(SqlParameters.Decimal("@Amount", imported.Amount));
            command.Parameters.Add(SqlParameters.NVarChar("@Type", imported.Type, 100));
            command.Parameters.Add(SqlParameters.NVarChar("@Description", imported.Description, 1000));
            command.Parameters.Add(SqlParameters.NVarChar("@Memo", imported.Memo, 1000));
            object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return value is null or DBNull ? null : Convert.ToInt64(value);
        }

        private static void AddDuplicateParameters(SqlCommand command, ImportedTransaction imported, string rowHash)
        {
            command.Parameters.Add(SqlParameters.Int("@AccountId", imported.AccountId));
            command.Parameters.Add(SqlParameters.BigInt("@ImportBatchId", imported.ImportBatchId));
            command.Parameters.Add(SqlParameters.NVarChar("@RawRowHash", rowHash, 64));
            command.Parameters.Add(SqlParameters.Date("@TransactionDate", imported.TransactionDate));
            command.Parameters.Add(SqlParameters.Date("@PostingDate", imported.PostingDate));
            command.Parameters.Add(SqlParameters.Time("@TransactionTime", imported.TransactionTime));
            command.Parameters.Add(SqlParameters.Decimal("@Amount", imported.Amount));
            command.Parameters.Add(SqlParameters.NVarChar("@Type", imported.Type, 100));
            command.Parameters.Add(SqlParameters.NVarChar("@Description", imported.Description, 1000));
            command.Parameters.Add(SqlParameters.NVarChar("@ExternalTransactionId", imported.ExternalTransactionId, 100));
            command.Parameters.Add(SqlParameters.NVarChar("@FundingSource", imported.FundingSource, 200));
            command.Parameters.Add(SqlParameters.NVarChar("@SourceCategory", imported.SourceCategory, 200));
            command.Parameters.Add(SqlParameters.NVarChar("@Memo", imported.Memo, 1000));
        }

        private static async Task InsertImportRowAsync(SqlConnection connection, SqlTransaction transaction, long batchId, int rowNumber, string status, string? errorMessage, ParsedTransaction? parsed, ImportedTransaction? imported, DuplicateClassification duplicate, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = _sqlResources.Read("ImportService/InsertImportRow.sql");

            command.Parameters.Add(SqlParameters.BigInt("@ImportBatchId", batchId));
            command.Parameters.Add(SqlParameters.Int("@RowNumber", rowNumber));
            command.Parameters.Add(SqlParameters.NVarChar("@Status", status, 30));
            command.Parameters.Add(SqlParameters.NVarChar("@ErrorMessage", errorMessage, 1000));
            command.Parameters.Add(SqlParameters.Date("@TransactionDate", parsed?.Date));
            command.Parameters.Add(SqlParameters.Date("@PostingDate", parsed?.PostingDate));
            command.Parameters.Add(SqlParameters.Time("@TransactionTime", parsed?.Time));
            command.Parameters.Add(SqlParameters.Decimal("@Amount", parsed?.Amount));
            command.Parameters.Add(SqlParameters.NVarChar("@Type", parsed?.Type, 100));
            command.Parameters.Add(SqlParameters.NVarChar("@Description", parsed?.Description, 1000));
            command.Parameters.Add(SqlParameters.NVarChar("@ExternalTransactionId", parsed?.ExternalTransactionId, 100));
            command.Parameters.Add(SqlParameters.NVarChar("@FundingSource", parsed?.FundingSource, 200));
            command.Parameters.Add(SqlParameters.NVarChar("@SourceCategory", parsed?.SourceCategory, 200));
            command.Parameters.Add(SqlParameters.NVarChar("@Memo", parsed?.Memo, 1000));
            command.Parameters.Add(SqlParameters.Int("@AppliedRuleId", imported?.AppliedRuleId));
            command.Parameters.Add(SqlParameters.NVarChar("@Category", imported?.Category, 200));
            command.Parameters.Add(SqlParameters.NVarChar("@SpendingClass", imported?.SpendingClass, 100));
            command.Parameters.Add(SqlParameters.NVarChar("@Merchant", imported?.Merchant, 200));
            command.Parameters.Add(SqlParameters.Bit("@IsIncome", imported?.IsIncome));
            command.Parameters.Add(SqlParameters.Bit("@IsTransfer", imported?.IsTransfer));
            command.Parameters.Add(SqlParameters.NVarChar("@IncomeOwner", OwnerValues.Normalize(imported?.IncomeOwner), 50));
            command.Parameters.Add(SqlParameters.BigInt("@DuplicateMatchTransactionId", duplicate.MatchTransactionId));
            command.Parameters.Add(SqlParameters.NVarChar("@DuplicateReason", duplicate.Reason, 1000));
            command.Parameters.Add(SqlParameters.NVarChar("@DuplicateConfidence", duplicate.Confidence, 30));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static async Task InsertTransactionAsync(SqlConnection connection, SqlTransaction transaction, ImportedTransaction imported, string rowHash, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = _sqlResources.Read("ImportService/InsertTransaction.sql");

            command.Parameters.Add(SqlParameters.Int("@AccountId", imported.AccountId));
            command.Parameters.Add(SqlParameters.BigInt("@ImportBatchId", imported.ImportBatchId));
            command.Parameters.Add(SqlParameters.Date("@TransactionDate", imported.TransactionDate));
            command.Parameters.Add(SqlParameters.Date("@PostingDate", imported.PostingDate));
            command.Parameters.Add(SqlParameters.Time("@TransactionTime", imported.TransactionTime));
            command.Parameters.Add(SqlParameters.Decimal("@Amount", imported.Amount));
            command.Parameters.Add(SqlParameters.NVarChar("@Type", imported.Type, 100));
            command.Parameters.Add(SqlParameters.NVarChar("@Description", imported.Description, 1000));
            command.Parameters.Add(SqlParameters.NVarChar("@ExternalTransactionId", imported.ExternalTransactionId, 100));
            command.Parameters.Add(SqlParameters.NVarChar("@FundingSource", imported.FundingSource, 200));
            command.Parameters.Add(SqlParameters.NVarChar("@SourceCategory", imported.SourceCategory, 200));
            command.Parameters.Add(SqlParameters.NVarChar("@Memo", imported.Memo, 1000));
            command.Parameters.Add(SqlParameters.Int("@AppliedRuleId", imported.AppliedRuleId));
            command.Parameters.Add(SqlParameters.NVarChar("@Category", imported.Category, 200));
            command.Parameters.Add(SqlParameters.NVarChar("@SpendingClass", imported.SpendingClass, 100));
            command.Parameters.Add(SqlParameters.NVarChar("@Merchant", imported.Merchant, 200));
            command.Parameters.Add(SqlParameters.Bit("@IsIncome", imported.IsIncome));
            command.Parameters.Add(SqlParameters.Bit("@IsTransfer", imported.IsTransfer));
            command.Parameters.Add(SqlParameters.NVarChar("@IncomeOwner", OwnerValues.Normalize(imported.IncomeOwner), 50));
            command.Parameters.Add(SqlParameters.NVarChar("@RawRowHash", rowHash, 64));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static async Task<int> LinkPayPalFundingTransactionsAsync(SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            List<(long Id, DateOnly Date, decimal Amount, string Description, string Type, string FundingSource)> payPalRows = new();

            await using (SqlCommand select = connection.CreateCommand())
            {
                select.Transaction = transaction;
                select.CommandText = _sqlResources.Read("ImportService/SelectPayPalTransactionsForFundingLinking.sql");

                await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    payPalRows.Add((
                        reader.GetInt64Value("ImportedTransactionId"),
                        reader.GetDateOnlyOrNull("TransactionDate") ?? DateOnly.MinValue,
                        reader.GetDecimalValue("Amount"),
                        reader.GetStringOrEmpty("Description"),
                        reader.GetStringOrEmpty("Type"),
                        reader.GetStringOrEmpty("FundingSource")));
                }
            }

            int linkedCount = 0;

            foreach ((long payPalId, DateOnly date, decimal amount, string description, string type, string fundingSource) in payPalRows)
            {
                if (!IsAllyFundingSource(fundingSource))
                {
                    continue;
                }

                long? bankTransactionId = await FindBankFundingTransactionAsync(connection, transaction, payPalId, date, amount, cancellationToken).ConfigureAwait(false);

                if (!bankTransactionId.HasValue)
                {
                    continue;
                }

                await InsertTransactionLinkAsync(connection, transaction, payPalId, bankTransactionId.Value, "PayPalFunding", null, 95, $"Matched PayPal {type}: {description}", cancellationToken).ConfigureAwait(false);
                await MarkFundingTransferTransactionAsync(connection, transaction, bankTransactionId.Value, "PayPal Funding", "Auto-linked as PayPal funding transfer.", cancellationToken).ConfigureAwait(false);
                linkedCount++;
            }

            return linkedCount;
        }

        private static async Task<long?> FindBankFundingTransactionAsync(SqlConnection connection, SqlTransaction transaction, long payPalTransactionId, DateOnly payPalDate, decimal payPalAmount, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = _sqlResources.Read("ImportService/FindBankFundingTransaction.sql");

            command.Parameters.Add(SqlParameters.BigInt("@PayPalTransactionId", payPalTransactionId));
            command.Parameters.Add(SqlParameters.Decimal("@Amount", payPalAmount));
            command.Parameters.Add(SqlParameters.Date("@FromDate", payPalDate.AddDays(-5)));
            command.Parameters.Add(SqlParameters.Date("@ToDate", payPalDate.AddDays(7)));
            command.Parameters.Add(SqlParameters.Date("@PayPalDate", payPalDate));
            object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return value is null or DBNull ? null : Convert.ToInt64(value);
        }

        private static async Task<int> LinkCreditCardPaymentFundingTransactionsAsync(SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            List<(long Id, int AccountId, DateOnly Date, decimal Amount, string Description)> cardPayments = new();

            await using (SqlCommand select = connection.CreateCommand())
            {
                select.Transaction = transaction;
                select.CommandText = _sqlResources.Read("ImportService/SelectCreditCardPaymentsForFundingLinking.sql");

                await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    cardPayments.Add((
                        reader.GetInt64Value("ImportedTransactionId"),
                        reader.GetInt32Value("AccountId"),
                        reader.GetDateOnlyOrNull("TransactionDate") ?? DateOnly.MinValue,
                        reader.GetDecimalValue("Amount"),
                        reader.GetStringOrEmpty("Description")));
                }
            }

            int linkedCount = 0;

            foreach ((long paymentId, int accountId, DateOnly date, decimal amount, string description) in cardPayments)
            {
                long? bankTransactionId = await FindCreditCardBankPaymentTransactionAsync(connection, transaction, paymentId, accountId, date, amount, cancellationToken).ConfigureAwait(false);

                if (!bankTransactionId.HasValue)
                {
                    await MarkFundingTransferTransactionAsync(connection, transaction, paymentId, "Credit Card Payment", "Marked credit card payment row as transfer.", cancellationToken).ConfigureAwait(false);
                    continue;
                }

                await InsertTransactionLinkAsync(connection, transaction, paymentId, bankTransactionId.Value, "CreditCardPaymentFunding", null, 90, $"Matched credit card payment: {description}", cancellationToken).ConfigureAwait(false);
                await MarkFundingTransferTransactionAsync(connection, transaction, paymentId, "Credit Card Payment", "Auto-linked as credit card payment transfer.", cancellationToken).ConfigureAwait(false);
                await MarkFundingTransferTransactionAsync(connection, transaction, bankTransactionId.Value, "Credit Card Payment", "Auto-linked as credit card payment funding transfer.", cancellationToken).ConfigureAwait(false);
                linkedCount++;
            }

            return linkedCount;
        }

        private static async Task<long?> FindCreditCardBankPaymentTransactionAsync(SqlConnection connection, SqlTransaction transaction, long cardPaymentTransactionId, int cardAccountId, DateOnly paymentDate, decimal paymentAmount, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = _sqlResources.Read("ImportService/FindCreditCardBankPaymentTransaction.sql");

            command.Parameters.Add(SqlParameters.BigInt("@CardPaymentTransactionId", cardPaymentTransactionId));
            command.Parameters.Add(SqlParameters.Int("@CardAccountId", cardAccountId));
            command.Parameters.Add(SqlParameters.Decimal("@PaymentAmount", paymentAmount));
            command.Parameters.Add(SqlParameters.Date("@FromDate", paymentDate.AddDays(-7)));
            command.Parameters.Add(SqlParameters.Date("@ToDate", paymentDate.AddDays(7)));
            command.Parameters.Add(SqlParameters.Date("@PaymentDate", paymentDate));
            object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return value is null or DBNull ? null : Convert.ToInt64(value);
        }

        private static async Task<int> AllocateCreditCardPaymentsAsync(SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            List<(long PaymentId, int AccountId, DateOnly Date, decimal Amount)> payments = new();

            await using (SqlCommand select = connection.CreateCommand())
            {
                select.Transaction = transaction;

                select.CommandText = _sqlResources.Read("ImportService/SelectCreditCardPaymentsForAllocation.sql");

                await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    payments.Add((
                        reader.GetInt64Value("ImportedTransactionId"),
                        reader.GetInt32Value("AccountId"),
                        reader.GetDateOnlyOrNull("TransactionDate") ?? DateOnly.MinValue,
                        reader.GetDecimalValue("Amount")));
                }
            }

            int allocationCount = 0;

            foreach ((long paymentId, int accountId, DateOnly paymentDate, decimal paymentAmount) in payments)
            {
                decimal remaining = paymentAmount - await GetPaymentAllocatedAmountAsync(connection, transaction, paymentId, cancellationToken).ConfigureAwait(false);

                if (remaining <= 0)
                {
                    continue;
                }

                IReadOnlyList<(long ChargeId, decimal OpenAmount)> charges = await GetOpenCreditCardChargesAsync(connection, transaction, accountId, paymentDate, cancellationToken).ConfigureAwait(false);

                foreach ((long chargeId, decimal openAmount) in charges)
                {
                    if (remaining <= 0)
                    {
                        break;
                    }

                    decimal allocation = Math.Min(remaining, openAmount);

                    if (allocation <= 0)
                    {
                        continue;
                    }

                    bool inserted = await InsertTransactionLinkAsync(connection, transaction, paymentId, chargeId, "CreditCardPaymentAllocation", allocation, 70, "FIFO allocation of credit card payment to prior card charge.", cancellationToken).ConfigureAwait(false);

                    if (inserted)
                    {
                        remaining -= allocation;
                        allocationCount++;
                    }
                }
            }

            return allocationCount;
        }

        private static async Task<decimal> GetPaymentAllocatedAmountAsync(SqlConnection connection, SqlTransaction transaction, long paymentId, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = _sqlResources.Read("ImportService/GetPaymentAllocatedAmount.sql");

            command.Parameters.Add(SqlParameters.BigInt("@PaymentId", paymentId));
            object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return value is null or DBNull ? 0M : Convert.ToDecimal(value);
        }

        private static async Task<IReadOnlyList<(long ChargeId, decimal OpenAmount)>> GetOpenCreditCardChargesAsync(SqlConnection connection, SqlTransaction transaction, int accountId, DateOnly paymentDate, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = _sqlResources.Read("ImportService/GetOpenCreditCardCharges.sql");

            command.Parameters.Add(SqlParameters.Int("@AccountId", accountId));
            command.Parameters.Add(SqlParameters.Date("@PaymentDate", paymentDate));
            List<(long ChargeId, decimal OpenAmount)> charges = new();
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                charges.Add((reader.GetInt64Value("ImportedTransactionId"), reader.GetDecimalValue("OpenAmount")));
            }

            return charges;
        }

        private static async Task<bool> InsertTransactionLinkAsync(SqlConnection connection, SqlTransaction transaction, long sourceTransactionId, long linkedTransactionId, string linkType, decimal? allocationAmount, int confidenceScore, string? notes, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = _sqlResources.Read("ImportService/InsertTransactionLink.sql");

            command.Parameters.Add(SqlParameters.BigInt("@SourceTransactionId", sourceTransactionId));
            command.Parameters.Add(SqlParameters.BigInt("@LinkedTransactionId", linkedTransactionId));
            command.Parameters.Add(SqlParameters.NVarChar("@LinkType", linkType, 100));
            command.Parameters.Add(SqlParameters.Decimal("@AllocationAmount", allocationAmount));
            command.Parameters.Add(SqlParameters.Int("@ConfidenceScore", confidenceScore));
            command.Parameters.Add(SqlParameters.NVarChar("@Notes", notes, 1000));
            object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return value is bool inserted && inserted;
        }

        private static async Task MarkFundingTransferTransactionAsync(SqlConnection connection, SqlTransaction transaction, long transactionId, string categoryType, string noteText, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = _sqlResources.Read("ImportService/MarkFundingTransferTransaction.sql");

            command.Parameters.Add(SqlParameters.NVarChar("@CategoryType", categoryType, 100));
            command.Parameters.Add(SqlParameters.NVarChar("@NoteText", noteText, 1000));
            command.Parameters.Add(SqlParameters.BigInt("@ImportedTransactionId", transactionId));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static async Task<int> NormalizeLinkedPayPalFundingRowsAsync(SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = _sqlResources.Read("ImportService/NormalizeLinkedPayPalFundingRows.sql");

            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static async Task<int> NormalizeLikelyPayPalFundingRowsAsync(SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = _sqlResources.Read("ImportService/NormalizeLikelyPayPalFundingRows.sql");

            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static async Task<int> NormalizeLinkedCreditCardPaymentRowsAsync(SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = _sqlResources.Read("ImportService/NormalizeLinkedCreditCardPaymentRows.sql");

            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static async Task<int> NormalizeLikelyCreditCardPaymentRowsAsync(SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = _sqlResources.Read("ImportService/NormalizeLikelyCreditCardPaymentRows.sql");

            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }


        private static async Task<int> LinkBankTransferTransactionsAsync(SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            List<(long Id, DateOnly Date, decimal Amount)> transfers = new();
            await using (SqlCommand select = connection.CreateCommand())
            {
                select.Transaction = transaction;

                select.CommandText = _sqlResources.Read("ImportService/SelectBankTransferSources.sql");

                await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    transfers.Add((reader.GetInt64Value("ImportedTransactionId"), reader.GetDateOnlyOrNull("TransactionDate") ?? DateOnly.MinValue, reader.GetDecimalValue("Amount")));
                }
            }

            int linkedCount = 0;

            foreach ((long sourceId, DateOnly sourceDate, decimal sourceAmount) in transfers)
            {
                long? matchId = await FindBankTransferMatchAsync(connection, transaction, sourceId, sourceDate, sourceAmount, cancellationToken).ConfigureAwait(false);

                if (!matchId.HasValue)
                {
                    continue;
                }

                bool inserted = await InsertTransactionLinkAsync(connection, transaction, sourceId, matchId.Value, "BankTransfer", null, 85, "Auto-linked as bank transfer between owned accounts.", cancellationToken).ConfigureAwait(false);

                if (inserted)
                {
                    await MarkFundingTransferTransactionAsync(connection, transaction, sourceId, "Bank Transfer", "Auto-linked as bank transfer.", cancellationToken).ConfigureAwait(false);
                    await MarkFundingTransferTransactionAsync(connection, transaction, matchId.Value, "Bank Transfer", "Auto-linked as bank transfer.", cancellationToken).ConfigureAwait(false);
                    linkedCount++;
                }
            }

            return linkedCount;
        }

        private static async Task<long?> FindBankTransferMatchAsync(SqlConnection connection, SqlTransaction transaction, long sourceTransactionId, DateOnly sourceDate, decimal sourceAmount, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = _sqlResources.Read("ImportService/FindBankTransferMatch.sql");

            command.Parameters.Add(SqlParameters.BigInt("@SourceTransactionId", sourceTransactionId));
            command.Parameters.Add(SqlParameters.Decimal("@SourceAmount", sourceAmount));
            command.Parameters.Add(SqlParameters.Date("@FromDate", sourceDate.AddDays(-3)));
            command.Parameters.Add(SqlParameters.Date("@ToDate", sourceDate.AddDays(5)));
            command.Parameters.Add(SqlParameters.Date("@SourceDate", sourceDate));
            object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return value is null or DBNull ? null : Convert.ToInt64(value);
        }

        private static async Task<int> NormalizeLinkedBankTransferRowsAsync(SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = _sqlResources.Read("ImportService/NormalizeLinkedBankTransferRows.sql");

            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static async Task<int> CountPotentialDuplicateGroupsAsync(SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = _sqlResources.Read("ImportService/CountPotentialDuplicateGroups.sql");

            object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt32(value);
        }

        private static async Task UpdateImportedTransactionRuleAsync(SqlConnection connection, SqlTransaction transaction, long importedTransactionId, ParsedTransaction parsed, FinancialRule? rule, CancellationToken cancellationToken)
        {
            bool buyerCreditPayment = parsed.Type.Contains("Buyer Credit Payment", StringComparison.OrdinalIgnoreCase);
            bool creditCardPayment = IsCreditCardPayment(parsed);
            bool creditCardReturn = IsCreditCardReturnOrCredit(parsed);
            bool creditCardFee = string.Equals(parsed.Type, "Fee", StringComparison.OrdinalIgnoreCase);
            bool creditCardAdjustment = string.Equals(parsed.Type, "Other", StringComparison.OrdinalIgnoreCase) && parsed.Amount > 0;
            bool bankTransfer = IsParsedTransfer(parsed);
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = _sqlResources.Read("ImportService/UpdateImportedTransactionRule.sql");

            command.Parameters.Add(SqlParameters.Int("@AppliedRuleId", rule?.RuleId));
            command.Parameters.Add(SqlParameters.NVarChar("@Category", rule?.Category ?? GetDefaultCategory(parsed, buyerCreditPayment, creditCardPayment, creditCardReturn, bankTransfer), 200));
            command.Parameters.Add(SqlParameters.NVarChar("@SpendingClass", rule?.SpendingClass ?? GetDefaultCategoryType(parsed, buyerCreditPayment, creditCardPayment, creditCardReturn, creditCardFee, bankTransfer), 100));
            command.Parameters.Add(SqlParameters.NVarChar("@Merchant", rule?.Merchant ?? parsed.Merchant, 200));
            command.Parameters.Add(SqlParameters.Bit("@IsIncome", rule?.IsIncome ?? (parsed.Amount > 0 && !buyerCreditPayment && !creditCardPayment && !creditCardReturn && !creditCardAdjustment && !bankTransfer)));
            command.Parameters.Add(SqlParameters.Bit("@IsTransfer", rule?.IsTransfer ?? (buyerCreditPayment || creditCardPayment || bankTransfer)));
            command.Parameters.Add(SqlParameters.NVarChar("@IncomeOwner", OwnerValues.Normalize(rule?.IncomeOwner), 50));
            command.Parameters.Add(SqlParameters.BigInt("@ImportedTransactionId", importedTransactionId));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static async Task UpdateBatchCountsAsync(SqlConnection connection, SqlTransaction transaction, long batchId, ImportSummary summary, CancellationToken cancellationToken)
        {
            await using SqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = _sqlResources.Read("ImportService/UpdateBatchCounts.sql");

            command.Parameters.Add(SqlParameters.Int("@TotalRowCount", summary.ParsedRows + summary.ErrorRows));
            command.Parameters.Add(SqlParameters.Int("@ImportedRowCount", summary.InsertedRows));
            command.Parameters.Add(SqlParameters.Int("@DuplicateRowCount", summary.DuplicateRows));
            command.Parameters.Add(SqlParameters.Int("@ErrorRowCount", summary.ErrorRows));
            command.Parameters.Add(SqlParameters.BigInt("@ImportBatchId", batchId));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static bool IsAllyFundingSource(string fundingSource)
        {
            return fundingSource.Contains("ALLY", StringComparison.OrdinalIgnoreCase) && fundingSource.Contains("CHECK", StringComparison.OrdinalIgnoreCase);
        }


        private sealed class DuplicateClassification
        {
            private DuplicateClassification(bool isDuplicate, bool isExact, long? matchTransactionId, string status, string? reason, string? confidence)
            {
                IsDuplicate = isDuplicate;
                IsExact = isExact;
                MatchTransactionId = matchTransactionId;
                Status = status;
                Reason = reason;
                Confidence = confidence;
            }

            public static DuplicateClassification None { get; } = new(false, false, null, "Imported", null, null);

            public bool IsDuplicate { get; }
            public bool IsExact { get; }
            public long? MatchTransactionId { get; }
            public string Status { get; }
            public string? Reason { get; }
            public string? Confidence { get; }

            public static DuplicateClassification Exact(long? matchTransactionId, string reason)
            {
                return new DuplicateClassification(true, true, matchTransactionId, "ExactDuplicateSkipped", reason, "Exact");
            }

            public static DuplicateClassification Review(long? matchTransactionId, string reason)
            {
                return new DuplicateClassification(true, false, matchTransactionId, "DuplicateReview", reason, "Needs Review");
            }
        }

        private static string BuildRowHash(ImportedTransaction transaction)
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

            byte[] hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text));
            return Convert.ToHexString(hash);
        }
    }
}
