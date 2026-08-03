namespace PersonalFinanceManager.Core.Models
{
    public sealed class DemoDataSummary
    {
        public int AccountCount { get; set; }
        public int TransactionCount { get; set; }
        public int RuleCount { get; set; }
        public int BillCount { get; set; }

        public bool HasUserData => AccountCount > 0 || TransactionCount > 0 || RuleCount > 0 || BillCount > 0;
    }
}
