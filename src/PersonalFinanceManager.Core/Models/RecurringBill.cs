namespace PersonalFinanceManager.Core.Models
{
    public sealed class RecurringBill
    {
        public int RecurringBillId { get; set; }
        public int? AccountId { get; set; }
        public string? AccountNickname { get; set; }
        public string BillName { get; set; } = string.Empty;
        public string Payee { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? SpendingClass { get; set; }
        public string RequirementType { get; set; } = BillRequirementTypes.Need;
        public decimal TypicalAmount { get; set; }
        public int? DueDay { get; set; }
        public DateOnly? NextDueDate { get; set; }
        public string Frequency { get; set; } = BillFrequencies.Monthly;
        public bool IsRequired { get; set; } = true;
        public bool IsActive { get; set; } = true;
        public string? SourceDescription { get; set; }
        public DateOnly? FirstSeenDate { get; set; }
        public DateOnly? LastSeenDate { get; set; }
        public int TransactionCount { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    public static class BillFrequencies
    {
        public const string Monthly = "Monthly";
        public const string Weekly = "Weekly";
        public const string Biweekly = "Biweekly";
        public const string Quarterly = "Quarterly";
        public const string Semiannual = "Semiannual";
        public const string Annual = "Annual";
    }

    public static class BillRequirementTypes
    {
        public const string Need = "Need";
        public const string Want = "Want";
        public const string Debt = "Debt";
        public const string Savings = "Savings";
        public const string Investment = "Investment";
        public const string Transfer = "Transfer";
        public const string Ignore = "Ignore";
    }
}
