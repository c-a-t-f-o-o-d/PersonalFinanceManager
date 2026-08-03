namespace PersonalFinanceManager.Core.Models
{
    public sealed class DataHealthDetailRow
    {
        public string GroupKey { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public long? TransactionId { get; set; }
        public int? AccountId { get; set; }
        public string? Account { get; set; }
        public string? AccountType { get; set; }
        public string? AccountOwner { get; set; }
        public DateOnly? Date { get; set; }
        public decimal? Amount { get; set; }
        public string? Type { get; set; }
        public string? Description { get; set; }
        public string? Category { get; set; }
        public string? CategoryType { get; set; }
        public string? IncomeOwner { get; set; }
        public string? Memo { get; set; }
        public string? Details { get; set; }
    }
}
