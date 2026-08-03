namespace PersonalFinanceManager.Core.Models
{
    public sealed class RunwayProjectionPoint
    {
        public DateOnly ProjectionDate { get; set; }
        public decimal StartingBalance { get; set; }
        public decimal MoneyIn { get; set; }
        public decimal RequiredExpenses { get; set; }
        public decimal EndingBalance { get; set; }
        public string PeriodLabel => ProjectionDate.ToString("yyyy-MM");
    }
}
