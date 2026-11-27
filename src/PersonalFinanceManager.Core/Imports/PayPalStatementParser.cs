using System.Globalization;
using System.Text.RegularExpressions;

namespace PersonalFinanceManager.Core.Imports;

public sealed class PayPalStatementParser : ITransactionParser
{
    private static readonly Regex SplitYearPattern = new(@"(?<prefix>\d{2}/\d{2}/\d{3})\s+(?<suffix>\d)", RegexOptions.CultureInvariant);
    private static readonly Regex DateHeaderPattern = new(@"(?m)^[ \t]*(?<date>\d{2}/\d{2}/\d{4})(?=\s|$)", RegexOptions.CultureInvariant);
    private static readonly Regex TotalsPattern = new(@"\b(?<currency>USD|EUR|GBP|CAD|AUD|JPY|CHF|CNY|INR|MXN)\s+(?<amount>\S+)\s+(?<fees>\S+)\s+(?<total>\S+)", RegexOptions.CultureInvariant);
    private static readonly Regex FundingPattern = new(@"(?m)^[ \t]*(?<source>[^\r\n]+?)[ \t]*(?:\n[ \t]*|[ \t]+)(?:(?<amount>[+-]?[\d,]+(?:\.\d{1,2})?)[ \t]*(?:\n[ \t]*|[ \t]+))?USD[ \t]*(?:\n[ \t]*|[ \t]+)ID:[ \t]*(?<id>[A-Za-z0-9]+)(?=\s|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex IdPattern = new(@"\bID:\s*(?<id>[A-Za-z0-9]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private readonly MinimalPdfTextExtractor _pdfTextExtractor = new();

    public string ProfileCode => ImportProfileCodes.PayPalStatement;

    public async Task<ParseResult> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.Equals(Path.GetExtension(filePath), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return ParseText(_pdfTextExtractor.ExtractText(filePath));
        }

        return ParseText(await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false));
    }

    public ParseResult ParseText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        ParseResult result = new();
        result.Warnings.Add(new ParseError(0, "Imports purchase/account activity, not a complete PayPal balance ledger; do not use these rows alone for wallet balance reconciliation."));
        string section = GetAccountActivitySection(NormalizeText(text));
        MatchCollection headers = DateHeaderPattern.Matches(section);
        List<(ParsedTransaction Row, List<FundingDetail> Details, int IdCount)> rows = new();

