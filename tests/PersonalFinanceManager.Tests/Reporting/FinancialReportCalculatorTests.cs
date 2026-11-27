using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Core.Reporting;

namespace PersonalFinanceManager.Tests.Reporting;

[TestClass]
public sealed class FinancialReportCalculatorTests
{
    private static readonly DateOnly January = new(2026, 1, 15);

    [TestMethod]
    public void RefundsReduceNetExpensesAndIgnoredRowsNeverReachAnyReport()
    {
        FinancialReportCalculator calculator = new(new ReportData
        {
            Transactions =
            [
                Row(1, -100M), Row(2, 40M), Row(3, -900M, category: "Ignore"),
                Row(4, 800M, category: "Ignore", income: true, owner: OwnerValues.Spouse)
            ]
        });

        ExpenseReportResult expense = calculator.Expenses(new());
        Assert.AreEqual(60M, expense.Metrics.TotalExpenses);
        Assert.AreEqual(60M, expense.Summary.Single().TotalAmount);
        Assert.AreEqual(2, expense.Metrics.TransactionCount);
        Assert.AreEqual(-60M, calculator.CashFlow(new()).Summary.NetCashFlow);
        Assert.AreEqual(60M, calculator.SalarySpending(new()).TargetMonthlySpending);
        Assert.HasCount(0, calculator.Income(new()));
    }

    [TestMethod]
    public void AveragesIncludeEmptyCalendarMonthsAndSelectedDays()
    {
        FinancialReportCalculator calculator = new(new ReportData { Transactions = [Row(1, -300M, date: new DateOnly(2026, 2, 15))] });
        ExpenseReportFilter filter = new() { FromDate = new DateOnly(2026, 1, 1), ToDate = new DateOnly(2026, 3, 31) };
        ExpenseReportResult result = calculator.Expenses(filter);
        Assert.HasCount(3, result.Monthly);
        Assert.AreEqual(0M, result.Monthly[0].TotalAmount);
        Assert.AreEqual(90, result.Period.DayCount);
        Assert.AreEqual(100M, result.Metrics.TotalExpenses / result.Period.MonthEquivalent);
        Assert.AreEqual(-100M, calculator.CashFlow(filter).Summary.NetCashFlow / result.Period.MonthEquivalent);
    }

    [TestMethod]
    public void AllTimeIncomeUsesItsFullHistoryEvenWithoutExpenseRows()
    {
        ReportTransaction[] income = Enumerable.Range(1, 12).Select(month => Row(month, 1000M, date: new DateOnly(2026, month, 15), income: true, owner: OwnerValues.Spouse)).ToArray();
        FinancialReportCalculator calculator = new(new ReportData { Transactions = income, Bills = [Bill(4000M)] });
        SalarySpendingAnalysis salary = calculator.SalarySpending(new() { SpendingMode = SalarySpendingMode.RequiredBillsOnly });
        Assert.IsTrue(salary.CanEstimate);
        Assert.AreEqual(12, salary.Period.MonthCount);
        Assert.AreEqual(1000M, salary.IncomeOwnerSummaries.Single().MonthlyAverageNetIncome);
        Assert.AreEqual(4000M, salary.TargetMonthlySpending);
    }

    [TestMethod]
    public void PayrollReversalReducesPostedNetIncome()
    {
        FinancialReportCalculator calculator = new(new ReportData
        {
            Transactions =
            [Row(1, 1000M, income: true, owner: OwnerValues.Spouse), Row(2, -200M, income: true, owner: OwnerValues.Spouse)]
        });

        Assert.AreEqual(800M, calculator.Income(new()).Single().MonthlyAverageNetIncome);
        Assert.AreEqual(0M, calculator.Expenses(new()).Metrics.TotalExpenses);
    }

