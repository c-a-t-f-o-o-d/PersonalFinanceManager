namespace PersonalFinanceManager.Core.Models
{
    public sealed class DuplicateReviewItem
    {
        public string ReviewKind { get; set; } = string.Empty;
        public long? CrossAccountDuplicateReviewId { get; set; }
        public long ImportRowId { get; set; }
        public long ImportBatchId { get; set; }
        public int RowNumber { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? DuplicateConfidence { get; set; }
        public string? DuplicateReason { get; set; }
        public string ImportProfileCode { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public int AccountId { get; set; }
        public string AccountNickname { get; set; } = string.Empty;
        public long? CandidateTransactionId { get; set; }
        public string? CandidateAccountNickname { get; set; }
        public DateOnly? CandidateDate { get; set; }
        public DateOnly? CandidatePostingDate { get; set; }
        public TimeOnly? CandidateTime { get; set; }
        public decimal? CandidateAmount { get; set; }
        public string? CandidateType { get; set; }
        public string? CandidateDescription { get; set; }
        public string? CandidateExternalTransactionId { get; set; }
        public string? CandidateFundingSource { get; set; }
        public string? CandidateSourceCategory { get; set; }
        public string? CandidateMemo { get; set; }
        public string? CandidateCategory { get; set; }
        public string? CandidateCategoryType { get; set; }
        public string? CandidateMerchant { get; set; }
        public bool? CandidateIsIncome { get; set; }
        public bool? CandidateIsTransfer { get; set; }
        public long? ExistingTransactionId { get; set; }
        public string? ExistingAccountNickname { get; set; }
        public DateOnly? ExistingDate { get; set; }
        public DateOnly? ExistingPostingDate { get; set; }
        public TimeOnly? ExistingTime { get; set; }
        public decimal? ExistingAmount { get; set; }
        public string? ExistingType { get; set; }
        public string? ExistingDescription { get; set; }
        public string? ExistingExternalTransactionId { get; set; }
        public string? ExistingFundingSource { get; set; }
        public string? ExistingSourceCategory { get; set; }
        public string? ExistingMemo { get; set; }
        public string? ExistingCategory { get; set; }
        public string? ExistingCategoryType { get; set; }
        public string? ExistingMerchant { get; set; }
        public bool? ExistingIsIncome { get; set; }
        public bool? ExistingIsTransfer { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public bool IsCrossAccountReview => string.Equals(ReviewKind, DuplicateReviewKinds.CrossAccount, StringComparison.OrdinalIgnoreCase);
        public string CandidateDisplayAccount => CandidateAccountNickname ?? AccountNickname;
        public string ExistingDisplayAccount => ExistingAccountNickname ?? string.Empty;
        public string CandidateSummary => $"{CandidateDate:yyyy-MM-dd} {CandidateAmount:C2} {CandidateDescription}";
        public string ExistingSummary => ExistingTransactionId.HasValue ? $"{ExistingDate:yyyy-MM-dd} {ExistingAmount:C2} {ExistingDescription}" : string.Empty;
    }

    public static class DuplicateReviewKinds
    {
        public const string ImportCandidate = "Import Candidate";
        public const string CrossAccount = "Cross-Account";
    }
}
