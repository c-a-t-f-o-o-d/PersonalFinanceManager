namespace PersonalFinanceManager.Core.Common
{
    public static class TextNormalizer
    {
        public static string NormalizeForComparison(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string collapsed = string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return collapsed.ToUpperInvariant();
        }

        public static string LastFour(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string digits = new(value.Where(char.IsDigit).ToArray());

            if (digits.Length <= 4)
            {
                return digits;
            }

            return digits[^4..];
        }

        public static string MaskAccountNumber(string? value)
        {
            string lastFour = LastFour(value);
            return string.IsNullOrWhiteSpace(lastFour) ? string.Empty : $"****{lastFour}";
        }
    }
}