        for (int index = 0; index < headers.Count; index++)
        {
            Match header = headers[index];
            int end = index + 1 < headers.Count ? headers[index + 1].Index : section.Length;
            string block = section[(header.Index + header.Length)..end].Trim();
            int rowNumber = section[..header.Index].Count(character => character == '\n') + 1;
            Match totals = TotalsPattern.Match(block);
            if (!DateOnly.TryParseExact(header.Groups["date"].Value, "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date) ||
                !totals.Success ||
                !ImportAmountParser.TryParse(totals.Groups["amount"].Value, out decimal amount) ||
                !ImportAmountParser.TryParse(totals.Groups["fees"].Value, out decimal fees) ||
                !ImportAmountParser.TryParse(totals.Groups["total"].Value, out decimal total))
            {
                result.Errors.Add(new ParseError(rowNumber, "Unable to read this PayPal transaction's date or amount/fees/total. Review the statement or use the normalized activity CSV format."));
                continue;
            }

            if (!string.Equals(totals.Groups["currency"].Value, "USD", StringComparison.Ordinal))
            {
                result.Errors.Add(new ParseError(rowNumber, "Only USD activity is supported. Convert and reconcile other currencies before importing."));
                continue;
            }

            if (amount + fees != total && amount - fees != total)
            {
                result.Errors.Add(new ParseError(rowNumber, "The PayPal amount, fees, and total do not reconcile."));
                continue;
            }

            string sourceDescription = NormalizeWhitespace(block[..totals.Index]);
            (string type, string description, bool recognized) = NormalizeDescription(block[..totals.Index], total);
            if (string.IsNullOrWhiteSpace(description) || sourceDescription.Length > 1000)
            {
                result.Errors.Add(new ParseError(rowNumber, "The PayPal transaction description is missing or exceeds 1,000 characters."));
                continue;
            }

            if (!recognized && total > 0)
            {
                result.Errors.Add(new ParseError(rowNumber, "Positive PayPal activity has an unrecognized type and was not imported. Use normalized CSV with an explicit Deposit, Refund, Transfer, or Payment type. Source: " + sourceDescription[..Math.Min(sourceDescription.Length, 600)]));
                continue;
            }

            if (!recognized)
            {
                result.Warnings.Add(new ParseError(rowNumber, "This PayPal activity type is unrecognized. The debit remains visible; review its category and whether it is an account transfer. Its original descriptor is retained in Memo."));
            }

            string tail = block[(totals.Index + totals.Length)..];
            ParsedTransaction transaction = new()
            {
                RowNumber = rowNumber,
                Date = date,
                Amount = total,
                Type = type,
                Description = description,
                Memo = sourceDescription
            };
            List<FundingDetail> details = FundingPattern.Matches(tail).Select(match => new FundingDetail(
                NormalizeWhitespace(match.Groups["source"].Value),
                match.Groups["id"].Value,
                ImportAmountParser.TryParse(match.Groups["amount"].Value, out decimal fundingAmount) ? fundingAmount : null)).ToList();
            rows.Add((transaction, details, IdPattern.Matches(tail).Count));
            result.Rows.Add(transaction);
        }

        AttachFunding(rows, result);
        ImportOccurrenceIdentity identities = new();
        foreach (ParsedTransaction row in result.Rows)
        {
            row.ExternalTransactionId ??= identities.Next("PAYPALSTATEMENT", row);
        }

        if (headers.Count == 0)
        {
            result.Errors.Add(new ParseError(1, "No dated PayPal transaction records were found."));
        }

        return result;
    }

    private static void AttachFunding(List<(ParsedTransaction Row, List<FundingDetail> Details, int IdCount)> rows, ParseResult result)
    {
        bool hasEarlierInlineFunding = rows.Take(Math.Max(0, rows.Count - 1)).Any(row => row.Details.Count == 1 && row.IdCount == 1);
        bool detached = rows.Count > 1 && !hasEarlierInlineFunding;
        if (detached)
        {
            List<FundingDetail> details = rows.SelectMany(row => row.Details).ToList();
            foreach (FundingDetail detail in details)
            {
                if (!detail.Amount.HasValue || result.Errors.Count > 0)
                {
                    continue;
                }

                List<ParsedTransaction> matches = rows.Select(row => row.Row).Where(row =>
                    row.Amount < 0 && Math.Abs(row.Amount) == Math.Abs(detail.Amount.Value)).ToList();
                int equalFundingAmounts = details.Count(other => other.Amount.HasValue && Math.Abs(other.Amount.Value) == Math.Abs(detail.Amount.Value));
                if (matches.Count == 1 && equalFundingAmounts == 1)
                {
                    Assign(matches[0], detail);
                }
            }

            foreach (var row in rows.Where(row => row.Row.ExternalTransactionId is null))
            {
                result.Warnings.Add(new ParseError(row.Row.RowNumber, "Detached or missing PayPal funding details could not be matched uniquely. The transaction was retained without funding metadata; review Transaction Links or use normalized CSV with Transaction ID and Funding Source."));
            }

            return;
        }

        foreach (var row in rows)
        {
            if (row.Details.Count == 1 && row.IdCount == 1 &&
                (!row.Details[0].Amount.HasValue || Math.Abs(row.Details[0].Amount!.Value) == Math.Abs(row.Row.Amount)))
            {
                Assign(row.Row, row.Details[0]);
            }
            else
            {
                result.Warnings.Add(new ParseError(row.Row.RowNumber, "Funding metadata is missing or ambiguous for this PayPal transaction. The transaction was retained; review Transaction Links before combining account activity."));
            }
        }
    }

    private static void Assign(ParsedTransaction row, FundingDetail detail)
    {
        row.FundingSource = detail.Source;
        row.ExternalTransactionId = detail.Id;
    }

