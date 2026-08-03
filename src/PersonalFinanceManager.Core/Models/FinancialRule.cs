namespace PersonalFinanceManager.Core.Models
{
    public sealed class FinancialRule
    {
        public int RuleId { get; set; }
        public string RuleName { get; set; } = string.Empty;
        public int Priority { get; set; } = 100;
        public string MatchDescription { get; set; } = string.Empty;
        public string? MatchType { get; set; }
        public string DescriptionMatchMode { get; set; } = RuleMatchModes.Exact;
        public string TypeMatchMode { get; set; } = RuleMatchModes.Exact;
        public string? Category { get; set; }
        public string? SpendingClass { get; set; }
        public string? Merchant { get; set; }
        public bool? IsIncome { get; set; }
        public bool? IsTransfer { get; set; }
        public string? IncomeOwner { get; set; }
        public bool IsActive { get; set; } = true;
        public string? Notes { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    public static class RuleMatchModes
    {
        public const string Exact = "Exact";
        public const string Contains = "Contains";
        public const string StartsWith = "StartsWith";
    }
}