    [TestMethod]
    public void CreditPurchaseAndLaterPaymentHaveDifferentSpendingAndCashDates()
    {
        FinancialReportCalculator calculator = new(new ReportData
        {
            Transactions = [Row(1, -500M, account: 2, accountType: "Credit Card"),
                Row(2, -500M, date: new DateOnly(2026, 2, 15), transfer: true),
                Row(3, 500M, date: new DateOnly(2026, 2, 15), account: 2, accountType: "Credit Card", transfer: true)],
            Links = [Link(3, 2, "CreditCardPaymentFunding")]
        });

        ExpenseReportFilter january = new() { FromDate = new DateOnly(2026, 1, 1), ToDate = new DateOnly(2026, 1, 31) };
        ExpenseReportFilter february = new() { FromDate = new DateOnly(2026, 2, 1), ToDate = new DateOnly(2026, 2, 28) };
        Assert.AreEqual(500M, calculator.Expenses(january).Metrics.TotalExpenses);
        Assert.AreEqual(0M, calculator.CashFlow(january).Summary.TotalMoneyOut);
        Assert.AreEqual(0M, calculator.Expenses(february).Metrics.TotalExpenses);
        Assert.AreEqual(500M, calculator.CashFlow(february).Summary.TotalMoneyOut);
    }

    [TestMethod]
    public void InternalCashTransfersDisappearOnlyAtHouseholdScope()
    {
        FinancialReportCalculator calculator = new(new ReportData { Transactions = [Row(1, -100M), Row(2, 100M, account: 2, accountType: "Savings")], Links = [Link(1, 2, "BankTransfer")] });
        Assert.AreEqual(0M, calculator.Expenses(new()).Metrics.TotalExpenses);
        Assert.AreEqual(0, calculator.CashFlow(new()).Summary.TransactionCount);
        Assert.AreEqual(100M, calculator.CashFlow(new() { AccountId = 1 }).Summary.TotalMoneyOut);
        Assert.AreEqual(100M, calculator.CashFlow(new() { AccountId = 2 }).Summary.TotalMoneyIn);
    }

    [TestMethod]
    public void PurchaseMirrorsNeverCreateASecondExpenseOrWalletCashMovement()
    {
        FinancialReportCalculator calculator = new(new ReportData
        {
            Transactions =
            [Row(1, -100M, account: 3, accountType: "PayPal"), Row(2, -100M, account: 2, accountType: "Credit Card")],
            Links = [Link(1, 2, "PurchaseMirror")]
        });

        Assert.AreEqual(100M, calculator.Expenses(new()).Metrics.TotalExpenses);
        Assert.AreEqual(0M, calculator.CashFlow(new()).Summary.TotalMoneyOut);
    }

    [TestMethod]
    public void PartialFundingAllocationRemovesOnlyItsAllocatedPortion()
    {
        FinancialReportCalculator calculator = new(new ReportData
        {
            Transactions =
            [Row(1, -20M, account: 3, accountType: "PayPal"), Row(2, -30M, account: 3, accountType: "PayPal"), Row(3, -100M)],
            Links = [Link(1, 3, "PurchaseFundingAllocation", 20M), Link(2, 3, "PurchaseFundingAllocation", 30M)]
        });

        ExpenseReportResult expenses = calculator.Expenses(new());
        Assert.AreEqual(100M, expenses.Metrics.TotalExpenses);
        Assert.AreEqual(50M, expenses.Details.Single(row => row.ImportedTransactionId == 3).Amount);
        Assert.AreEqual(100M, calculator.CashFlow(new()).Summary.TotalMoneyOut);
    }

    [TestMethod]
    public void PartlyFundedWalletPurchasePreservesCashPaidFromTheWalletBalance()
    {
        FinancialReportCalculator calculator = new(new ReportData
        {
            Transactions =
            [Row(1, -100M, account: 3, accountType: "PayPal"), Row(2, -40M)],
            Links = [Link(1, 2, "PurchaseFundingAllocation", 40M)]
        });

        CashFlowReportResult cash = calculator.CashFlow(new());
        Assert.AreEqual(100M, cash.Summary.TotalMoneyOut);
        Assert.AreEqual(-60M, cash.Details.Single(row => row.ImportedTransactionId == 1).Amount);
        Assert.AreEqual(100M, calculator.Expenses(new()).Metrics.TotalExpenses);
    }

