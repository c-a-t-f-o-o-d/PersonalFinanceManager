namespace PersonalFinanceManager.Core.Models
{
    public sealed class MoneyInOutSummary
    {
        public decimal TotalMoneyIn { get; set; }
        public decimal TotalMoneyOut { get; set; }
        public decimal NetCashFlow => TotalMoneyIn - TotalMoneyOut;
        public int TransactionCount { get; set; }
        public DateOnly? FirstTransactionDate { get; set; }
        public DateOnly? LastTransactionDate { get; set; }
    }
}
