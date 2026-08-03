namespace PersonalFinanceManager.Core.Models
{
    public sealed class TransactionFilter
    {
        public int? AccountId { get; set; }
        public DateOnly? FromDate { get; set; }
        public DateOnly? ToDate { get; set; }
        public string? SearchText { get; set; }
        public string? Type { get; set; }
        public string? Category { get; set; }
        public string? SpendingClass { get; set; }
        public bool UncategorizedOnly { get; set; }
        public bool? IsIncome { get; set; }
        public bool? IsTransfer { get; set; }
        public bool? IsManual { get; set; }
        public string? IncomeOwner { get; set; }
        public int MaxRows { get; set; } = 1000;
    }
}
