using PersonalFinanceManager.Core.Models;

namespace PersonalFinanceManager.Core.Rules
{
    public sealed class RuleMatchResult
    {
        public RuleMatchResult(FinancialRule rule)
        {
            Rule = rule;
        }

        public FinancialRule Rule { get; }
    }
}
