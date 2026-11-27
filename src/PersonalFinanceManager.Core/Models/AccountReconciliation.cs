namespace PersonalFinanceManager.Core.Models
{
    public sealed class AccountReconciliationRequest
    {
        public int AccountId { get; set; }
        public DateOnly FromDate { get; set; }
        public DateOnly ToDate { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal ClosingBalance { get; set; }
    }

    public sealed class AccountReconciliation
    {
        public long AccountReconciliationId { get; set; }
        public int AccountId { get; set; }
        public DateOnly FromDate { get; set; }
        public DateOnly ToDate { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal ClosingBalance { get; set; }
        public decimal ExpectedClosingBalance { get; set; }
        public decimal NetActivity { get; set; }
        public int TransactionCount { get; set; }
        public int DateFallbackCount { get; set; }
        public bool UsesLiabilityBalances { get; set; }
        public DateTime RecordedAtUtc { get; set; }
        public decimal Difference => ClosingBalance - ExpectedClosingBalance;
        public bool IsBalanced => Difference == 0M;
    }
}
