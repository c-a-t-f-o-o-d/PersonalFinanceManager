using PersonalFinanceManager.Core.Imports;
using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Core.Rules;

namespace PersonalFinanceManager.Tests.Rules
{
    [TestClass]
    public sealed class RuleTransactionTypeNormalizerTests
    {
        [TestMethod]
        public void Match_WithdrawalRuleMatchesSaleWhenAmountIsNegative()
        {
            FinancialRule rule = new()
            {
                MatchDescription = "SAMPLE MERCHANT",
                MatchType = "Withdrawal",
                DescriptionMatchMode = RuleMatchModes.Exact,
                TypeMatchMode = RuleMatchModes.Exact,
                IsActive = true
            };

            ParsedTransaction transaction = new()
            {
                Description = "SAMPLE MERCHANT",
                Type = "Sale",
                Amount = -12.34m
            };

            FinancialRule? matched = new RuleMatcher().Match(new[] { rule }, transaction);
            Assert.IsNotNull(matched);
        }

        [TestMethod]
        public void Match_DepositRuleMatchesReturnWhenAmountIsPositive()
        {
            FinancialRule rule = new()
            {
                MatchDescription = "SAMPLE MERCHANT",
                MatchType = "Deposit",
                DescriptionMatchMode = RuleMatchModes.Exact,
                TypeMatchMode = RuleMatchModes.Exact,
                IsActive = true
            };

            ParsedTransaction transaction = new()
            {
                Description = "SAMPLE MERCHANT",
                Type = "Return",
                Amount = 12.34m
            };

            FinancialRule? matched = new RuleMatcher().Match(new[] { rule }, transaction);
            Assert.IsNotNull(matched);
        }

        [TestMethod]
        public void Match_WithdrawalRuleDoesNotMatchPositiveReturn()
        {
            FinancialRule rule = new()
            {
                MatchDescription = "SAMPLE MERCHANT",
                MatchType = "Withdrawal",
                DescriptionMatchMode = RuleMatchModes.Exact,
                TypeMatchMode = RuleMatchModes.Exact,
                IsActive = true
            };

            ParsedTransaction transaction = new()
            {
                Description = "SAMPLE MERCHANT",
                Type = "Return",
                Amount = 12.34m
            };

            FinancialRule? matched = new RuleMatcher().Match(new[] { rule }, transaction);
            Assert.IsNull(matched);
        }

        [TestMethod]
        public void NormalizeRuleType_MapsPurchaseToWithdrawal()
        {
            Assert.AreEqual(RuleTransactionTypeNormalizer.Withdrawal, RuleTransactionTypeNormalizer.NormalizeRuleType("Purchase"));
            Assert.AreEqual(RuleTransactionTypeNormalizer.Withdrawal, RuleTransactionTypeNormalizer.NormalizeRuleType("Sale"));
            Assert.AreEqual(RuleTransactionTypeNormalizer.Withdrawal, RuleTransactionTypeNormalizer.NormalizeRuleType("Debit"));
        }

        [TestMethod]
        public void Matches_SignedAmountOverridesContradictorySourceType()
        {
            Assert.IsFalse(RuleTransactionTypeNormalizer.Matches("Payment", -50m, "Deposit", RuleMatchModes.Exact));
            Assert.IsTrue(RuleTransactionTypeNormalizer.Matches("Payment", -50m, "Withdrawal", RuleMatchModes.Exact));
            Assert.IsFalse(RuleTransactionTypeNormalizer.Matches("Withdrawal", 50m, "Withdrawal", RuleMatchModes.Exact));
            Assert.IsTrue(RuleTransactionTypeNormalizer.Matches("Withdrawal", 50m, "Deposit", RuleMatchModes.Exact));
        }
    }
}
