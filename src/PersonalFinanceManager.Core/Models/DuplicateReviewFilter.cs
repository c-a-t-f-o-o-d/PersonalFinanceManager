namespace PersonalFinanceManager.Core.Models
{
    public sealed class DuplicateReviewFilter
    {
        public int? AccountId { get; set; }
        public string? Status { get; set; } = DuplicateReviewStatuses.NeedsReview;
        public string? SearchText { get; set; }
        public int MaxRows { get; set; } = 500;
    }

    public static class DuplicateReviewStatuses
    {
        public const string NeedsReview = "NeedsReview";
        public const string ImportDuplicateReview = "DuplicateReview";
        public const string CrossAccountReview = "CrossAccountReview";
        public const string ExactDuplicateSkipped = "ExactDuplicateSkipped";
        public const string DuplicateAccepted = "DuplicateAccepted";
        public const string DuplicateIgnored = "DuplicateIgnored";
        public const string CrossAccountKept = "CrossAccountKept";
        public const string CrossAccountCandidateDeleted = "CrossAccountCandidateDeleted";
        public const string CrossAccountExistingDeleted = "CrossAccountExistingDeleted";
    }
}
