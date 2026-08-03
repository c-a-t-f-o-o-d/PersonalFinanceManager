using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PersonalFinanceManager.Core.Common;

namespace PersonalFinanceManager.Core.Imports
{
    public sealed class RegionsBankParser : ITransactionParser
    {
        private readonly CsvRecordReader _reader = new();

        public string ProfileCode => ImportProfileCodes.RegionsBankCsv;

        public async Task<ParseResult> ParseAsync(string filePath, CancellationToken cancellationToken = default)
        {
            string text = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            return ParseText(text);
        }

        public ParseResult ParseText(string text)
        {
            ParseResult result = new();
            IReadOnlyList<CsvRecord> records;

            try
            {
                records = _reader.Read(text).Where(record => record.Fields.Any(field => !string.IsNullOrWhiteSpace(field))).ToArray();
            }
            catch (FormatException ex)
            {
                result.Errors.Add(new ParseError(1, ex.Message));
                return result;
            }

            if (records.Count == 0)
            {
                result.Errors.Add(new ParseError(1, "The file is empty."));
                return result;
            }

            Dictionary<string, int> headers = BuildHeaderMap(records[0]);
            string[] requiredHeaders = ["Account", "Transaction Date", "Posted Date", "No.", "Description", "Debit", "Credit", "Long Description"];

            foreach (string requiredHeader in requiredHeaders)
            {
                if (!headers.ContainsKey(NormalizeHeader(requiredHeader)))
                {
                    result.Errors.Add(new ParseError(records[0].PhysicalStartLine, $"Missing required header '{requiredHeader}'."));
                    return result;
                }
            }

            Dictionary<string, int> occurrenceCounts = new(StringComparer.OrdinalIgnoreCase);

            for (int index = 1; index < records.Count; index++)
            {
                CsvRecord record = records[index];
                ParsedTransaction? transaction = TryParseRow(record, headers, out string? error);

                if (transaction is null)
                {
                    result.Errors.Add(new ParseError(record.PhysicalStartLine, error ?? "Unable to parse row."));
                    continue;
                }

                string baseExternalId = transaction.ExternalTransactionId ?? "REGIONS:UNKNOWN";
                occurrenceCounts.TryGetValue(baseExternalId, out int occurrence);
                occurrence++;
                occurrenceCounts[baseExternalId] = occurrence;
                transaction.ExternalTransactionId = $"{baseExternalId}:{occurrence.ToString(CultureInfo.InvariantCulture)}";
                result.Rows.Add(transaction);
            }

            return result;
        }

        private static Dictionary<string, int> BuildHeaderMap(CsvRecord headerRecord)
        {
            Dictionary<string, int> map = new(StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < headerRecord.Fields.Count; index++)
            {
                string header = NormalizeHeader(headerRecord.Fields[index]);

                if (!string.IsNullOrWhiteSpace(header) && !map.ContainsKey(header))
                {
                    map.Add(header, index);
                }
            }

            return map;
        }

        private static string NormalizeHeader(string value)
        {
            return value.Trim();
        }

