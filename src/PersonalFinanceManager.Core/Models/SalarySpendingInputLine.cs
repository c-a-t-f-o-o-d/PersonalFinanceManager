namespace PersonalFinanceManager.Core.Models
{
    public sealed class SalarySpendingInputLine
    {
        public string Source { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? SpendingClass { get; set; }
        public decimal MonthlyAmount { get; set; }
        public decimal AnnualizedAmount { get; set; }
        public int Count { get; set; }
        public string Notes { get; set; } = string.Empty;
    }
}
