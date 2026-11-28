using System.Globalization;
using System.Text.RegularExpressions;

namespace PersonalFinanceManager.Core.Imports;

public static class ImportAmountParser
{
    private static readonly Regex AmountPattern = new(@"^[+-]?(?:\d+|\d{1,3}(?:,\d{3})+)(?:\.\d{1,2}0*)?$", RegexOptions.CultureInvariant);
    private const decimal MaximumAmount = 9999999999999999.99m;

    public static bool TryParse(string? text, out decimal amount)
    {
        amount = 0;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string value = text.Trim();
        bool parenthesized = value.StartsWith('(') && value.EndsWith(')');

        if (parenthesized)
        {
            value = value[1..^1].Trim();
        }

        if (value.StartsWith('$'))
        {
            value = value[1..].Trim();
        }

        if (!AmountPattern.IsMatch(value) ||
            !decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out amount) ||
            (amount < -MaximumAmount || amount > MaximumAmount) ||
            (parenthesized && (value.StartsWith('-') || value.StartsWith('+'))))
        {
            amount = 0;
            return false;
        }

        if (parenthesized)
        {
            amount = -amount;
        }

        return true;
    }
}
