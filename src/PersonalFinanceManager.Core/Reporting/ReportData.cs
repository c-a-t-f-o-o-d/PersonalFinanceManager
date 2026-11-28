using PersonalFinanceManager.Core.Models;

namespace PersonalFinanceManager.Core.Reporting;

public sealed class ReportTransaction
{
    public long Id { get; init; }
    public int AccountId { get; init; }
    public string AccountNickname { get; init; } = string.Empty;
    public string AccountType { get; init; } = string.Empty;
    public string AccountOwner { get; init; } = OwnerValues.Unassigned;
    public DateOnly Date { get; init; }
    public decimal Amount { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? Category { get; init; }
    public string? SpendingClass { get; init; }
    public string? Merchant { get; init; }
    public string? IncomeOwner { get; init; }
    public bool IsIncome { get; init; }
    public bool IsTransfer { get; init; }
    public bool ClassificationIsManual { get; init; }
    public bool LegacyLinkTreatment { get; init; }
}

public sealed class ReportData
{
    public IReadOnlyList<ReportTransaction> Transactions { get; init; } = [];
    public IReadOnlyList<TransactionLinkRecord> Links { get; init; } = [];
    public IReadOnlyList<RecurringBill> Bills { get; init; } = [];
}

public sealed class ExpenseReportResult
{
    public ReportPeriod Period { get; init; } = new(null, null, false);
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public ExpenseMetricSummary Metrics { get; init; } = new();
    public IReadOnlyList<ExpenseSummaryItem> Summary { get; init; } = [];
    public IReadOnlyList<MonthlyExpenseTotalItem> Monthly { get; init; } = [];
    public IReadOnlyList<ExpenseDetailItem> Details { get; init; } = [];
}

public sealed class CashFlowReportResult
{
    public ReportPeriod Period { get; init; } = new(null, null, false);
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public MoneyInOutSummary Summary { get; init; } = new();
    public IReadOnlyList<MoneyInOutMonthlyItem> Monthly { get; init; } = [];
    public IReadOnlyList<MoneyInOutDetailItem> Details { get; init; } = [];
}
