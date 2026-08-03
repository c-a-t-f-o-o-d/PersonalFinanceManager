namespace PersonalFinanceManager.Core.Models
{
    public sealed class ImportedTransaction
    {
        public long ImportedTransactionId { get; set; }
        public int AccountId { get; set; }
        public long? ImportBatchId { get; set; }
        public string? AccountNickname { get; set; }
        public DateOnly TransactionDate { get; set; }
        public DateOnly? PostingDate { get; set; }
        public TimeOnly? TransactionTime { get; set; }
        public decimal Amount { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? ExternalTransactionId { get; set; }
        public string? FundingSource { get; set; }
        public string? SourceCategory { get; set; }
        public string? Memo { get; set; }
        public int? AppliedRuleId { get; set; }
        public string? Category { get; set; }
        public string? SpendingClass { get; set; }
        public string? Merchant { get; set; }
        public bool IsIncome { get; set; }
        public bool IsTransfer { get; set; }
        public bool IsManual { get; set; }
        public string? IncomeOwner { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
    }
}
