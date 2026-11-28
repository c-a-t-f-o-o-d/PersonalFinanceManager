using PersonalFinanceManager.Core.Imports;
using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Core.Planning;
using PersonalFinanceManager.Core.Reporting;

namespace PersonalFinanceManager.Tests.Reporting;

[TestClass]
public sealed class ImportClassificationReportTests
{
    [TestMethod]
    public void UnknownCreditsRemainCashWithoutBecomingIncomeOrRefunds()
    {
        FinancialReportCalculator calculator = new(new ReportData
        {
            Transactions = [Row(1, -100m, "Purchase"), Row(2, 500m, ImportClassification.UnclassifiedCredit)]
        });

        Assert.AreEqual(100m, calculator.Expenses(new()).Metrics.TotalExpenses);
        Assert.IsEmpty(calculator.Income(new()));
        Assert.AreEqual(500m, calculator.CashFlow(new()).Summary.TotalMoneyIn);
        Assert.AreEqual(100m, calculator.CashFlow(new()).Summary.TotalMoneyOut);
        Assert.HasCount(1, calculator.Expenses(new()).Warnings);
        Assert.IsFalse(calculator.SalarySpending(new()).CanEstimate);
    }

    [TestMethod]
    public void ManualRefundAndIncomeDecisionsResolveClassification()
    {
        FinancialReportCalculator calculator = new(new ReportData
        {
            Transactions = [Row(1, -100m, "Purchase"), Row(2, 20m, "Refund", manual: true), Row(3, 500m, ImportClassification.UnclassifiedCredit, income: true)]
        });

        Assert.AreEqual(80m, calculator.Expenses(new()).Metrics.TotalExpenses);
        Assert.AreEqual(500m, calculator.Income(new()).Single().TotalNetIncome);
        Assert.IsEmpty(calculator.Expenses(new()).Warnings);
        Assert.IsTrue(calculator.SalarySpending(new()).CanEstimate);
    }

    [TestMethod]
    public void CategoryOnlyEditsCannotTurnUnknownCreditsIntoRefunds()
    {
        FinancialReportCalculator calculator = new(new ReportData
        {
            Transactions = [Row(1, -100m, "Purchase"), Row(2, 500m, ImportClassification.UnclassifiedCredit, manual: true)]
        });

        Assert.AreEqual(100m, calculator.Expenses(new()).Metrics.TotalExpenses);
        Assert.IsEmpty(calculator.Income(new()));
        Assert.IsTrue(calculator.NeedsClassificationReview(2));
        Assert.IsFalse(calculator.SalarySpending(new()).CanEstimate);
    }

    [TestMethod]
    public void PartialRefundAllocationUsesOnlyTheConfirmedAmount()
    {
        ReportData data = new()
        {
            Transactions = [Row(1, -100m, "Purchase"), Row(2, 50m, ImportClassification.UnclassifiedCredit)],
            Links = [new TransactionLinkRecord { TransactionLinkId = 1, SourceTransactionId = 2, LinkedTransactionId = 1, LinkType = TransactionLinkTypes.PurchaseRefund, AllocationAmount = 20m }]
        };

        FinancialReportCalculator calculator = new(data);
        Assert.AreEqual(80m, calculator.Expenses(new()).Metrics.TotalExpenses);
        Assert.IsTrue(calculator.NeedsClassificationReview(2));
        data.Links[0].AllocationAmount = 50m;
        FinancialReportCalculator resolved = new(data);
        Assert.AreEqual(50m, resolved.Expenses(new()).Metrics.TotalExpenses);
        Assert.IsFalse(resolved.NeedsClassificationReview(2));
        Assert.IsEmpty(resolved.Expenses(new()).Warnings);
    }

    [TestMethod]
    public void UnclassifiedDebitsRemainVisibleButDoNotCreateRecurringBillSuggestions()
    {
        ReportData data = new()
        {
            Transactions = Enumerable.Range(1, 3).Select(id => Row(id, -100m, ImportClassification.UnclassifiedDebit)).ToArray()
        };

        FinancialReportCalculator calculator = new(data);
        Assert.AreEqual(300m, calculator.Expenses(new()).Metrics.TotalExpenses);
        Assert.IsFalse(calculator.SalarySpending(new()).CanEstimate);
        Assert.IsEmpty(RecurringBillCandidateDetector.Detect(data, 2, 2));
    }

    private static ReportTransaction Row(long id, decimal amount, string type, bool manual = false, bool income = false) => new()
    {
        Id = id,
        AccountId = 1,
        AccountNickname = "Example Checking",
        AccountType = "Checking",
        Date = new DateOnly(2026, (int)id, 12),
        Amount = amount,
        Type = type,
        Description = "Example activity",
        Category = "Shopping",
        SpendingClass = "Shopping",
        ClassificationIsManual = manual,
        IsIncome = income,
        IncomeOwner = OwnerValues.Me
    };
}
