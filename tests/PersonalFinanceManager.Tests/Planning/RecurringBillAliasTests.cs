using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Core.Planning;
using PersonalFinanceManager.Core.Reporting;

namespace PersonalFinanceManager.Tests.Planning;

[TestClass]
public sealed class RecurringBillAliasTests
{
    [TestMethod]
    public void FormerAndCurrentServicerPaymentsReplaceHistoryOnceAndKeepOtherMortgages()
    {
        ReportData data = new()
        {
            Bills = [Mortgage()],
            Transactions =
            [
                Payment(1, 1, "OLD SERVICER HOME 101"),
                Payment(2, 2, "NEW SERVICER HOME 101"),
                Payment(3, 1, "NEW SERVICER PROPERTY 202", 500M),
                Payment(4, 2, "NEW SERVICER PROPERTY 202", 500M)
            ]
        };

        SalarySpendingAnalysis result = new FinancialReportCalculator(data).SalarySpending(Combined());

        Assert.IsTrue(result.CanEstimate);
        Assert.AreEqual(1800M, result.RequiredBillsMonthlySpending);
        Assert.AreEqual(500M, result.TransactionMonthlySpending);
        Assert.AreEqual(2300M, result.TargetMonthlySpending);
        Assert.HasCount(1, result.InputLines.Where(line => line.Source == "Required Bill").ToArray());
        Assert.AreEqual("OLD SERVICER HOME 101", data.Transactions[0].Description);
        Assert.AreEqual("NEW SERVICER HOME 101", data.Transactions[1].Description);
    }

    [TestMethod]
    public void AliasHistoryDoesNotAddForecastOccurrences()
    {
        DateOnly start = new(2026, 3, 1);
        RecurringBill bill = Mortgage();
        bill.NextDueDate = new DateOnly(2026, 3, 15);

        RunwayAnalysisResult result = new RunwayCalculator().Calculate(
            [new Account { AccountType = "Checking", AvailableBalance = 10000M, BalanceAsOfDate = start }],
            [bill], "Strict", 0M, 3, start);

        Assert.IsTrue(result.HasCompleteInputs);
        Assert.HasCount(1, result.IncludedBills);
        Assert.AreEqual(1800M, result.MonthlyRequiredExpenses);
        Assert.AreEqual(5400M, result.ProjectionPoints.Sum(point => point.RequiredExpenses));
        Assert.AreEqual(4600M, result.ProjectedEndingBalance);
    }

    [TestMethod]
    public void MatchingRetainsAccountCategoryAndCategoryTypeBoundaries()
    {
        RecurringBill bill = Mortgage();
        ExpenseDetailItem row = Expense("  old servicer home 101  ");
        Assert.IsTrue(RecurringBillMatcher.Matches(row, bill));
        row.AccountId = 2;
        Assert.IsFalse(RecurringBillMatcher.Matches(row, bill));
        row.AccountId = 1;
        row.Category = "Other loan";
        Assert.IsFalse(RecurringBillMatcher.Matches(row, bill));
        row.Category = "Mortgage";
        row.SpendingClass = "Other";
        Assert.IsFalse(RecurringBillMatcher.Matches(row, bill));
    }

    [TestMethod]
    public void ExactDescriptionsTakePrecedenceOverCurrentAndFormerPayees()
    {
        RecurringBill bill = Mortgage();
        ExpenseDetailItem row = Expense("NEW SERVICER PROPERTY 202");
        row.Merchant = "New servicer";
        Assert.IsFalse(RecurringBillMatcher.Matches(row, bill));
        row.Merchant = "Old servicer";
        Assert.IsFalse(RecurringBillMatcher.Matches(row, bill));
        row.Description = "OLD SERVICER HOME 101";
        Assert.IsTrue(RecurringBillMatcher.Matches(row, bill));
    }

    [TestMethod]
    public void PayeeAliasesMatchExactlyWhenNoStatementDescriptionsAreConfigured()
    {
        RecurringBill bill = Mortgage();
        bill.SourceDescription = null;
        bill.SourceDescriptionAliases = [];
        ExpenseDetailItem row = Expense("Monthly payment");
        row.Merchant = "old SERVICER";
        Assert.IsTrue(RecurringBillMatcher.Matches(row, bill));
        row.Merchant = "Old servicer fee division";
        Assert.IsFalse(RecurringBillMatcher.Matches(row, bill));
    }

