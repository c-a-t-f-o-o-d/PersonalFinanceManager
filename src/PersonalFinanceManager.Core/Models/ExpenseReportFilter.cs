namespace PersonalFinanceManager.Core.Models
{
    public sealed class ExpenseReportFilter
    {
        public int? AccountId { get; set; }
        public DateOnly? FromDate { get; set; }
        public DateOnly? ToDate { get; set; }
        public string BreakdownMode { get; set; } = ExpenseBreakdownModes.Category;
        public int MaxDetailRows { get; set; } = 1000;
    }

    public static class ExpenseBreakdownModes
    {
        public const string Category = "Category";
        public const string CategoryType = "CategoryType";
    }
}
