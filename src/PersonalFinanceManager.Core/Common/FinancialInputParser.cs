using System.Globalization;
using System.Text.RegularExpressions;

namespace PersonalFinanceManager.Core.Common
{
    public static class FinancialInputParser
    {
        public static decimal? OptionalDecimal(string? text, string fieldName, int scale = 2)
        {
            if (scale is not (2 or 5))
            {
                throw new ArgumentOutOfRangeException(nameof(scale));
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            string numericText = text.Trim();
            bool parenthesized = numericText.StartsWith('(') && numericText.EndsWith(')');
            if (parenthesized) numericText = numericText[1..^1].Trim();
            string sign = string.Empty;
            if (numericText.StartsWith('-') || numericText.StartsWith('+'))
            {
                sign = numericText[..1];
                numericText = numericText[1..].Trim();
            }
            if (numericText.StartsWith('$')) numericText = numericText[1..].Trim();
            if (parenthesized && sign.Length != 0)
            {
                throw new FormatException($"{fieldName} must use either a sign or parentheses, not both.");
            }

            if (!Regex.IsMatch(numericText, @"^(?:[0-9]+|[0-9]{1,3}(?:,[0-9]{3})+)(?:\.[0-9]+)?$", RegexOptions.CultureInvariant)
                || !decimal.TryParse(sign + numericText, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out decimal value)
                || decimal.Round(value, scale) != value)
            {
                throw new FormatException($"{fieldName} must be a valid number with no more than {scale} decimal places.");
            }

            if (parenthesized) value = -value;

            decimal limit = scale == 5 ? 9999.99999M : 9999999999999999.99M;
            if (value < -limit || value > limit)
            {
                throw new FormatException($"{fieldName} is outside the supported range.");
            }

            return value;
        }

        public static int? OptionalInteger(string? text, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                throw new FormatException($"{fieldName} must be a whole number.");
            }

            return value;
        }

        public static DateOnly? OptionalDate(string? text, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            string[] formats = ["yyyy-MM-dd", "M/d/yyyy", "MM/dd/yyyy"];
            if (!DateOnly.TryParseExact(text.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly value))
            {
                throw new FormatException($"{fieldName} must be a valid date in yyyy-MM-dd or M/d/yyyy format.");
            }

            return value;
        }
    }
}
