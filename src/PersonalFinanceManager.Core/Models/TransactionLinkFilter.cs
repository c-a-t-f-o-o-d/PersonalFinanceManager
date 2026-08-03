namespace PersonalFinanceManager.Core.Models
{
    public sealed class TransactionLinkFilter
    {
        public int? AccountId { get; set; }
        public string? LinkType { get; set; }
        public string? SearchText { get; set; }
        public int MaxRows { get; set; } = 1000;
    }
}
