namespace PersonalFinanceManager.Core.Models
{
    public sealed class MonthlyExpenseTotalItem
    {
        public DateOnly MonthStart { get; set; }
        public decimal TotalAmount { get; set; }
        public int TransactionCount { get; set; }
        public string MonthLabel => MonthStart.ToString("yyyy-MM");
    }
}
