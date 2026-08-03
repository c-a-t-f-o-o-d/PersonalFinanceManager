using PersonalFinanceManager.Core.Imports;
using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Core.Rules;

namespace PersonalFinanceManager.Tests.Rules
{
    [TestClass]
    public sealed class ProvidedRuleBehaviorTests
    {
        [TestMethod]
        public void Match_StartsWithDescription_UsesExactType()
        {
            FinancialRule rule = new()
            {
                RuleId = 12,
                RuleName = "Sample Restaurant",
                MatchDescription = "SAMPLE FOOD",
                MatchType = "Withdrawal",
                DescriptionMatchMode = RuleMatchModes.StartsWith,
                TypeMatchMode = RuleMatchModes.Exact,
                Category = "Dining",
                SpendingClass = "Restaurant",
                IsActive = true
            };

            ParsedTransaction transaction = new()
            {
                Description = "SAMPLE FOOD 123 MAIN STREET",
                Type = "Withdrawal",
                Amount = -15.25m
            };

            FinancialRule? matched = new RuleMatcher().Match(new[] { rule }, transaction);
            Assert.IsNotNull(matched);
            Assert.AreEqual("Dining", matched.Category);
            Assert.AreEqual("Restaurant", matched.SpendingClass);
        }

        [TestMethod]
        public void Match_DoesNotMatchStartsWithRuleWhenTypeDiffers()
        {
            FinancialRule rule = new()
            {
                RuleName = "Sample Transfer",
                MatchDescription = "SAMPLE TRANSFER",
                MatchType = "Deposit",
                DescriptionMatchMode = RuleMatchModes.StartsWith,
                TypeMatchMode = RuleMatchModes.Exact,
                IsActive = true
            };

            ParsedTransaction transaction = new()
            {
                Description = "SAMPLE TRANSFER FROM ACCOUNT",
                Type = "Withdrawal",
                Amount = -100m
            };

            FinancialRule? matched = new RuleMatcher().Match(new[] { rule }, transaction);
            Assert.IsNull(matched);
        }
    }
}
