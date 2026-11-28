using PersonalFinanceManager.Core.Models;

namespace PersonalFinanceManager.Tests.Services;

[TestClass]
public sealed class AccountReconciliationCalculatorTests
{
    [TestMethod]
    public void CheckingToSavings_UsesEachAccountEntryEvenWhenExcludedFromSpending()
    {
        ImportedTransaction[] rows =
        [
            Row(1, -250m, new(2026, 1, 10), transfer: true),
            Row(2, 250m, new(2026, 1, 10), transfer: true)
        ];

        AccountReconciliation checking = Check(1, "Checking", 1000m, 750m, rows);
        AccountReconciliation savings = Check(2, "Savings", 500m, 750m, rows);
        Assert.IsTrue(checking.IsBalanced);
        Assert.IsTrue(savings.IsBalanced);
        Assert.AreEqual(1, checking.TransactionCount);
        Assert.AreEqual(-250m, checking.NetActivity);
        Assert.AreEqual(250m, savings.NetActivity);
    }

    [TestMethod]
    public void SmartConnect_PurchasesRefundsAndAggregateRepaymentReconcileAsDebt()
    {
        ImportedTransaction[] rows =
        [
            Row(3, -60m, new(2026, 1, 2)),
            Row(3, -40m, new(2026, 1, 6)),
            Row(3, 10m, new(2026, 1, 8)),
            Row(3, 90m, new(2026, 1, 20), transfer: true),
            Row(1, -90m, new(2026, 1, 21), transfer: true)
        ];

        AccountReconciliation result = Check(3, "PayPal Smart Connect", 20m, 20m, rows);
        Assert.IsTrue(result.IsBalanced);
        Assert.IsTrue(result.UsesLiabilityBalances);
        Assert.AreEqual(4, result.TransactionCount);
        Assert.AreEqual(0m, result.NetActivity);
    }

    [TestMethod]
    public void LiabilityBalance_IncreasesWhenNewChargesExceedPayments()
    {
        AccountReconciliation result = Check(3, "Credit Card", 100m, 125m, [Row(3, -50m, new(2026, 1, 2)), Row(3, 25m, new(2026, 1, 3))]);
        Assert.IsTrue(result.IsBalanced);
        Assert.AreEqual(125m, result.ExpectedClosingBalance);
    }

    [TestMethod]
    public void StatementPeriod_PrefersPostingDateAndReportsFallbackCount()
    {
        ImportedTransaction postedInJanuary = Row(1, -20m, new(2025, 12, 31));
        postedInJanuary.PostingDate = new(2026, 1, 2);
        ImportedTransaction postedInFebruary = Row(1, -30m, new(2026, 1, 31));
        postedInFebruary.PostingDate = new(2026, 2, 2);
        AccountReconciliation result = Check(1, "Checking", 100m, 75m, [postedInJanuary, postedInFebruary, Row(1, -5m, new(2026, 1, 10)), Row(1, -99m, new(2025, 12, 30))]);
        Assert.IsTrue(result.IsBalanced);
        Assert.AreEqual(2, result.TransactionCount);
        Assert.AreEqual(1, result.DateFallbackCount);
    }

    [TestMethod]
    public void MissingActivity_RemainsVisibleAsDifference()
    {
        AccountReconciliation result = Check(1, "Checking", 100m, 70m, [Row(1, -20m, new(2026, 1, 5))]);
        Assert.IsFalse(result.IsBalanced);
        Assert.AreEqual(-10m, result.Difference);
    }

    [TestMethod]
    public void InvalidBalanceOrPeriod_IsRejectedBeforeRecording()
    {
        Assert.Throws<ArgumentException>(() => Check(1, "Checking", 1.001m, 1m, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => Check(1, "Checking", 9999999999999999.99m, 0m, [Row(1, 1m, new(2026, 1, 1))]));

        Assert.Throws<ArgumentException>(() => AccountReconciliationCalculator.Calculate(new AccountReconciliationRequest
        {
            AccountId = 1,
            FromDate = new(2026, 2, 1),
            ToDate = new(2026, 1, 1)
        }, "Checking", []));
    }

    private static AccountReconciliation Check(int account, string type, decimal opening, decimal closing, ImportedTransaction[] rows)
    {
        return AccountReconciliationCalculator.Calculate(new AccountReconciliationRequest
        {
            AccountId = account,
            FromDate = new(2026, 1, 1),
            ToDate = new(2026, 1, 31),
            OpeningBalance = opening,
            ClosingBalance = closing
        }, type, rows);
    }

    private static ImportedTransaction Row(int account, decimal amount, DateOnly date, bool transfer = false)
    {
        return new() { AccountId = account, Amount = amount, TransactionDate = date, IsTransfer = transfer };
    }
}
