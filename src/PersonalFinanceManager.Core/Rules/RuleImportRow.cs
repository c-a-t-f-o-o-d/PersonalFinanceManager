using PersonalFinanceManager.Core.Models;

namespace PersonalFinanceManager.Core.Rules
{
    public sealed class RuleImportRow
    {
        public int RowNumber { get; set; }
        public string RuleName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string DescriptionMatchMode { get; set; } = RuleMatchModes.Exact;
        public string TypeMatchMode { get; set; } = RuleMatchModes.Exact;
        public string? Category { get; set; }
        public string? SpendingClass { get; set; }
        public string? Merchant { get; set; }
        public int Priority { get; set; } = 100;
        public bool? IsIncome { get; set; }
        public bool? IsTransfer { get; set; }
        public bool Active { get; set; } = true;
        public string? Notes { get; set; }
    }

    public sealed class RuleImportResult
    {
        public List<RuleImportRow> Rows { get; } = new();
        public List<string> Errors { get; } = new();
    }
}
