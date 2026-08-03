namespace PersonalFinanceManager.Core.Models
{
    public sealed class ExpenseSummaryItem
    {
        public string GroupName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public int TransactionCount { get; set; }
        public decimal PercentOfTotal { get; set; }
    }
}