    [TestMethod]
    public void CandidateDetectionRecognizesBothNamesWithoutMergingOtherLoans()
    {
        ReportData data = new()
        {
            Bills = [Mortgage()],
            Transactions =
            [
                Payment(1, 1, "OLD SERVICER HOME 101"),
                Payment(2, 2, "OLD SERVICER HOME 101"),
                Payment(3, 3, "NEW SERVICER HOME 101"),
                Payment(4, 4, "NEW SERVICER HOME 101"),
                Payment(5, 1, "NEW SERVICER PROPERTY 202"),
                Payment(6, 2, "NEW SERVICER PROPERTY 202")
            ]
        };

        IReadOnlyList<RecurringBillCandidate> candidates = RecurringBillCandidateDetector.Detect(data, 2, 2);
        Assert.HasCount(3, candidates);
        Assert.HasCount(2, candidates.Where(candidate => candidate.AlreadyExists).ToArray());
        Assert.IsFalse(candidates.Single(candidate => candidate.SourceDescription == "NEW SERVICER PROPERTY 202").AlreadyExists);
        Assert.IsTrue(candidates.Where(candidate => candidate.AlreadyExists).All(candidate => candidate.MatchingBillName == "Home mortgage" && !candidate.RequiresMatchReview));
    }

    [TestMethod]
    public void AttachingCandidatePreservesTheExistingObligationAndIsIdempotent()
    {
        RecurringBill bill = Mortgage();
        bill.SourceDescriptionAliases = [];
        DateOnly nextDueDate = new(2026, 3, 15);
        bill.NextDueDate = nextDueDate;
        RecurringBillCandidate candidate = Candidate();
        candidate.AverageAmount = 1950M;
        candidate.SuggestedFrequency = BillFrequencies.Weekly;

        RecurringBillMatcher.AttachCandidate(bill, candidate);
        RecurringBillMatcher.AttachCandidate(bill, candidate);

        Assert.AreEqual(7, bill.RecurringBillId);
        Assert.AreEqual("Home mortgage", bill.BillName);
        Assert.AreEqual(1800M, bill.TypicalAmount);
        Assert.AreEqual(BillFrequencies.Monthly, bill.Frequency);
        Assert.AreEqual(nextDueDate, bill.NextDueDate);
        Assert.AreEqual("NEW SERVICER HOME 101", bill.SourceDescription);
        CollectionAssert.AreEqual(new[] { "OLD SERVICER HOME 101" }, bill.SourceDescriptionAliases.ToArray());
    }

    [TestMethod]
    public void AttachingCandidateRequiresMatchingScopeAndAnExistingExactDescription()
    {
        RecurringBill bill = Mortgage();
        RecurringBillCandidate candidate = Candidate();
        candidate.AccountId = 2;
        Assert.ThrowsExactly<InvalidOperationException>(() => RecurringBillMatcher.AttachCandidate(bill, candidate));
        candidate.AccountId = 1;
        candidate.Category = "Uncategorized";
        Assert.ThrowsExactly<InvalidOperationException>(() => RecurringBillMatcher.AttachCandidate(bill, candidate));
        candidate.Category = "Mortgage";
        bill.SourceDescription = null;
        bill.SourceDescriptionAliases = [];
        Assert.ThrowsExactly<InvalidOperationException>(() => RecurringBillMatcher.AttachCandidate(bill, candidate));
    }

    [TestMethod]
    public void OverlapDetectionAllowsSeparateDescriptionsAndAccountsButRejectsSharedAliases()
    {
        RecurringBill first = Mortgage();
        RecurringBill second = Mortgage();
        second.SourceDescription = "PROPERTY 202";
        second.SourceDescriptionAliases = [];
        Assert.IsFalse(RecurringBillMatcher.HasOverlappingIdentity(first, second));
        second.SourceDescriptionAliases = [" old servicer home 101 "];
        second.Category = "Different category";
        Assert.IsTrue(RecurringBillMatcher.HasOverlappingIdentity(first, second));
        second.AccountId = 2;
        Assert.IsFalse(RecurringBillMatcher.HasOverlappingIdentity(first, second));
        first.AccountId = null;
        Assert.IsTrue(RecurringBillMatcher.HasOverlappingIdentity(first, second));
        second.IsActive = false;
        Assert.IsFalse(RecurringBillMatcher.HasOverlappingIdentity(first, second));
    }

