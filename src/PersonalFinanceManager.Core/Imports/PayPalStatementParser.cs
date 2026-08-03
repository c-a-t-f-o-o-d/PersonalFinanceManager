using System.Globalization;
using System.Text.RegularExpressions;

namespace PersonalFinanceManager.Core.Imports
{
    public sealed class PayPalStatementParser : ITransactionParser
    {
        private static readonly Regex _splitYearRegex = new(@"(?<prefix>\d{2}/\d{2}/\d{3})\s+(?<suffix>\d)", RegexOptions.Compiled);
        private static readonly Regex _transactionRegex = new(@"(?<date>\d{2}/\d{2}/\d{4})\s+(?<description>.*?)\s+USD\s+(?<amount>-?\d+(?:\.\d{2}))\s+(?<fees>-?\d+(?:\.\d{2}))\s+(?<total>-?\d+(?:\.\d{2}))", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex _fundingRegex = new(@"(?<source>ALLY BANK\s*-\s*Checking\s*x-\d+|PayPal Smart Connect)\s+(?:\d+(?:\.\d{2})\s+)?USD\s+ID:\s*(?<id>[A-Za-z0-9]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private readonly MinimalPdfTextExtractor _pdfTextExtractor = new();
        public string ProfileCode => ImportProfileCodes.PayPalStatement;

        public async Task<ParseResult> ParseAsync(string filePath, CancellationToken cancellationToken = default)
        {
            string extension = Path.GetExtension(filePath);

            if (string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return ParseText(_pdfTextExtractor.ExtractText(filePath));
            }

            string text = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            return ParseText(text);
        }

        public ParseResult ParseText(string text)
        {
            ParseResult result = new();
            string normalized = NormalizeText(RepairPdfGlyphNoise(text));
            string accountSection = GetPayPalAccountActivitySection(normalized);

            if (string.IsNullOrWhiteSpace(accountSection))
            {
                result.Errors.Add(new ParseError(1, "The PayPal account activity section was not found."));
                return result;
            }

            IReadOnlyList<(string Source, string Id)> fundingSources = ExtractFundingSources(accountSection);
            MatchCollection matches = _transactionRegex.Matches(accountSection);
            int rowNumber = 1;

            for (int index = 0; index < matches.Count; index++)
            {
                Match match = matches[index];

                if (!DateOnly.TryParseExact(match.Groups["date"].Value, "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
                {
                    result.Errors.Add(new ParseError(rowNumber, "Invalid PayPal transaction date."));
                    rowNumber++;
                    continue;
                }

                if (!decimal.TryParse(match.Groups["total"].Value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal total))
                {
                    result.Errors.Add(new ParseError(rowNumber, "Invalid PayPal transaction total."));
                    rowNumber++;
                    continue;
                }

                string rawDescription = match.Groups["description"].Value;
                (string type, string description) = NormalizeTransactionDescription(rawDescription);
                string? fundingSource = null;
                string? externalId = null;

                if (index < fundingSources.Count)
                {
                    fundingSource = fundingSources[index].Source;
                    externalId = fundingSources[index].Id;
                }

                result.Rows.Add(new ParsedTransaction
                {
                    RowNumber = rowNumber,
                    Date = date,
                    Amount = total,
                    Type = type,
                    Description = description,
                    ExternalTransactionId = externalId,
                    FundingSource = fundingSource
                });

                rowNumber++;
            }

            if (result.Rows.Count == 0 && result.Errors.Count == 0)
            {
                result.Errors.Add(new ParseError(1, "No PayPal transactions were found."));
            }

            return result;
        }

        private static string RepairPdfGlyphNoise(string text)
        {
            string repaired = text;
            repaired = repaired.Replace("PPYPPL", "PAYPAL", StringComparison.Ordinal);
            repaired = repaired.Replace("PCCOUNc", "ACCOUNT", StringComparison.Ordinal);
            repaired = repaired.Replace("PCcIeIcY", "ACTIVITY", StringComparison.Ordinal);
            repaired = repaired.Replace("PLLY BPNK", "ALLY BANK", StringComparison.Ordinal);
            repaired = repaired.Replace("Chtcking", "Checking", StringComparison.Ordinal);
            repaired = repaired.Replace("Conntct", "Connect", StringComparison.Ordinal);
            repaired = repaired.Replace("PrtPpprovts Paymtnt Bill Ustr Paymtnt", "PreApproved Payment Bill User Payment", StringComparison.Ordinal);
            repaired = repaired.Replace("Buytr Crtsit Paymtnt", "Buyer Credit Payment", StringComparison.Ordinal);
            repaired = Regex.Replace(repaired, @"(?<=\d)C(?=[/\d])", "4");
            repaired = Regex.Replace(repaired, @"(?<=/)C(?=[/\d])", "4");
            repaired = Regex.Replace(repaired, @"x-C(?=\d{3})", "x-4");
            return repaired;
        }

        private static string NormalizeText(string text)
        {
            string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            normalized = _splitYearRegex.Replace(normalized, match => match.Groups["prefix"].Value + match.Groups["suffix"].Value);
            normalized = Regex.Replace(normalized, @"[ \t]+", " ");
            return normalized;
        }

        private static string GetPayPalAccountActivitySection(string text)
        {
            Match activityHeader = Regex.Match(text, @"PAYPAL\s+ACCOUNT\s+ACCOUNT\s+ACTIVITY\s+DATE", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            int start = activityHeader.Success ? activityHeader.Index : text.IndexOf("ACCOUNT ACTIVITY", StringComparison.OrdinalIgnoreCase);

            if (start < 0)
            {
                return text;
            }

            int footnoteStart = text.IndexOf("*For each transaction", start, StringComparison.OrdinalIgnoreCase);
            int balanceStart = text.IndexOf("PAYPAL BALANCE ACCOUNT", start + 1, StringComparison.OrdinalIgnoreCase);
            int end = text.Length;

            if (footnoteStart > start)
            {
                end = Math.Min(end, footnoteStart);
            }

            if (balanceStart > start)
            {
                end = Math.Min(end, balanceStart);
            }

            return text[start..end];
        }

        private static IReadOnlyList<(string Source, string Id)> ExtractFundingSources(string section)
        {
            List<(string Source, string Id)> sources = new();

            foreach (Match match in _fundingRegex.Matches(section))
            {
                sources.Add((NormalizeWhitespace(match.Groups["source"].Value), NormalizeWhitespace(match.Groups["id"].Value)));
            }

            return sources;
        }

        private static (string Type, string Description) NormalizeTransactionDescription(string rawDescription)
        {
            List<string> lines = rawDescription
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizeWhitespace)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Where(line => !line.Equals("CURRENCY", StringComparison.OrdinalIgnoreCase))
                .Where(line => !line.Equals("AMOUNT", StringComparison.OrdinalIgnoreCase))
                .Where(line => !line.Equals("FEES", StringComparison.OrdinalIgnoreCase))
                .Where(line => !line.Equals("USD", StringComparison.OrdinalIgnoreCase))
                .Where(line => !line.StartsWith("ALLY BANK", StringComparison.OrdinalIgnoreCase))
                .Where(line => !line.Equals("PayPal Smart Connect", StringComparison.OrdinalIgnoreCase))
                .Where(line => !line.StartsWith("ID:", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (lines.Count == 0)
            {
                return ("PayPal", "PayPal Transaction");
            }

            string joined = string.Join(" ", lines);

            if (joined.StartsWith("PreApproved Payment Bill User Payment:", StringComparison.OrdinalIgnoreCase))
            {
                string merchant = joined["PreApproved Payment Bill User Payment:".Length..].Trim();
                return ("PreApproved Payment", string.IsNullOrWhiteSpace(merchant) ? "PayPal PreApproved Payment" : merchant);
            }

            if (joined.StartsWith("Buyer Credit Payment", StringComparison.OrdinalIgnoreCase))
            {
                return ("Buyer Credit Payment", "Buyer Credit Payment");
            }

            return (lines[0], joined);
        }

        private static string NormalizeWhitespace(string value)
        {
            return Regex.Replace(value.Trim(), @"\s+", " ");
        }
    }
}
