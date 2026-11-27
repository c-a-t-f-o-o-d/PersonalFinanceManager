using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PersonalFinanceManager.Core.Common;

namespace PersonalFinanceManager.Core.Imports
{

    public sealed class AppleCardParser : ITransactionParser
    {
        private readonly CsvRecordReader _reader = new();

        public string ProfileCode => ImportProfileCodes.AppleCardCsv;

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
            string[] requiredHeaders = ["Transaction Date", "Clearing Date", "Description", "Merchant", "Category", "Type", "Amount (USD)", "Purchased By"];

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

                string baseExternalId = transaction.ExternalTransactionId ?? "APPLECARD:UNKNOWN";
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
            string transactionDateText = Get(record, headers, "Transaction Date").Trim();
            string clearingDateText = Get(record, headers, "Clearing Date").Trim();
            string description = Get(record, headers, "Description").Trim();
            string merchant = Get(record, headers, "Merchant").Trim();
            string sourceCategory = Get(record, headers, "Category").Trim();
            string type = Get(record, headers, "Type").Trim();
            string amountText = Get(record, headers, "Amount (USD)").Trim();
            string purchasedBy = Get(record, headers, "Purchased By").Trim();

            if (!DateOnly.TryParseExact(transactionDateText, "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly transactionDate))
            {
                error = $"Invalid transaction date '{transactionDateText}'.";
                return null;
            }

            DateOnly? clearingDate = null;

            if (!string.IsNullOrWhiteSpace(clearingDateText))
            {
                if (!DateOnly.TryParseExact(clearingDateText, "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly parsedClearingDate))
                {
                    error = $"Invalid clearing date '{clearingDateText}'.";
                    return null;
                }

                clearingDate = parsedClearingDate;
            }

            if (!ImportAmountParser.TryParse(amountText, out decimal rawAmount))
            {
                error = $"Invalid amount '{amountText}'.";
                return null;
            }

            if (string.IsNullOrWhiteSpace(type))
            {
                error = "Transaction type is required.";
                return null;
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                error = "Description is required.";
                return null;
            }

            string? normalizedCategory = NormalizeCategory(sourceCategory, type);
            string? normalizedCategoryType = NormalizeCategoryType(sourceCategory, type);
            decimal normalizedAmount = NormalizeAmount(type, rawAmount);
            string? memo = BuildMemo(sourceCategory, purchasedBy, rawAmount);

            return new ParsedTransaction
            {
                RowNumber = record.PhysicalStartLine,
                Date = transactionDate,
                PostingDate = clearingDate,
                Amount = normalizedAmount,
                Type = type,
                Description = description,
                Merchant = string.IsNullOrWhiteSpace(merchant) ? null : merchant,
                SourceCategory = normalizedCategory,
                SourceCategoryType = normalizedCategoryType,
                Memo = memo,
                ExternalTransactionId = BuildExternalId(transactionDate, clearingDate, description, merchant, sourceCategory, type, rawAmount, purchasedBy)
            };
        }

        private static decimal NormalizeAmount(string type, decimal rawAmount)
        {
            return -rawAmount;
        }

        private static string? NormalizeCategory(string category, string type)
        {
            string normalizedType = TextNormalizer.NormalizeForComparison(type);
            string normalizedCategory = TextNormalizer.NormalizeForComparison(category);

            if (normalizedType == "PAYMENT" || normalizedCategory == "PAYMENT")
            {
                return "Transfer";
            }

            if (normalizedType is "CREDIT" or "RETURN" || normalizedCategory == "CREDIT")
            {
                return "Refund";
            }

            if (normalizedCategory == "RESTAURANTS")
            {
                return "Dining";
            }

            if (normalizedCategory is "TRANSPORTATION" or "AIRLINES")
            {
                return "Travel";
            }

            if (normalizedCategory == "SHOPPING")
            {
                return "Shopping";
            }

            if (normalizedCategory == "DEBIT")
            {
                return "Expense";
            }

            if (string.IsNullOrWhiteSpace(category) || normalizedCategory == "OTHER")
            {
                return null;
            }

            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(category.Trim().ToLowerInvariant());
        }

        private static string? NormalizeCategoryType(string category, string type)
        {
            string normalizedType = TextNormalizer.NormalizeForComparison(type);
            string normalizedCategory = TextNormalizer.NormalizeForComparison(category);

            if (normalizedType == "PAYMENT" || normalizedCategory == "PAYMENT")
            {
                return "Credit Card Payment";
            }

            if (normalizedType is "CREDIT" or "RETURN" || normalizedCategory == "CREDIT")
            {
                return "Credit Card Refund";
            }

            if (normalizedCategory == "RESTAURANTS")
            {
                return "Restaurant";
            }

            if (normalizedCategory == "AIRLINES")
            {
                return "Airline";
            }

            if (normalizedCategory == "TRANSPORTATION")
            {
                return "Transportation";
            }

            return null;
        }

        private static string? BuildMemo(string sourceCategory, string purchasedBy, decimal rawAmount)
        {
            List<string> parts = new();

            if (!string.IsNullOrWhiteSpace(sourceCategory))
            {
                parts.Add($"Apple Category: {sourceCategory}");
            }

            if (!string.IsNullOrWhiteSpace(purchasedBy))
            {
                parts.Add($"Purchased By: {purchasedBy}");
            }

            parts.Add("Original Apple Amount: " + rawAmount.ToString("0.00", CultureInfo.InvariantCulture));
            return parts.Count == 0 ? null : string.Join("; ", parts);
        }

        private static string BuildExternalId(DateOnly transactionDate, DateOnly? clearingDate, string description, string merchant, string category, string type, decimal rawAmount, string purchasedBy)
        {
            string raw = string.Join("|",
                "APPLECARD",
                transactionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                clearingDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                TextNormalizer.NormalizeForComparison(description),
                TextNormalizer.NormalizeForComparison(merchant),
                TextNormalizer.NormalizeForComparison(category),
                TextNormalizer.NormalizeForComparison(type),
                rawAmount.ToString("0.00", CultureInfo.InvariantCulture),
                TextNormalizer.NormalizeForComparison(purchasedBy));

            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return "APPLECARD:" + Convert.ToHexString(hash)[..32];
        }

        private static string Get(CsvRecord record, IReadOnlyDictionary<string, int> headers, string header)
        {
            int index = headers[NormalizeHeader(header)];
            return index >= record.Fields.Count ? string.Empty : record.Fields[index];
        }
    }
}