        private static ParsedTransaction? TryParseRow(CsvRecord record, IReadOnlyDictionary<string, int> headers, out string? error)
        {
            error = null;
            string account = Get(record, headers, "Account").Trim();
            string transactionDateText = Get(record, headers, "Transaction Date").Trim();
            string postedDateText = Get(record, headers, "Posted Date").Trim();
            string number = Get(record, headers, "No.").Trim();
            string description = Get(record, headers, "Description").Trim();
            string debitText = Get(record, headers, "Debit").Trim();
            string creditText = Get(record, headers, "Credit").Trim();
            string longDescription = Get(record, headers, "Long Description").Trim();

            if (!DateOnly.TryParseExact(transactionDateText, "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly transactionDate))
            {
                error = $"Invalid transaction date '{transactionDateText}'.";
                return null;
            }

            DateOnly? postedDate = null;

            if (!string.IsNullOrWhiteSpace(postedDateText))
            {
                if (!DateOnly.TryParseExact(postedDateText, "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly parsedPostedDate))
                {
                    error = $"Invalid posted date '{postedDateText}'.";
                    return null;
                }

                postedDate = parsedPostedDate;
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                error = "Description is required.";
                return null;
            }

            if (!TryParseRegionsAmount(debitText, creditText, out decimal amount, out string type, out error))
            {
                return null;
            }

            (string? category, string? categoryType, bool isTransfer) = InferRegionsCategory(description, longDescription, amount);
            string? memo = BuildMemo(account, number, longDescription);

            return new ParsedTransaction
            {
                RowNumber = record.PhysicalStartLine,
                Date = transactionDate,
                PostingDate = postedDate,
                Amount = amount,
                Type = type,
                Description = description,
                Merchant = description,
                SourceCategory = category,
                SourceCategoryType = categoryType,
                FundingSource = isTransfer ? account : null,
                Memo = memo,
                ExternalTransactionId = BuildExternalId(account, transactionDate, postedDate, number, description, debitText, creditText, longDescription)
            };
        }

        private static bool TryParseRegionsAmount(string debitText, string creditText, out decimal amount, out string type, out string? error)
        {
            error = null;
            amount = 0M;
            type = string.Empty;
            bool hasDebit = !string.IsNullOrWhiteSpace(debitText);
            bool hasCredit = !string.IsNullOrWhiteSpace(creditText);

            if (hasDebit == hasCredit)
            {
                error = "Exactly one of Debit or Credit must be populated.";
                return false;
            }

            string amountText = hasDebit ? debitText : creditText;

            if (!decimal.TryParse(amountText, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out decimal parsedAmount))
            {
                error = $"Invalid amount '{amountText}'.";
                return false;
            }

            if (hasDebit)
            {
                amount = -Math.Abs(parsedAmount);
                type = "Withdrawal";
                return true;
            }

            amount = Math.Abs(parsedAmount);
            type = "Deposit";
            return true;
        }

        private static (string? Category, string? CategoryType, bool IsTransfer) InferRegionsCategory(string description, string longDescription, decimal amount)
        {
            string combined = TextNormalizer.NormalizeForComparison(description + " " + longDescription);

            if (combined.Contains("INTEREST PAYMENT", StringComparison.Ordinal))
            {
                return ("Interest", "Yield", false);
            }

            if (combined.Contains("SERVICE FEE", StringComparison.Ordinal) || combined.Contains("SERVICE ASSESSMENT", StringComparison.Ordinal))
            {
                return ("Fees", "Bank Fee", false);
            }

            if (combined.Contains("EB FROM CHECKING", StringComparison.Ordinal) || combined.Contains("EB TO SAVINGS", StringComparison.Ordinal))
            {
                return ("Transfer", "Checking -> Savings", true);
            }

            if (combined.Contains("EB TO CHECKING", StringComparison.Ordinal) || combined.Contains("EB FROM SAVINGS", StringComparison.Ordinal))
            {
                return ("Transfer", "Savings -> Checking", true);
            }

            if (combined.Contains("PAYROLL", StringComparison.Ordinal) || combined.Contains("DIR DEP", StringComparison.Ordinal))
            {
                return ("Payroll", "Payroll", false);
            }

            if (combined.Contains("ATM WITHDRAWAL", StringComparison.Ordinal))
            {
                return ("Cash", "ATM Withdrawal", false);
            }

            return (null, null, false);
        }

        private static string? BuildMemo(string account, string number, string longDescription)
        {
            List<string> parts = new();

            if (!string.IsNullOrWhiteSpace(account))
            {
                parts.Add("Regions Account: " + account.Trim());
            }

            if (!string.IsNullOrWhiteSpace(number))
            {
                parts.Add("No.: " + number.Trim());
            }

            if (!string.IsNullOrWhiteSpace(longDescription))
            {
                parts.Add("Long Description: " + longDescription.Trim());
            }

            return parts.Count == 0 ? null : string.Join(" | ", parts);
        }

        private static string BuildExternalId(string account, DateOnly transactionDate, DateOnly? postedDate, string number, string description, string debit, string credit, string longDescription)
        {
            string raw = string.Join("|",
                "REGIONS",
                TextNormalizer.NormalizeForComparison(account),
                transactionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                postedDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                TextNormalizer.NormalizeForComparison(number),
                TextNormalizer.NormalizeForComparison(description),
                TextNormalizer.NormalizeForComparison(debit),
                TextNormalizer.NormalizeForComparison(credit),
                TextNormalizer.NormalizeForComparison(longDescription));

            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return "REGIONS:" + Convert.ToHexString(hash)[..32];
        }

        private static string Get(CsvRecord record, IReadOnlyDictionary<string, int> headers, string header)
        {
            int index = headers[NormalizeHeader(header)];
            return index >= record.Fields.Count ? string.Empty : record.Fields[index];
        }
    }
}
