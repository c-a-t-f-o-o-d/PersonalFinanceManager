namespace PersonalFinanceManager.Core.Models
{
    public sealed class TransactionLinkEditModel
    {
        public long TransactionLinkId { get; set; }
        public long SourceTransactionId { get; set; }
        public long LinkedTransactionId { get; set; }
        public string LinkType { get; set; } = "PayPalFunding";
        public decimal? AllocationAmount { get; set; }
        public int ConfidenceScore { get; set; } = 100;
        public string? Notes { get; set; }
    }
}
