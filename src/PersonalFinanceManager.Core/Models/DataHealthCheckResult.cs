namespace PersonalFinanceManager.Core.Models
{
    public sealed class DataHealthCheckResult
    {
        public string IssueCode { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Issue { get; set; } = string.Empty;
        public int Count { get; set; }
        public string Recommendation { get; set; } = string.Empty;
    }
}
