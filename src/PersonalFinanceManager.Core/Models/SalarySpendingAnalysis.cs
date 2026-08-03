namespace PersonalFinanceManager.Core.Models
{
    public sealed class SalarySpendingAnalysis
    {
        public SalarySpendingMode SpendingMode { get; set; }
        public decimal TargetMonthlySpending { get; set; }
        public decimal TransactionMonthlySpending { get; set; }
        public decimal RequiredBillsMonthlySpending { get; set; }
        public int ActiveMonthCount { get; set; }
        public DateOnly? FirstTransactionDate { get; set; }
        public DateOnly? LastTransactionDate { get; set; }
        public IReadOnlyList<MonthlyExpenseTotalItem> MonthlyTrend { get; set; } = [];
        public IReadOnlyList<SalarySpendingInputLine> InputLines { get; set; } = [];
    }
}
