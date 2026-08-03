namespace PersonalFinanceManager.Core.Models
{
    public sealed class MoneyInOutMonthlyItem
    {
        public DateOnly MonthStart { get; set; }
        public decimal MoneyIn { get; set; }
        public decimal MoneyOut { get; set; }
        public decimal NetCashFlow => MoneyIn - MoneyOut;
        public int TransactionCount { get; set; }
        public string MonthLabel => MonthStart.ToString("yyyy-MM");
    }
}
