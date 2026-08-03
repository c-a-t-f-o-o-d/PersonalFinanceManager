namespace PersonalFinanceManager.Core.Models
{
    public sealed class IncomeOwnerSummary
    {
        public string IncomeOwner { get; set; } = string.Empty;
        public decimal MonthlyAverageNetIncome { get; set; }
        public decimal AnnualizedNetIncome { get; set; }
        public decimal TotalNetIncome { get; set; }
        public int TransactionCount { get; set; }
        public int PeriodMonthCount { get; set; }
    }
}
