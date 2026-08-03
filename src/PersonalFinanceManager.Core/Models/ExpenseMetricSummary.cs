namespace PersonalFinanceManager.Core.Models
{
    public sealed class ExpenseMetricSummary
    {
        public decimal TotalExpenses { get; set; }
        public int TransactionCount { get; set; }
        public DateOnly? FirstTransactionDate { get; set; }
        public DateOnly? LastTransactionDate { get; set; }
    }
}
