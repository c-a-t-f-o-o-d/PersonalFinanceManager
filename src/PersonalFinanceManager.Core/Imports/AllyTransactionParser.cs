using System.Globalization;

namespace PersonalFinanceManager.Core.Imports
{
    public sealed class AllyTransactionParser : ITransactionParser
    {
        private readonly CsvRecordReader _reader = new();

        public string ProfileCode => ImportProfileCodes.AllyCheckingCsv;

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
            string[] requiredHeaders = ["Date", "Time", "Amount", "Type", "Description"];

            foreach (string requiredHeader in requiredHeaders)
            {
                if (!headers.ContainsKey(NormalizeHeader(requiredHeader)))
                {
                    result.Errors.Add(new ParseError(records[0].PhysicalStartLine, $"Missing required header '{requiredHeader}'."));
                    return result;
                }
            }

            ImportOccurrenceIdentity identities = new();

            for (int index = 1; index < records.Count; index++)
            {
                CsvRecord record = records[index];
                ParsedTransaction? transaction = TryParseRow(record, headers, out string? error);

                if (transaction is null)
                {
                    result.Errors.Add(new ParseError(record.PhysicalStartLine, error ?? "Unable to parse row."));
                    continue;
                }

                transaction.ExternalTransactionId = identities.Next("ALLY", transaction);
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
            string dateText = Get(record, headers, "Date");
            string timeText = Get(record, headers, "Time");
            string amountText = Get(record, headers, "Amount");
            string type = Get(record, headers, "Type");
            string description = Get(record, headers, "Description");

            if (!DateOnly.TryParseExact(dateText.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
            {
                error = $"Invalid transaction date '{dateText}'.";
                return null;
            }

            TimeOnly? time = null;

            if (!string.IsNullOrWhiteSpace(timeText))
            {
                if (!TimeOnly.TryParseExact(timeText.Trim(), "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly parsedTime))
                {
                    error = $"Invalid transaction time '{timeText}'.";
                    return null;
                }

                time = parsedTime;
            }

            if (!ImportAmountParser.TryParse(amountText, out decimal amount))
            {
                error = $"Invalid amount '{amountText}'.";
                return null;
            }

            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(description))
            {
                error = "Transaction type and description are required.";
                return null;
            }

            return new ParsedTransaction
            {
                RowNumber = record.PhysicalStartLine,
                Date = date,
                Time = time,
                Amount = amount,
                Type = type.Trim(),
                Description = description.Trim()
            };
        }

        private static string Get(CsvRecord record, IReadOnlyDictionary<string, int> headers, string header)
        {
            int index = headers[header];
            return index >= record.Fields.Count ? string.Empty : record.Fields[index];
        }
    }
}
