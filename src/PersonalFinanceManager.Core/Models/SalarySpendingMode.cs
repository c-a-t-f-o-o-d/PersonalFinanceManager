namespace PersonalFinanceManager.Core.Models
{
    public enum SalarySpendingMode
    {
        AllExpenseTrend = 0,
        RequiredBillsOnly = 1,
        SelectedCategoriesOnly = 2,
        RequiredBillsPlusSelectedCategories = 3
    }

    public static class SalarySpendingModeLabels
    {
        public const string AllExpenseTrend = "Full Expense Trend";
        public const string RequiredBillsOnly = "Required Bills Only";
        public const string SelectedCategoriesOnly = "Selected Spending Buckets Only";
        public const string RequiredBillsPlusSelectedCategories = "Required Bills + Selected Buckets";
    }
}
