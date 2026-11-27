using System.Globalization;

namespace PersonalFinanceManager.Core.Imports;

public class AccountActivityCsvParser : ITransactionParser
{
    private readonly CsvRecordReader _reader = new();
    public virtual string ProfileCode => ImportProfileCodes.AccountActivityCsv;
    protected virtual bool IsCreditAccount => false;

    public async Task<ParseResult> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return ParseText(await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false));
    }

    public ParseResult ParseText(string text)
    {
        ParseResult result = new();
        IReadOnlyList<CsvRecord> records;
        try
        {
            records = _reader.Read(text).Where(record => record.Fields.Any(field => !string.IsNullOrWhiteSpace(field))).ToArray();
        }
        catch (FormatException exception)
        {
            result.Errors.Add(new ParseError(1, exception.Message));
            return result;
        }

        if (records.Count == 0)
        {
            result.Errors.Add(new ParseError(1, "The file is empty."));
            return result;
        }

        Dictionary<string, int> headers = new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < records[0].Fields.Count; index++)
        {
            string header = records[0].Fields[index].Trim().TrimStart('\uFEFF');
            if (!headers.TryAdd(header, index))
            {
                result.Errors.Add(new ParseError(records[0].PhysicalStartLine, $"The header '{header}' occurs more than once."));
                return result;
            }
        }

        foreach (string required in new[] { "Date", "Amount", "Type", "Description" })
        {
            if (!headers.ContainsKey(required))
            {
                result.Errors.Add(new ParseError(records[0].PhysicalStartLine, $"Missing '{required}'. Use the documented normalized activity CSV format; native provider CSV layouts are not inferred."));
            }
        }

        if (result.Errors.Count > 0)
        {
            return result;
        }

        ImportOccurrenceIdentity identities = new();
        Dictionary<string, ParsedTransaction> sourceIds = new(StringComparer.OrdinalIgnoreCase);
        foreach (CsvRecord record in records.Skip(1))
        {
            ParsedTransaction? transaction = ParseRow(record, headers, out string? error);
            if (transaction is null)
            {
                result.Errors.Add(new ParseError(record.PhysicalStartLine, error ?? "Unable to parse row."));
                continue;
            }

            if (transaction.ExternalTransactionId is string id && sourceIds.TryGetValue(id, out ParsedTransaction? previous) &&
                (previous.Date != transaction.Date || previous.Amount != transaction.Amount ||
                 !string.Equals(previous.Type, transaction.Type, StringComparison.OrdinalIgnoreCase) ||
                 !string.Equals(previous.Description, transaction.Description, StringComparison.OrdinalIgnoreCase)))
            {
                result.Errors.Add(new ParseError(record.PhysicalStartLine, "The same Transaction ID has conflicting values in this file. Resolve the source conflict before importing this row."));
                continue;
            }

            transaction.ExternalTransactionId ??= identities.Next(ProfileCode, transaction);
            sourceIds[transaction.ExternalTransactionId] = transaction;
            result.Rows.Add(transaction);
        }

        return result;
    }

    private ParsedTransaction? ParseRow(CsvRecord record, IReadOnlyDictionary<string, int> headers, out string? error)
    {
        error = null;
        string Get(string name) => headers.TryGetValue(name, out int index) && index < record.Fields.Count ? record.Fields[index].Trim() : string.Empty;
        string? Optional(string name) => string.IsNullOrWhiteSpace(Get(name)) ? null : Get(name);
        string dateText = Get("Date");
        string amountText = Get("Amount");
        string type = Get("Type");
        string description = Get("Description");
        if (!TryParseDate(dateText, out DateOnly date))
        {
            error = "Date must be yyyy-MM-dd or MM/dd/yyyy.";
            return null;
        }

        if (!ImportAmountParser.TryParse(amountText, out decimal amount))
        {
            error = "Amount must be a signed USD amount with no more than two decimal places. Purchases/debits are negative; deposits, refunds and credit-account payments are positive.";
            return null;
        }

        if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(description))
        {
            error = "Type and Description are required.";
            return null;
        }

        string? currency = Optional("Currency");
        if (currency is not null && !currency.Equals("USD", StringComparison.OrdinalIgnoreCase))
        {
            error = "Only USD activity is supported.";
            return null;
        }

        if (!ValidateDirection(type, amount, out error))
        {
            return null;
        }

        DateOnly? postingDate = null;
        if (Optional("Posting Date") is string postingText)
        {
            if (!TryParseDate(postingText, out DateOnly parsedDate))
            {
                error = "Posting Date must be yyyy-MM-dd or MM/dd/yyyy.";
                return null;
            }

            postingDate = parsedDate;
        }

        TimeOnly? time = null;
        if (Optional("Time") is string timeText)
        {
            if (!TimeOnly.TryParseExact(timeText, new[] { "HH:mm:ss", "HH:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly parsedTime))
            {
                error = "Time must be HH:mm:ss or HH:mm.";
                return null;
            }

            time = parsedTime;
        }

        string? externalId = Optional("Transaction ID");
        if (externalId?.Length > 100 || description.Length > 1000 || type.Length > 100 || Get("Funding Source").Length > 200 || Get("Memo").Length > 1000 ||
            Get("Category").Length > 200 || Get("Category Type").Length > 100 || Get("Merchant").Length > 200)
        {
            error = "A transaction ID, description, type, funding source, or memo exceeds the supported field length.";
            return null;
        }

        return new ParsedTransaction
        {
            RowNumber = record.PhysicalStartLine,
            Date = date,
            PostingDate = postingDate,
            Time = time,
            Amount = amount,
            Type = type,
            Description = description,
            SourceAccountIdentity = Optional("Account"),
            Currency = "USD",
            ExternalTransactionId = externalId,
            FundingSource = Optional("Funding Source"),
            SourceCategory = Optional("Category"),
            SourceCategoryType = Optional("Category Type"),
            Merchant = Optional("Merchant"),
            Memo = Optional("Memo")
        };
    }

    private bool ValidateDirection(string type, decimal amount, out string? error)
    {
        string normalized = type.Trim().ToUpperInvariant();
        bool debit = normalized is "PURCHASE" or "SALE" or "DEBIT" or "WITHDRAWAL" or "FEE" or "INTEREST CHARGE";
        bool credit = normalized is "DEPOSIT" or "REFUND" or "RETURN" or "CREDIT" or "CASHBACK";
        if ((debit && amount > 0) || (credit && amount < 0) || (IsCreditAccount && normalized == "PAYMENT" && amount < 0))
        {
            error = "Amount sign conflicts with Type. Normalized activity uses negative charges and positive credits; a credit-account payment is positive.";
            return false;
        }

        if (IsCreditAccount && normalized is not ("PURCHASE" or "SALE" or "DEBIT" or "FEE" or "INTEREST CHARGE" or "PAYMENT" or "REFUND" or "RETURN" or "CREDIT" or "CASHBACK" or "ADJUSTMENT"))
        {
            error = "Normalized credit-account Type must be Purchase, Sale, Debit, Fee, Interest Charge, Payment, Refund, Return, Credit, Cashback, or Adjustment.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryParseDate(string text, out DateOnly date) => DateOnly.TryParseExact(text, new[] { "yyyy-MM-dd", "MM/dd/yyyy", "M/d/yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}

public class CreditAccountCsvParser : AccountActivityCsvParser
{
    public override string ProfileCode => ImportProfileCodes.CreditAccountCsv;
    protected override bool IsCreditAccount => true;
}

public sealed class PayPalSmartConnectCsvParser : CreditAccountCsvParser
{
    public override string ProfileCode => ImportProfileCodes.PayPalSmartConnectCsv;
}
