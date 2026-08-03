namespace PersonalFinanceManager.Core.Models
{
    public sealed class ExpenseDetailItem
    {
        public long ImportedTransactionId { get; set; }
        public int AccountId { get; set; }
        public string AccountNickname { get; set; } = string.Empty;
        public DateOnly TransactionDate { get; set; }
        public decimal Amount { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? SpendingClass { get; set; }
        public string? Merchant { get; set; }
    }
}
