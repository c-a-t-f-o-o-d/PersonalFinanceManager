using PersonalFinanceManager.Core.Imports;
using PersonalFinanceManager.Core.Models;

namespace PersonalFinanceManager.Core.Rules
{
    public sealed class RuleMatcher
    {
        public FinancialRule? Match(IEnumerable<FinancialRule> rules, ParsedTransaction transaction)
        {
            ArgumentNullException.ThrowIfNull(rules);
            ArgumentNullException.ThrowIfNull(transaction);
            return rules.Where(rule => rule.IsActive).OrderBy(rule => rule.Priority).ThenBy(rule => rule.RuleId).FirstOrDefault(rule => IsMatch(rule, transaction));
        }

        private static bool IsMatch(FinancialRule rule, ParsedTransaction transaction)
        {
            if (!TextMatches(transaction.Description, rule.MatchDescription, rule.DescriptionMatchMode))
            {
                return false;
            }

            if (!RuleTransactionTypeNormalizer.Matches(transaction.Type, transaction.Amount, rule.MatchType, rule.TypeMatchMode))
            {
                return false;
            }

            return true;
        }

        private static bool TextMatches(string actual, string? expected, string? mode)
        {
            if (string.IsNullOrWhiteSpace(expected))
            {
                return true;
            }

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
