namespace PersonalFinanceManager.Core.Models
{
    public sealed class DemoDataLoadResult
    {
        public int AccountsCreated { get; set; }
        public int TransactionsCreated { get; set; }
        public int RulesCreated { get; set; }
        public int BillsCreated { get; set; }

        public string Message => $"Demo data created: {AccountsCreated} accounts, {TransactionsCreated} transactions, {RulesCreated} rules, {BillsCreated} bills.";
    }
}
