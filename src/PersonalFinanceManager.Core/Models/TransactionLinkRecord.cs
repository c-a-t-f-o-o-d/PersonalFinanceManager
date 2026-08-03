namespace PersonalFinanceManager.Core.Models
{
    public sealed class TransactionLinkRecord
    {
        public long TransactionLinkId { get; set; }
        public long SourceTransactionId { get; set; }
        public int SourceAccountId { get; set; }
        public string SourceAccountNickname { get; set; } = string.Empty;
        public DateOnly SourceDate { get; set; }
        public decimal SourceAmount { get; set; }
        public string SourceType { get; set; } = string.Empty;
        public string SourceDescription { get; set; } = string.Empty;
        public string? SourceCategory { get; set; }
        public string? SourceCategoryType { get; set; }
        public string? SourceFundingSource { get; set; }
        public string? SourceExternalTransactionId { get; set; }
        public long LinkedTransactionId { get; set; }
        public int LinkedAccountId { get; set; }
        public string LinkedAccountNickname { get; set; } = string.Empty;
        public DateOnly LinkedDate { get; set; }
        public decimal LinkedAmount { get; set; }
        public string LinkedType { get; set; } = string.Empty;
        public string LinkedDescription { get; set; } = string.Empty;
        public string? LinkedCategory { get; set; }
        public string? LinkedCategoryType { get; set; }
        public string? LinkedFundingSource { get; set; }
        public string? LinkedExternalTransactionId { get; set; }
        public string LinkType { get; set; } = string.Empty;
        public decimal? AllocationAmount { get; set; }
        public int ConfidenceScore { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
        public string SourceSummary => $"{SourceDate:yyyy-MM-dd} | {SourceAccountNickname} | {SourceAmount:C2} | {SourceDescription}";
        public string LinkedSummary => $"{LinkedDate:yyyy-MM-dd} | {LinkedAccountNickname} | {LinkedAmount:C2} | {LinkedDescription}";
    }
}
