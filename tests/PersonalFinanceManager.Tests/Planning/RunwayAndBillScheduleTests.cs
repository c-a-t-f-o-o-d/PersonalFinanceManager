using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Core.Planning;
using PersonalFinanceManager.Core.Reporting;

namespace PersonalFinanceManager.Tests.Planning;

[TestClass]
public sealed class RunwayAndBillScheduleTests
{
    [TestMethod]
    public void AccountClassificationDistinguishesCashFromAvailableCredit()
    {
        Assert.IsTrue(AccountClassification.IsCash("Credit Union Checking"));
        Assert.IsTrue(AccountClassification.IsCash("Credit Union Savings"));
        Assert.IsTrue(AccountClassification.IsCash("Apple Cash"));
        Assert.IsTrue(AccountClassification.IsWallet("PayPal"));
        Assert.IsFalse(AccountClassification.IsCash("Apple Pay"));
        Assert.IsTrue(AccountClassification.IsLiability("PayPal Smart Connect"));
        Assert.IsFalse(AccountClassification.IsCash("PayPal Smart Connect"));
        Assert.IsFalse(AccountClassification.IsWallet("PayPal Credit"));
    }

    [TestMethod]
    public void RunwayUsesAvailableCashIncludesOverdraftsAndExcludesCredit()
    {
        DateOnly today = new(2026, 11, 27);

        Account[] accounts = [new() { AccountType = "Checking", CurrentBalance = 1000M, AvailableBalance = 500M, BalanceAsOfDate = today },
            new() { AccountType = "Savings", CurrentBalance = -200M, BalanceAsOfDate = today },
            new() { AccountType = "PayPal Smart Connect", CurrentBalance = 2000M, AvailableBalance = 10000M, BalanceAsOfDate = today }];

        RunwayAnalysisResult result = new RunwayCalculator().Calculate(accounts, [], "Emergency", 0M, 1, today);
        Assert.AreEqual(300M, result.StartingAvailableBalance);
        Assert.AreEqual(300M, result.ProjectedEndingBalance);
    }

    [TestMethod]
    public void AnnualBillDepletesCashOnItsDueDateRatherThanOnTheFirstOfTheCurrentMonth()
    {
        DateOnly today = new(2026, 11, 27);

        RunwayAnalysisResult result = new RunwayCalculator().Calculate([new Account { AccountType = "Checking", AvailableBalance = 1200M, BalanceAsOfDate = today }],
            [new RecurringBill { BillName = "Insurance", Payee = "Insurer", TypicalAmount = 1200M, Frequency = BillFrequencies.Annual, NextDueDate = new DateOnly(2026, 12, 1) }], "Strict", 0M, 2, today);

        Assert.AreEqual(new DateOnly(2026, 12, 1), result.EstimatedDepletionDate);
        Assert.AreEqual(new DateOnly(2026, 12, 27), result.ProjectionPoints[0].ProjectionDate);
        Assert.AreEqual(1200M, result.ProjectionPoints[0].RequiredExpenses);
        Assert.AreEqual(0M, result.ProjectionPoints[1].RequiredExpenses);
    }

    [TestMethod]
    public void UnknownBalanceAndKnownZeroBalanceAreDifferent()
    {
        RunwayCalculator calculator = new();
        DateOnly today = new(2026, 11, 27);
        RunwayAnalysisResult unknown = calculator.Calculate([new Account { AccountType = "Checking" }], [], "Strict", 0M, 1, today);
        Assert.IsFalse(unknown.HasKnownBalance);
        Assert.IsNull(unknown.EstimatedDepletionDate);
        RunwayAnalysisResult zero = calculator.Calculate([new Account { AccountType = "Checking", AvailableBalance = 0M, BalanceAsOfDate = today }], [], "Strict", 0M, 1, today);
        Assert.IsTrue(zero.HasKnownBalance);
        Assert.AreEqual(today, zero.EstimatedDepletionDate);
    }

    [TestMethod]
    public void MonthlyScheduleRetainsThe31stAfterFebruary()
    {
        DateOnly anchor = new(2027, 1, 31);
        Assert.AreEqual(new DateOnly(2027, 2, 28), BillSchedule.Occurrence(anchor, BillFrequencies.Monthly, 1));
        Assert.AreEqual(new DateOnly(2027, 3, 31), BillSchedule.Occurrence(anchor, BillFrequencies.Monthly, 2));
        Assert.AreEqual(new DateOnly(2027, 3, 31), BillSchedule.NextOnOrAfter(anchor, BillFrequencies.Monthly, new DateOnly(2027, 3, 1)));
    }

    [TestMethod]
    public void WeeklyCandidatesKeepTheirPerOccurrenceAmountAndCadence()
    {
        DateOnly first = new(2026, 1, 5);

        ReportData data = new()
        {
            Transactions = Enumerable.Range(0, 9).Select(index => new ReportTransaction
            {
                Id = index + 1,
                AccountId = 1,
                AccountType = "Checking",
                Date = first.AddDays(index * 7),
                Amount = -100M,
                Description = "Weekly service",
                Category = "Utilities"
            }).ToArray()
        };

        RecurringBillCandidate candidate = RecurringBillCandidateDetector.Detect(data, 2, 2).Single();
        Assert.AreEqual(BillFrequencies.Weekly, candidate.SuggestedFrequency);
        RecurringBill bill = RecurringBillCandidateDetector.CreateDraft(candidate, new DateOnly(2026, 3, 3));
        Assert.AreEqual(100M, bill.TypicalAmount);
        Assert.AreEqual(433.33M, BillSchedule.MonthlyEquivalent(bill.TypicalAmount, bill.Frequency));
        Assert.AreEqual(new DateOnly(2026, 3, 9), bill.NextDueDate);
        Assert.IsNull(bill.DueDay);
    }

    [TestMethod]
    public void IrregularTransactionsRequireAnExplicitFrequencyChoice()
    {
        Assert.IsNull(BillSchedule.InferFrequency([new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 3), new DateOnly(2026, 2, 18)]));

        RecurringBill draft = RecurringBillCandidateDetector.CreateDraft(new RecurringBillCandidate
        {
            Payee = "Irregular merchant",
            AverageAmount = 100M,
            LastSeenDate = new DateOnly(2026, 2, 18)
        }, new DateOnly(2026, 3, 1));

        Assert.AreEqual(string.Empty, draft.Frequency);
        Assert.IsNull(draft.NextDueDate);
        Assert.ThrowsExactly<ArgumentException>(() => BillSchedule.MonthlyEquivalent(100M, draft.Frequency));
    }

    [TestMethod]
    public void MissingRequiredBillAmountsMakeTheProjectionIncomplete()
    {
        RunwayAnalysisResult result = new RunwayCalculator().Calculate([new Account { AccountType = "Checking", CurrentBalance = 1000M }],
            [new RecurringBill { BillName = "Unpriced bill", Payee = "Utility" }], "Strict", 0M, 1, new DateOnly(2026, 11, 27));

        Assert.IsFalse(result.HasCompleteInputs);
        Assert.IsTrue(result.Assumptions.Any(note => note.Contains("positive amount", StringComparison.Ordinal)));
    }
}
