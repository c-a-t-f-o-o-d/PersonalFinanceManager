namespace PersonalFinanceManager.Core.Models
{
    public sealed class TransactionLinkTransactionOption
    {
        public long ImportedTransactionId { get; set; }
        public int AccountId { get; set; }
        public string AccountNickname { get; set; } = string.Empty;
        public DateOnly TransactionDate { get; set; }
        public decimal Amount { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? CategoryType { get; set; }
        public string? FundingSource { get; set; }
        public string? ExternalTransactionId { get; set; }
        public string DisplayText => $"{ImportedTransactionId} | {TransactionDate:yyyy-MM-dd} | {AccountNickname} | {Amount:C2} | {Description}";
    }
}
