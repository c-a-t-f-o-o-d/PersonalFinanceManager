SELECT CandidateTransactionId, ExistingTransactionId
FROM dbo.CrossAccountDuplicateReview
WHERE CrossAccountDuplicateReviewId = @CrossAccountDuplicateReviewId
  AND [Status] = N'Pending';
