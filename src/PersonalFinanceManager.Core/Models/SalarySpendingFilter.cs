namespace PersonalFinanceManager.Core.Models
{
    public sealed class SalarySpendingFilter
    {
        public int? AccountId { get; set; }
        public DateOnly? FromDate { get; set; }
        public DateOnly? ToDate { get; set; }
        public SalarySpendingMode SpendingMode { get; set; } = SalarySpendingMode.AllExpenseTrend;
        public IReadOnlyList<string> SelectedCategories { get; set; } = [];
        public IReadOnlyList<string> SelectedCategoryTypes { get; set; } = [];
        public IReadOnlyList<SalarySpendingScopeOption> SelectedSpendingBuckets { get; set; } = [];
    }
}
