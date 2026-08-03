namespace PersonalFinanceManager.Core.Models
{
    public sealed class RunwayBillItem
    {
        public string BillName { get; set; } = string.Empty;
        public string Payee { get; set; } = string.Empty;
        public string RequirementType { get; set; } = string.Empty;
        public string Frequency { get; set; } = string.Empty;
        public decimal TypicalAmount { get; set; }
        public decimal MonthlyEquivalent { get; set; }
    }
}
