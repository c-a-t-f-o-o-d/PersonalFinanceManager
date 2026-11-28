SELECT CandidateTransactionId, ExistingTransactionId
FROM dbo.CrossAccountDuplicateReview WITH (UPDLOCK, HOLDLOCK)
WHERE CrossAccountDuplicateReviewId = @CrossAccountDuplicateReviewId
  AND [Status] = N'Pending';
