using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PersonalFinanceManager.Core.Common;

namespace PersonalFinanceManager.Core.Imports
{
    public sealed class ChaseCreditCardParser : ITransactionParser
    {
        private readonly CsvRecordReader _reader = new();

        public string ProfileCode => ImportProfileCodes.ChaseCreditCardCsv;

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
            string[] requiredHeaders = ["Transaction Date", "Post Date", "Description", "Category", "Type", "Amount", "Memo"];

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

                string baseExternalId = transaction.ExternalTransactionId ?? "CHASE:UNKNOWN";
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
            string postDateText = Get(record, headers, "Post Date").Trim();
            string description = Get(record, headers, "Description").Trim();
            string category = Get(record, headers, "Category").Trim();
            string type = Get(record, headers, "Type").Trim();
            string amountText = Get(record, headers, "Amount").Trim();
            string memo = Get(record, headers, "Memo").Trim();

            if (!DateOnly.TryParseExact(transactionDateText, "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly transactionDate))
            {
                error = $"Invalid transaction date '{transactionDateText}'.";
                return null;
            }

            DateOnly? postDate = null;

            if (!string.IsNullOrWhiteSpace(postDateText))
            {
                if (!DateOnly.TryParseExact(postDateText, "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly parsedPostDate))
                {
                    error = $"Invalid post date '{postDateText}'.";
                    return null;
                }

                postDate = parsedPostDate;
            }

            if (!ImportAmountParser.TryParse(amountText, out decimal amount))
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

            return new ParsedTransaction
            {
                RowNumber = record.PhysicalStartLine,
                Date = transactionDate,
                PostingDate = postDate,
                Amount = amount,
                Type = type,
                Description = description,
                SourceCategory = string.IsNullOrWhiteSpace(category) ? null : category,
                Memo = string.IsNullOrWhiteSpace(memo) ? null : memo,
                ExternalTransactionId = BuildExternalId(transactionDate, postDate, description, category, type, amount, memo)
            };
        }

        private static string BuildExternalId(DateOnly transactionDate, DateOnly? postDate, string description, string category, string type, decimal amount, string memo)
        {
            string raw = string.Join("|",
                "CHASE",
                transactionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                postDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                TextNormalizer.NormalizeForComparison(description),
                TextNormalizer.NormalizeForComparison(category),
                TextNormalizer.NormalizeForComparison(type),
                amount.ToString("0.00", CultureInfo.InvariantCulture),
                TextNormalizer.NormalizeForComparison(memo));

            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return "CHASE:" + Convert.ToHexString(hash)[..32];
        }

        private static string Get(CsvRecord record, IReadOnlyDictionary<string, int> headers, string header)
        {
            int index = headers[NormalizeHeader(header)];
            return index >= record.Fields.Count ? string.Empty : record.Fields[index];
        }
    }
}
