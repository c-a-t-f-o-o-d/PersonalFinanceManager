using PersonalFinanceManager.Core.Imports;

namespace PersonalFinanceManager.Core.Models
{
    public sealed class ImportSummary
    {
        public long ImportBatchId { get; set; }
        public int ParsedRows { get; set; }
        public int InsertedRows { get; set; }
        public int DuplicateRows { get; set; }
        public int ExactDuplicateRows { get; set; }
        public int DuplicateReviewRows { get; set; }
        public int ErrorRows { get; set; }
        public int LinkedFundingRows { get; set; }
        public int NormalizedLinkedFundingRows { get; set; }
        public int NormalizedLikelyPayPalFundingRows { get; set; }
        public int CreditCardFundingLinks { get; set; }
        public int CreditCardPaymentAllocations { get; set; }
        public int NormalizedCreditCardPaymentRows { get; set; }
        public int NormalizedLikelyCreditCardPaymentRows { get; set; }
        public int BankTransferLinks { get; set; }
        public int NormalizedBankTransferRows { get; set; }
        public bool FileHashPreviouslyImported { get; set; }
        public IReadOnlyList<ImportedTransaction> PreviewRows { get; set; } = Array.Empty<ImportedTransaction>();
        public IReadOnlyList<ParseError> ParseWarnings { get; set; } = Array.Empty<ParseError>();
        public IReadOnlyList<ParseError> ParseErrors { get; set; } = Array.Empty<ParseError>();
    }
}