    [TestMethod]
    public void ExplicitRefundLinkUsesPurchaseCategoryWithoutReducingPurchaseTwice()
    {
        FinancialReportCalculator calculator = new(new ReportData
        {
            Transactions =
            [Row(1, -100M, category: "Shopping"), Row(2, 40M, category: "Refund")],
            Links = [Link(2, 1, "PurchaseRefund", 40M)]
        });

        ExpenseReportResult report = calculator.Expenses(new());
        Assert.AreEqual(60M, report.Metrics.TotalExpenses);
        Assert.HasCount(1, report.Summary);
        Assert.AreEqual("Shopping", report.Summary[0].GroupName);
        Assert.AreEqual(60M, report.Summary[0].TotalAmount);
        Assert.AreEqual(2, report.Metrics.TransactionCount);
    }

    [TestMethod]
    public void RemovingALegacyLinkRevealsItsPreviouslyGeneratedTransferFlag()
    {
        ReportTransaction legacy = new()
        {
            Id = 1,
            AccountId = 1,
            Date = January,
            Amount = -100M,
            AccountType = "Checking",
            IsTransfer = true,
            LegacyLinkTreatment = true,
            Category = "Utilities"
        };

        FinancialReportCalculator calculator = new(new ReportData { Transactions = [legacy] });
        Assert.AreEqual(100M, calculator.Expenses(new()).Metrics.TotalExpenses);

        ReportTransaction manual = new()
        {
            Id = 1,
            AccountId = 1,
            Date = January,
            Amount = -100M,
            AccountType = "Checking",
            IsTransfer = true,
            LegacyLinkTreatment = true,
            ClassificationIsManual = true
        };

        Assert.AreEqual(0M, new FinancialReportCalculator(new ReportData { Transactions = [manual] }).Expenses(new()).Metrics.TotalExpenses);
    }

    [TestMethod]
    public void RequiredBillReplacesMatchedHistoryWhileOtherBucketSpendingRemains()
    {
        FinancialReportCalculator calculator = new(new ReportData
        {
            Transactions =
            [Row(1, -200M, description: "Electric Utility"), Row(2, -100M, description: "Gas Utility")],
            Bills = [Bill(200M)]
        });

        SalarySpendingAnalysis result = calculator.SalarySpending(Combined());
        Assert.IsTrue(result.CanEstimate);
        Assert.AreEqual(100M, result.TransactionMonthlySpending);
        Assert.AreEqual(200M, result.RequiredBillsMonthlySpending);
        Assert.AreEqual(300M, result.TargetMonthlySpending);
    }

    [TestMethod]
    public void RequiredBillCannotReplaceOtherSpendingBucketsAtTheSameMerchant()
    {
        FinancialReportCalculator calculator = new(new ReportData
        {
            Transactions =
            [
                new ReportTransaction
                {
                    Id = 1,
                    AccountId = 1,
                    AccountType = "Checking",
                    Date = January,
                    Amount = -200M,
                    Description = "Example store order",
                    Merchant = "Example store",
                    Category = "Groceries",
                    SpendingClass = "Food"
                },
                new ReportTransaction
                {
                    Id = 2,
                    AccountId = 1,
                    AccountType = "Checking",
                    Date = January,
                    Amount = -15M,
                    Description = "Example membership",
                    Merchant = "Example store",
                    Category = "Subscriptions",
                    SpendingClass = "Services" }
            ],
            Bills =
            [
                new RecurringBill
                {
                    BillName = "Membership",
                    Payee = "Example store",
                    TypicalAmount = 15M,
                    AccountId = 1,
                    Category = "Subscriptions",
                    SpendingClass = "Services"
                }
            ]
        });

        SalarySpendingAnalysis result = calculator.SalarySpending(new()
        {
            SpendingMode = SalarySpendingMode.RequiredBillsPlusSelectedCategories,
            SelectedSpendingBuckets =
            [
                new SalarySpendingScopeOption { Category = "Groceries", SpendingClass = "Food" },
                new SalarySpendingScopeOption { Category = "Subscriptions", SpendingClass = "Services" }
            ]
        });

        Assert.IsTrue(result.CanEstimate);
        Assert.AreEqual(200M, result.TransactionMonthlySpending);
        Assert.AreEqual(15M, result.RequiredBillsMonthlySpending);
        Assert.AreEqual(215M, result.TargetMonthlySpending);
    }