    private static string NormalizeText(string text)
    {
        string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        normalized = SplitYearPattern.Replace(normalized, match => match.Groups["prefix"].Value + match.Groups["suffix"].Value);
        normalized = Regex.Replace(normalized, @"(?m)^(?<date>[\dC]{2}/[\dC]{2}/[\dC]{4})(?=\s|$)", match => match.Value.Replace('C', '4'));
        normalized = normalized.Replace("PPYPPL", "PAYPAL", StringComparison.Ordinal)
            .Replace("PCCOUNc", "ACCOUNT", StringComparison.Ordinal)
            .Replace("PCcIeIcY", "ACTIVITY", StringComparison.Ordinal)
            .Replace("PLLY BPNK", "ALLY BANK", StringComparison.Ordinal)
            .Replace("Chtcking", "Checking", StringComparison.Ordinal)
            .Replace("Conntct", "Connect", StringComparison.Ordinal)
            .Replace("PrtPpprovts Paymtnt Bill Ustr Paymtnt", "PreApproved Payment Bill User Payment", StringComparison.Ordinal)
            .Replace("Buytr Crtsit Paymtnt", "Buyer Credit Payment", StringComparison.Ordinal);
        return Regex.Replace(normalized, @"[ \t]+", " ");
    }

    private static string GetAccountActivitySection(string text)
    {
        Match header = Regex.Match(text, @"PAYPAL\s+ACCOUNT\s+ACCOUNT\s+ACTIVITY\s+DATE", RegexOptions.IgnoreCase);
        int start = header.Success ? header.Index : text.IndexOf("ACCOUNT ACTIVITY", StringComparison.OrdinalIgnoreCase);
        start = Math.Max(0, start);
        int end = text.Length;
        foreach (string marker in new[] { "*For each transaction", "PAYPAL BALANCE ACCOUNT" })
        {
            int markerIndex = text.IndexOf(marker, start + 1, StringComparison.OrdinalIgnoreCase);
            if (markerIndex >= 0)
            {
                end = Math.Min(end, markerIndex);
            }
        }

        return text[start..end];
    }

    private static (string Type, string Description, bool Recognized) NormalizeDescription(string text, decimal amount)
    {
        string description = NormalizeWhitespace(text);
        const string preapproved = "PreApproved Payment Bill User Payment:";
        if (description.StartsWith(preapproved, StringComparison.OrdinalIgnoreCase))
        {
            return ("PreApproved Payment", description[preapproved.Length..].Trim(), amount <= 0);
        }

        if (description.StartsWith("Buyer Credit Payment", StringComparison.OrdinalIgnoreCase))
        {
            return ("Buyer Credit Payment", description, true);
        }

        if (new[] { "Refund", "Payment Refund", "General Payment Refund" }.Any(prefix => StartsWithDescriptor(description, prefix)))
        {
            return ("Refund", description, true);
        }

        if (new[] { "Transfer from Bank", "Transfer to Bank", "Bank Deposit to PP Account", "Bank Deposit to PayPal Account", "General Credit Card Deposit" }.Any(prefix => StartsWithDescriptor(description, prefix)))
        {
            return ("Transfer", description, true);
        }

        if (amount > 0 && StartsWithDescriptor(description, "Payment Received"))
        {
            return ("Deposit", description, true);
        }

        if (amount < 0 && new[] { "Purchase", "Express Checkout Payment", "Website Payment", "Mobile Payment" }.Any(prefix => StartsWithDescriptor(description, prefix)))
        {
            return ("Purchase", description, true);
        }

        return ("PayPal Unclassified", description, false);
    }

    private static bool StartsWithDescriptor(string text, string descriptor) => text.Equals(descriptor, StringComparison.OrdinalIgnoreCase)
        || text.StartsWith(descriptor + ":", StringComparison.OrdinalIgnoreCase)
        || text.StartsWith(descriptor + " ", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeWhitespace(string text) => Regex.Replace(text.Trim(), @"\s+", " ");

    private sealed record FundingDetail(string Source, string Id, decimal? Amount);
}
