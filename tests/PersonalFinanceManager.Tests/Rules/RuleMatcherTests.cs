using PersonalFinanceManager.Core.Imports;
using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Core.Rules;

namespace PersonalFinanceManager.Tests.Rules
{
    [TestClass]
    public sealed class RuleMatcherTests
    {
        [TestMethod]
        public void Match_UsesDescriptionAndType()
        {
            FinancialRule rule = new()
            {
                RuleId = 7,
                RuleName = "Sample",
                MatchDescription = "SAMPLE MERCHANT",
                MatchType = "Withdrawal",
                DescriptionMatchMode = RuleMatchModes.Exact,
                TypeMatchMode = RuleMatchModes.Exact,
                IsActive = true
            };

            ParsedTransaction transaction = new()
            {
                Description = "SAMPLE MERCHANT",
                Type = "Withdrawal",
                Amount = -10m
            };

            RuleMatcher matcher = new();
            FinancialRule? matched = matcher.Match(new[] { rule }, transaction);
            Assert.IsNotNull(matched);
            Assert.AreEqual(7, matched.RuleId);
        }

        [TestMethod]
        public void Match_DoesNotMatchDifferentType()
        {
            FinancialRule rule = new()
            {
                RuleName = "Sample",
                MatchDescription = "SAMPLE MERCHANT",
                MatchType = "Deposit",
                DescriptionMatchMode = RuleMatchModes.Exact,
                TypeMatchMode = RuleMatchModes.Exact,
                IsActive = true
            };

            ParsedTransaction transaction = new()
            {
                Description = "SAMPLE MERCHANT",
                Type = "Withdrawal",
                Amount = -10m
            };

            RuleMatcher matcher = new();
            FinancialRule? matched = matcher.Match(new[] { rule }, transaction);
            Assert.IsNull(matched);
        }
    }
}
