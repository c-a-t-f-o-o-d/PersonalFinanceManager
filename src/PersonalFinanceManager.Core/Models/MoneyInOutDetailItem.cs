namespace PersonalFinanceManager.Core.Models
{
    public sealed class MoneyInOutDetailItem
    {
        public long ImportedTransactionId { get; set; }
        public string AccountNickname { get; set; } = string.Empty;
        public DateOnly TransactionDate { get; set; }
        public decimal Amount { get; set; }
        public string Direction => Amount >= 0M ? "In" : "Out";
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? SpendingClass { get; set; }
        public string? Merchant { get; set; }
    }
}