    [TestMethod]
    public void LegacyOverlappingBillsBlockSalaryAndMarkRunwayIncomplete()
    {
        RecurringBill first = Mortgage();
        RecurringBill second = Mortgage();
        second.RecurringBillId = 8;
        second.BillName = "Duplicate mortgage";
        ReportData data = new() { Bills = [first, second] };
        SalarySpendingAnalysis salary = new FinancialReportCalculator(data).SalarySpending(new() { SpendingMode = SalarySpendingMode.RequiredBillsOnly });
        Assert.IsFalse(salary.CanEstimate);
        Assert.IsTrue(salary.Warnings.Any(warning => warning.Contains("overlapping payment identities", StringComparison.Ordinal)));

        DateOnly start = new(2026, 3, 1);
        RunwayAnalysisResult runway = new RunwayCalculator().Calculate(
            [new Account { AccountType = "Checking", AvailableBalance = 10000M, BalanceAsOfDate = start }],
            [first, second], "Strict", 0M, 1, start);
        Assert.IsFalse(runway.HasCompleteInputs);
        Assert.IsTrue(runway.Assumptions.Any(warning => warning.Contains("overlapping payment identities", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void AnAliasInADifferentCategoryNeedsReviewAndRemainsInHistory()
    {
        ReportTransaction misclassified = Payment(2, 2, "NEW SERVICER HOME 101", category: "Uncategorized");
        ReportData data = new() { Bills = [Mortgage()], Transactions = [Payment(1, 1, "OLD SERVICER HOME 101"), misclassified] };
        SalarySpendingFilter filter = Combined();
        filter.SelectedSpendingBuckets =
        [
            new SalarySpendingScopeOption { Category = "Mortgage", SpendingClass = "Housing" },
            new SalarySpendingScopeOption { Category = "Uncategorized", SpendingClass = "Housing" }
        ];

        SalarySpendingAnalysis result = new FinancialReportCalculator(data).SalarySpending(filter);
        Assert.IsFalse(result.CanEstimate);
        Assert.AreEqual(900M, result.TransactionMonthlySpending);
        Assert.IsTrue(result.Warnings.Any(warning => warning.Contains("Review classification", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ReplacingPrimaryNamesCanPreserveTheirHistoryWithoutAddingAnotherBill()
    {
        RecurringBill bill = Mortgage();
        string previousPayee = bill.Payee;
        string? previousDescription = bill.SourceDescription;
        bill.Payee = "Latest servicer";
        bill.SourceDescription = "LATEST SERVICER HOME 101";
        RecurringBillMatcher.PreserveFormerNames(bill, previousPayee, previousDescription);
        RecurringBillMatcher.PreserveFormerNames(bill, previousPayee, previousDescription);
        CollectionAssert.AreEqual(new[] { "Old servicer", "New servicer" }, bill.PayeeAliases.ToArray());
        CollectionAssert.AreEqual(new[] { "OLD SERVICER HOME 101", "NEW SERVICER HOME 101" }, bill.SourceDescriptionAliases.ToArray());
        Assert.AreEqual(7, bill.RecurringBillId);
        Assert.AreEqual(1800M, bill.TypicalAmount);
        Assert.AreEqual(BillFrequencies.Monthly, bill.Frequency);
    }

    [TestMethod]
    public void PayeeOnlyRenameRequiresReviewedFormerDescriptionsBeforeExactMatching()
    {
        RecurringBill updated = Mortgage();
        updated.SourceDescriptionAliases = [];
        updated.PayeeAliases = [];
        Assert.ThrowsExactly<InvalidOperationException>(() => RecurringBillMatcher.PreserveFormerNames(updated, "Old servicer", null));
        Assert.ThrowsExactly<InvalidOperationException>(() => RecurringBillMatcher.PreserveFormerNames(updated, "Old servicer", null,
            descriptionConversionReviewed: true));

        updated.SourceDescriptionAliases = ["OLD SERVICER HOME 101"];
        Assert.ThrowsExactly<InvalidOperationException>(() => RecurringBillMatcher.PreserveFormerNames(updated, "Old servicer", null));
        RecurringBillMatcher.PreserveFormerNames(updated, "Old servicer", null, descriptionConversionReviewed: true);

        ReportData data = new()
        {
            Bills = [updated],
            Transactions = [Payment(1, 1, "OLD SERVICER HOME 101"), Payment(2, 2, "NEW SERVICER HOME 101")]
        };
        SalarySpendingAnalysis result = new FinancialReportCalculator(data).SalarySpending(Combined());
        Assert.IsTrue(result.CanEstimate);
        Assert.AreEqual(0M, result.TransactionMonthlySpending);
        Assert.AreEqual(1800M, result.TargetMonthlySpending);
        Assert.HasCount(1, result.InputLines.Where(line => line.Source == "Required Bill").ToArray());
    }

    [TestMethod]
    public void AFormerDescriptionStoredOnlyAsAnAliasIsAlreadyAnExactMatch()
    {
        RecurringBill bill = Mortgage();
        RecurringBillMatcher.PreserveFormerNames(bill, "Old servicer", null, ["OLD SERVICER HOME 101"]);
        Assert.IsTrue(RecurringBillMatcher.Matches(Expense("OLD SERVICER HOME 101"), bill));
        Assert.IsTrue(RecurringBillMatcher.Matches(Expense("NEW SERVICER HOME 101"), bill));
    }

    [TestMethod]
    public void AliasNormalizationTrimsDeduplicatesAndRejectsOversizedInputs()
    {
        RecurringBill bill = Mortgage();
        bill.PayeeAliases = ["  Old servicer ", "OLD SERVICER", "New servicer", " "];
        bill.SourceDescriptionAliases = [" OLD SERVICER HOME 101 ", "old servicer home 101", "NEW SERVICER HOME 101"];
        RecurringBillMatcher.NormalizeAliases(bill);
        CollectionAssert.AreEqual(new[] { "Old servicer" }, bill.PayeeAliases.ToArray());
        CollectionAssert.AreEqual(new[] { "OLD SERVICER HOME 101" }, bill.SourceDescriptionAliases.ToArray());
        bill.PayeeAliases = [new string('x', 301)];
        Assert.ThrowsExactly<InvalidOperationException>(() => RecurringBillMatcher.NormalizeAliases(bill));
        bill.PayeeAliases = Enumerable.Range(1, 51).Select(index => $"Payee {index}").ToArray();
        Assert.ThrowsExactly<InvalidOperationException>(() => RecurringBillMatcher.NormalizeAliases(bill));
    }

    private static RecurringBill Mortgage()
    {
        return new RecurringBill
        {
            RecurringBillId = 7,
            AccountId = 1,
            BillName = "Home mortgage",
            Payee = "New servicer",
            PayeeAliases = ["Old servicer"],
            SourceDescription = "NEW SERVICER HOME 101",
            SourceDescriptionAliases = ["OLD SERVICER HOME 101"],
            Category = "Mortgage",
            SpendingClass = "Housing",
            RequirementType = BillRequirementTypes.Debt,
            TypicalAmount = 1800M
        };
    }

    private static ReportTransaction Payment(long id, int month, string description, decimal amount = 1800M, string category = "Mortgage")
    {
        return new ReportTransaction
        {
            Id = id,
            AccountId = 1,
            AccountNickname = "Checking",
            AccountType = "Checking",
            Date = new DateOnly(2026, month, 15),
            Amount = -amount,
            Description = description,
            Merchant = "New servicer",
            Category = category,
            SpendingClass = "Housing"
        };
    }

    private static ExpenseDetailItem Expense(string description)
    {
        return new ExpenseDetailItem { AccountId = 1, Category = "Mortgage", SpendingClass = "Housing", Description = description };
    }

    private static RecurringBillCandidate Candidate()
    {
        return new RecurringBillCandidate { AccountId = 1, Payee = "Old servicer", SourceDescription = "OLD SERVICER HOME 101", Category = "Mortgage", SpendingClass = "Housing" };
    }

    private static SalarySpendingFilter Combined()
    {
        return new SalarySpendingFilter
        {
            FromDate = new DateOnly(2026, 1, 1),
            ToDate = new DateOnly(2026, 2, 28),
            SpendingMode = SalarySpendingMode.RequiredBillsPlusSelectedCategories,
            SelectedSpendingBuckets = [new SalarySpendingScopeOption { Category = "Mortgage", SpendingClass = "Housing" }]
        };
    }
}
