using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PersonalFinanceManager.Core.Common;

namespace PersonalFinanceManager.Core.Imports;

public sealed class ImportOccurrenceIdentity
{
    private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);

    public string Next(string source, ParsedTransaction row, string? sourceDetails = null)
    {
        string text = string.Join("|",
            source,
            row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            row.PostingDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            row.Time?.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            row.Amount.ToString("0.00", CultureInfo.InvariantCulture),
            TextNormalizer.NormalizeForComparison(row.Type),
            TextNormalizer.NormalizeForComparison(row.Description),
            TextNormalizer.NormalizeForComparison(row.SourceAccountIdentity ?? string.Empty));
        if (sourceDetails is not null) text += "|" + sourceDetails;
        string fingerprint = source + ":" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..32];
        _counts.TryGetValue(fingerprint, out int count);
        _counts[fingerprint] = ++count;
        return fingerprint + ":" + count.ToString(CultureInfo.InvariantCulture);
    }
}
