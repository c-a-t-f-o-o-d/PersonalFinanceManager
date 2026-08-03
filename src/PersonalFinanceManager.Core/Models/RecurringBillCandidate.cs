namespace PersonalFinanceManager.Core.Models
{
    public sealed class RecurringBillCandidate
    {
        public int AccountId { get; set; }
        public string AccountNickname { get; set; } = string.Empty;
        public string SourceDescription { get; set; } = string.Empty;
        public string Payee { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? SpendingClass { get; set; }
        public string SuggestedRequirementType { get; set; } = BillRequirementTypes.Need;
        public decimal AverageAmount { get; set; }
        public decimal MinimumAmount { get; set; }
        public decimal MaximumAmount { get; set; }
        public DateOnly FirstSeenDate { get; set; }
        public DateOnly LastSeenDate { get; set; }
        public int TransactionCount { get; set; }
        public int MonthCount { get; set; }
        public int SuggestedDueDay { get; set; }
        public bool AlreadyExists { get; set; }
    }
}
