using PersonalFinanceManager.Core.Models;

namespace PersonalFinanceManager.Core.Rules
{
    public static class RuleTransactionTypeNormalizer
    {
        public const string Deposit = "Deposit";
        public const string Withdrawal = "Withdrawal";

        private static readonly HashSet<string> _depositAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            "Credit",
            "Deposit",
            "Refund",
            "Return",
            "Returned",
            "Payment",
            "Payment Thank You",
            "Cashback",
            "Interest",
            "Interest Paid"
        };

        private static readonly HashSet<string> _withdrawalAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            "Buyer Credit Payment",
            "Debit",
            "PreApproved Payment",
            "Purchase",
            "Sale",
            "Withdrawal"
        };

        public static string? NormalizeRuleType(string? type)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                return null;
            }

            string trimmed = type.Trim();

            if (_depositAliases.Contains(trimmed))
            {
                return Deposit;
            }

            if (_withdrawalAliases.Contains(trimmed))
            {
                return Withdrawal;
            }

            return trimmed;
        }

        public static bool IsNormalizedDirection(string? type)
        {
            string? normalized = NormalizeRuleType(type);
            return string.Equals(normalized, Deposit, StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, Withdrawal, StringComparison.OrdinalIgnoreCase);
        }

        public static bool Matches(string actualType, decimal amount, string? expectedType, string? matchMode)
        {
            if (string.IsNullOrWhiteSpace(expectedType))
            {
                return true;
            }

            string? normalizedExpected = NormalizeRuleType(expectedType);

            if (string.Equals(normalizedExpected, Deposit, StringComparison.OrdinalIgnoreCase))
            {
                return amount > 0 || (amount == 0 && string.Equals(NormalizeRuleType(actualType), Deposit, StringComparison.OrdinalIgnoreCase));
            }

            if (string.Equals(normalizedExpected, Withdrawal, StringComparison.OrdinalIgnoreCase))
            {
                return amount < 0 || (amount == 0 && string.Equals(NormalizeRuleType(actualType), Withdrawal, StringComparison.OrdinalIgnoreCase));
            }

            return TextMatches(actualType, expectedType, matchMode);
        }

        private static bool TextMatches(string actual, string expected, string? mode)
        {
            string normalizedMode = string.IsNullOrWhiteSpace(mode) ? RuleMatchModes.Exact : mode.Trim();

            return normalizedMode switch
            {
                RuleMatchModes.Contains => actual.Contains(expected, StringComparison.OrdinalIgnoreCase),
                RuleMatchModes.StartsWith => actual.StartsWith(expected, StringComparison.OrdinalIgnoreCase),
                _ => string.Equals(actual.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase)
            };
        }
    }
}
