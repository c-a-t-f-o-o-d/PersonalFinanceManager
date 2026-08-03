namespace PersonalFinanceManager.Core.Models
{
    public sealed class SalarySpendingScopeOption
    {
        public string Category { get; set; } = string.Empty;
        public string SpendingClass { get; set; } = string.Empty;
        public decimal MonthlyAverage { get; set; }
        public decimal TotalAmount { get; set; }
        public int TransactionCount { get; set; }
        public int ActiveMonthCount { get; set; }
        public int PeriodMonthCount { get; set; }
        public string DisplayName => string.IsNullOrWhiteSpace(SpendingClass) ? $"{Category}  ({MonthlyAverage:C0}/mo avg)" : $"{Category} / {SpendingClass}  ({MonthlyAverage:C0}/mo avg)";
    }
}