    [TestMethod]
    public void ExplicitBillSourceCannotReplaceAnotherSubscriptionAtTheSameMerchant()
    {
        FinancialReportCalculator calculator = new(new ReportData
        {
            Transactions =
            [
                new ReportTransaction
                {
                    Id = 1,
                    AccountId = 1,
                    AccountType = "Checking",
                    Date = January,
                    Amount = -15M,
                    Description = "Membership A",
                    Merchant = "Example store",
                    Category = "Subscriptions",
                    SpendingClass = "Services"
                },
                new ReportTransaction
                {
                    Id = 2,
                    AccountId = 1,
                    AccountType = "Checking",
                    Date = January,
                    Amount = -20M,
                    Description = "Subscription B",
                    Merchant = "Example store",
                    Category = "Subscriptions",
                    SpendingClass = "Services"
                }
            ],
            Bills =
            [
                new RecurringBill
                {
                    BillName = "Membership",
                    Payee = "Example store",
                    SourceDescription = "Membership A",
                    TypicalAmount = 15M,
                    AccountId = 1,
                    Category = "Subscriptions",
                    SpendingClass = "Services"
                }
            ]
        });

        SalarySpendingAnalysis result = calculator.SalarySpending(new()
        {
            SpendingMode = SalarySpendingMode.RequiredBillsPlusSelectedCategories,
            SelectedSpendingBuckets = [new SalarySpendingScopeOption { Category = "Subscriptions", SpendingClass = "Services" }]
        });

        Assert.IsTrue(result.CanEstimate);
        Assert.AreEqual(20M, result.TransactionMonthlySpending);
        Assert.AreEqual(15M, result.RequiredBillsMonthlySpending);
        Assert.AreEqual(35M, result.TargetMonthlySpending);
    }

