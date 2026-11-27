using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PersonalFinanceManager.Core.Imports;

public sealed class QifParser : ITransactionParser
{
    public string ProfileCode => ImportProfileCodes.Qif;

    public async Task<ParseResult> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            return ParseText(await File.ReadAllTextAsync(filePath, new UTF8Encoding(false, true), cancellationToken).ConfigureAwait(false));
        }
        catch (DecoderFallbackException)
        {
            ParseResult result = new();
            result.Errors.Add(new ParseError(0, "Save the QIF file as UTF-8 before importing; its text encoding could not be decoded safely."));
            return result;
        }
    }

    public ParseResult ParseText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        ParseResult result = new() { Currency = "USD" };
        result.Warnings.Add(new ParseError(0, "This QIF profile uses USD and month/day/four-digit-year dates. QIF does not declare currency; verify the account currency and date format before import."));
        string? section = null;
        string? declaredAccountType = null;
        bool accountSeen = false;
        bool transactionsSeen = false;
        List<(int Number, string Text)> record = new();
        ImportOccurrenceIdentity identities = new();
        string[] lines = text.TrimStart('\uFEFF').Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index].TrimEnd();
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.StartsWith('!'))
            {
                if (record.Count > 0) return Fail(result, index + 1, "A QIF entry is missing its terminating ^ line.");
                if (line.Equals("!Option:AllXfr", StringComparison.OrdinalIgnoreCase)) continue;
                if (line.Equals("!Account", StringComparison.OrdinalIgnoreCase))
                {
                    if (accountSeen || transactionsSeen) return Fail(result, index + 1, "Import one QIF account at a time; multiple account sections are not supported.");
                    accountSeen = true;
                    section = "ACCOUNT";
                    continue;
                }
                string? type = line.ToUpperInvariant() switch
                {
                    "!TYPE:BANK" => "BANK", "!TYPE:CCARD" => "CREDITCARD", "!TYPE:CASH" => "CASH", _ => null
                };
                if (type is null) return Fail(result, index + 1, "Only QIF Bank, Cash, and CCard transaction sections are supported. Export one account without category, investment, or memorized-transaction lists.");
                if (transactionsSeen) return Fail(result, index + 1, "Export one QIF transaction section per file.");
                if (accountSeen && string.IsNullOrWhiteSpace(result.SourceAccountIdentity)) return Fail(result, index + 1, "The QIF account section has no account name.");
                if (declaredAccountType is not null && declaredAccountType != type) return Fail(result, index + 1, "The QIF account and transaction section types disagree.");
                result.SourceAccountType = type;
                section = type;
                transactionsSeen = true;
                continue;
            }
            if (section is null) return Fail(result, index + 1, "The file must begin with a QIF account or transaction header.");
            if (line == "^")
            {
                if (record.Count == 0) return Fail(result, index + 1, "The file contains an empty QIF entry.");
                if (section == "ACCOUNT")
                {
                    if (result.SourceAccountIdentity is not null) return Fail(result, index + 1, "Import one QIF account at a time.");
                    string[] names = record.Where(field => field.Text.StartsWith('N')).Select(field => field.Text[1..].Trim()).ToArray();
                    string[] types = record.Where(field => field.Text.StartsWith('T')).Select(field => field.Text[1..].Trim().ToUpperInvariant()).ToArray();
                    if (names.Length != 1 || names[0].Length == 0 || types.Length != 1)
                        return Fail(result, record[0].Number, "The QIF account entry must contain one name and one account type.");
                    declaredAccountType = types[0] switch { "BANK" => "BANK", "CCARD" => "CREDITCARD", "CASH" => "CASH", _ => null };
                    if (declaredAccountType is null) return Fail(result, record[0].Number, "The QIF account type is not supported.");
                    result.SourceAccountIdentity = names[0];
                    result.SourceAccountType = declaredAccountType;
                }
                else ParseRecord(result, record, identities);
                record.Clear();
                continue;
            }
            record.Add((index + 1, line));
        }
        if (record.Count > 0) return Fail(result, record[0].Number, "The last QIF entry is missing its terminating ^ line.");
        if (!transactionsSeen) return Fail(result, 1, "No supported QIF transaction section was found.");
        return result;
    }

    private void ParseRecord(ParseResult result, IReadOnlyList<(int Number, string Text)> record, ImportOccurrenceIdentity identities)
    {
        int line = record[0].Number;
        Dictionary<char, string> fields = new();
        List<string> splitCategories = new();
        List<string> splitAmounts = new();
        List<string> splitMemos = new();
        foreach ((int number, string text) in record)
        {
            char key = text[0];
            string value = text[1..].Trim();
            if (key == 'A') continue;
            if (key == 'S') { splitCategories.Add(value); continue; }
            if (key == '$') { splitAmounts.Add(value); continue; }
            if (key == 'E') { splitMemos.Add(value); continue; }
            if ("DTCNPMLU".IndexOf(key) < 0 || !fields.TryAdd(key, value))
            {
                result.Errors.Add(new ParseError(number, "The QIF entry contains an unsupported or repeated field. Normalize this entry explicitly before importing."));
                return;
            }
        }
        string Get(char key) => fields.GetValueOrDefault(key, string.Empty);
        string dateText = Regex.Replace(Get('D'), @"\s+", string.Empty);
        if (!DateOnly.TryParseExact(dateText, new[] { "M/d/yyyy", "MM/dd/yyyy", "yyyy-MM-dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
        {
            result.Errors.Add(new ParseError(line, "QIF dates must use month/day/four-digit-year or yyyy-MM-dd. Two-digit years and locale-dependent dates must be normalized explicitly."));
            return;
        }
        if (!ImportAmountParser.TryParse(Get('T'), out decimal amount)
            || (fields.ContainsKey('U') && (!ImportAmountParser.TryParse(Get('U'), out decimal alternate) || alternate != amount)))
        {
            result.Errors.Add(new ParseError(line, "The QIF amount must contain exact signed cents; duplicate T/U amounts must agree."));
            return;
        }
        bool split = splitCategories.Count + splitAmounts.Count + splitMemos.Count > 0;
        if (split)
        {
            decimal sum = 0M;
            if (splitCategories.Count == 0 || splitAmounts.Count != splitCategories.Count)
            {
                result.Errors.Add(new ParseError(line, "Each QIF split requires a category and amount."));
                return;
            }
            foreach (string value in splitAmounts)
            {
                if (!ImportAmountParser.TryParse(value, out decimal part))
                {
                    result.Errors.Add(new ParseError(line, "A QIF split amount is invalid."));
                    return;
                }
                try { sum = checked(sum + part); }
                catch (OverflowException) { result.Errors.Add(new ParseError(line, "QIF split amounts exceed the supported range.")); return; }
            }
            if (sum != amount)
            {
                result.Errors.Add(new ParseError(line, "QIF split amounts do not equal the transaction total."));
                return;
            }
        }

        string category = Get('L');
        bool transfer = !split && Regex.IsMatch(category, @"^\[[^\[\]]+\](?:/.*)?$", RegexOptions.CultureInvariant);
        string description = Get('P');
        if (description.Length == 0) description = Get('M');
        if (description.Length == 0) description = "QIF account activity";
        if (description.Equals("Opening Balance", StringComparison.OrdinalIgnoreCase))
        {
            result.Warnings.Add(new ParseError(line, "Skipped the QIF opening-balance initializer. Enter the source opening balance in Reconcile Statement or the appropriate dated account balance; it is not period transaction activity."));
            return;
        }
        string type = transfer ? (result.SourceAccountType == "CREDITCARD" ? "Payment" : "Transfer")
            : amount > 0M ? ImportClassification.UnclassifiedCredit : "Debit";
        if (split && (splitCategories.Any(value => value.Contains('[')) || amount > 0M))
            type = amount > 0M ? ImportClassification.UnclassifiedCredit : ImportClassification.UnclassifiedDebit;
        List<string> memo = new();
        if (Get('M').Length > 0) memo.Add(Get('M'));
        if (Get('N').Length > 0) memo.Add("Reference: " + Get('N'));
        if (Get('C').Length > 0) memo.Add("Cleared status: " + Get('C'));
        if (transfer) memo.Add("Transfer category: " + category);
        if (split)
        {
            memo.Add("Splits: " + string.Join("; ", splitCategories.Select((value, index) => value + " " + splitAmounts[index])));
            if (splitMemos.Count > 0) memo.Add("Split notes: " + string.Join("; ", splitMemos));
            result.Warnings.Add(new ParseError(line, "The QIF split is retained as one account movement with its total and split details in Memo. Review category allocation; mixed transfers require explicit classification."));
        }
        string memoText = string.Join(" | ", memo);
        if (description.Length > 1000 || category.Length > 200 || memoText.Length > 1000)
        {
            result.Errors.Add(new ParseError(line, "QIF description, category, or memo exceeds the supported field length; no text was truncated."));
            return;
        }
        if (ImportClassification.RequiresReview(type))
            result.Warnings.Add(new ParseError(line, "This QIF entry does not establish income, refund, or transfer treatment. Confirm its financial classification in Transactions."));
        ParsedTransaction row = new()
        {
            RowNumber = line, Date = date, Amount = amount, Type = type, Description = description,
            SourceAccountIdentity = result.SourceAccountIdentity, Currency = "USD",
            SourceCategory = transfer ? "Transfer" : split || category.Length == 0 ? null : category,
            Memo = memoText.Length == 0 ? null : memoText
        };
        string identityDetails = string.Join("|", Get('N'), Get('M'), category,
            string.Join(";", splitCategories), string.Join(";", splitAmounts), string.Join(";", splitMemos));
        row.ExternalTransactionId = identities.Next(ProfileCode, row, identityDetails);
        result.Rows.Add(row);
    }

    private static ParseResult Fail(ParseResult result, int line, string error)
    {
        result.Rows.Clear();
        result.Errors.Add(new ParseError(line, error));
        return result;
    }
}
