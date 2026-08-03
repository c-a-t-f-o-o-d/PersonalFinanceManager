namespace PersonalFinanceManager.Core.Models
{
    public sealed class DataFlowRepairSummary
    {
        public int LinkedFundingRows { get; set; }
        public int NormalizedLinkedFundingRows { get; set; }
        public int NormalizedLikelyPayPalFundingRows { get; set; }
        public int CreditCardFundingLinks { get; set; }
        public int CreditCardPaymentAllocations { get; set; }
        public int NormalizedCreditCardPaymentRows { get; set; }
        public int NormalizedLikelyCreditCardPaymentRows { get; set; }
        public int BankTransferLinks { get; set; }
        public int NormalizedBankTransferRows { get; set; }
        public int PotentialDuplicateGroups { get; set; }
    }
}