    [TestMethod]
    public void AmbiguousBillOverlapBlocksASalaryRecommendation()
    {
        FinancialReportCalculator calculator = new(new ReportData { Transactions = [Row(1, -200M, description: "Unknown Utility")], Bills = [Bill(200M)] });
        SalarySpendingAnalysis result = calculator.SalarySpending(Combined());
        Assert.IsFalse(result.CanEstimate);
        Assert.IsTrue(result.Warnings.Any(warning => warning.Contains("overlap", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void NoTransactionsMeansUnknownHistoryInsteadOfZeroObservedSpending()
    {
        FinancialReportCalculator calculator = new(new ReportData());
        Assert.IsFalse(calculator.SalarySpending(new()).CanEstimate);
        Assert.IsFalse(calculator.Expenses(new()).Period.HasObservedTransactions);
        Assert.AreEqual(0, calculator.Expenses(new()).Period.MonthCount);
    }

    [TestMethod]
    public void IncomeOnlyHistoryDoesNotProveThatLivingCostsAreZero()
    {
        FinancialReportCalculator calculator = new(new ReportData { Transactions = [Row(1, 5000M, income: true)] });
        Assert.IsTrue(calculator.Expenses(new()).Period.HasObservedTransactions);
        Assert.IsFalse(calculator.SalarySpending(new()).CanEstimate);
        Assert.IsFalse(calculator.SalarySpending(new() { SpendingMode = SalarySpendingMode.RequiredBillsOnly }).CanEstimate);
    }

    [TestMethod]
    public void SpendingBucketsUseConsistentCaseInsensitiveCategoryMatching()
    {
        FinancialReportCalculator calculator = new(new ReportData { Transactions = [Row(1, -100M, category: "Utilities"), Row(2, -200M, category: "utilities")] });
        Assert.HasCount(1, calculator.SpendingOptions(new()));
        Assert.HasCount(1, calculator.SalarySpending(new()).InputLines);
        Assert.AreEqual(300M, calculator.SpendingOptions(new()).Single().MonthlyAverage);
    }

    [TestMethod]
    public void InvalidHistoricalMirrorCannotHideTheBankDebit()
    {
        FinancialReportCalculator calculator = new(new ReportData
        {
            Transactions =
            [Row(1, -60M, account: 3, accountType: "PayPal"), Row(2, -100M)],
            Links = [Link(1, 2, "PayPalFunding")]
        });

        ExpenseReportResult expense = calculator.Expenses(new());
        Assert.AreEqual(160M, expense.Metrics.TotalExpenses);
        Assert.HasCount(2, expense.Details);
        Assert.HasCount(1, expense.Warnings);
        Assert.IsTrue(calculator.NeedsLinkReview(2));
        Assert.IsFalse(calculator.SalarySpending(new()).CanEstimate);
    }

    [TestMethod]
    public void OverallocatedStoredGroupsAreAllExcludedFromFinancialTreatment()
    {
        FinancialReportCalculator calculator = new(new ReportData
        {
            Transactions =
            [Row(1, -100M, account: 3, accountType: "PayPal"), Row(2, -80M), Row(3, -80M)],
            Links = [Link(1, 2, "PurchaseFundingAllocation", 80M), Link(1, 3, "PurchaseFundingAllocation", 80M)]
        });

        Assert.AreEqual(260M, calculator.Expenses(new()).Metrics.TotalExpenses);
        Assert.IsTrue(calculator.NeedsLinkReview(1));
        Assert.IsTrue(calculator.NeedsLinkReview(2));
        Assert.IsTrue(calculator.NeedsLinkReview(3));
        Assert.HasCount(3, calculator.Expenses(new()).Details);
    }

    [TestMethod]
    public void QuarantinedLegacyRelationshipsBlockEstimatesUntilReviewed()
    {
        TransactionLinkRecord link = Link(1, 2, "ManualReview");
        link.Notes = "Review required after upgrade: historical automatic match.";

        FinancialReportCalculator calculator = new(new ReportData
        {
            Transactions =
            [Row(1, -100M, account: 3, accountType: "PayPal"), Row(2, -100M)],
            Links = [link]
        });

        Assert.AreEqual(200M, calculator.Expenses(new()).Metrics.TotalExpenses);
        Assert.HasCount(1, calculator.Expenses(new()).Warnings);
        Assert.IsFalse(calculator.SalarySpending(new()).CanEstimate);
    }

    private static SalarySpendingFilter Combined()
    {
        return new()
        {
            SpendingMode = SalarySpendingMode.RequiredBillsPlusSelectedCategories,
            SelectedSpendingBuckets = [new SalarySpendingScopeOption { Category = "Utilities", SpendingClass = "Household" }]
        };
    }

    private static RecurringBill Bill(decimal amount)
    {
        return new()
        {
            BillName = "Electric bill",
            Payee = "Electric Utility",
            Category = "Utilities",
            SpendingClass = "Household",
            TypicalAmount = amount,
            AccountId = 1
        };
    }

    private static ReportTransaction Row(long id, decimal amount, int account = 1, string accountType = "Checking", DateOnly? date = null,
        string category = "Utilities", string description = "Electric Utility", bool income = false, bool transfer = false, string? owner = null)
    {
        return new()
        {
            Id = id,
            AccountId = account,
            AccountType = accountType,
            AccountNickname = accountType,
            Date = date ?? January,
            Amount = amount,
            Category = category,
            SpendingClass = "Household",
            Description = description,
            IsIncome = income,
            IsTransfer = transfer,
            IncomeOwner = owner
        };
    }

    private static TransactionLinkRecord Link(long source, long linked, string type, decimal? amount = null)
    {
        return new()
        {
            SourceTransactionId = source,
            LinkedTransactionId = linked,
            LinkType = type,
            AllocationAmount = amount
        };
    }
}
