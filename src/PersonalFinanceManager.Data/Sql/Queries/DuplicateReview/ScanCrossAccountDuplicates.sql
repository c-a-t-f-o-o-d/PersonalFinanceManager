INSERT INTO dbo.CrossAccountDuplicateReview
(
    CandidateTransactionId,
    ExistingTransactionId,
    [Status],
    Reason,
    Confidence
)
SELECT
    t1.ImportedTransactionId,
    t2.ImportedTransactionId,
    N'Pending',
    N'Same transaction fields appear under different accounts. A wallet purchase and its card funding can both be legitimate account records. Review Transaction Links first; delete a row only when the source was imported into the wrong account or twice.',
    N'Needs Review'
FROM dbo.ImportedTransaction AS t1
INNER JOIN dbo.ImportedTransaction AS t2 ON t1.ImportedTransactionId < t2.ImportedTransactionId
WHERE t1.AccountId <> t2.AccountId
  AND t1.TransactionDate = t2.TransactionDate
  AND t1.Amount = t2.Amount
  AND UPPER(LTRIM(RTRIM(t1.[Type]))) = UPPER(LTRIM(RTRIM(t2.[Type])))
  AND UPPER(LTRIM(RTRIM(t1.[Description]))) = UPPER(LTRIM(RTRIM(t2.[Description])))
  AND
  (
      NULLIF(LTRIM(RTRIM(t1.Memo)), N'') IS NULL
      OR NULLIF(LTRIM(RTRIM(t2.Memo)), N'') IS NULL
      OR UPPER(LTRIM(RTRIM(t1.Memo))) = UPPER(LTRIM(RTRIM(t2.Memo)))
  )
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.CrossAccountDuplicateReview AS existing
      WHERE
          (existing.CandidateTransactionId = t1.ImportedTransactionId AND existing.ExistingTransactionId = t2.ImportedTransactionId)
          OR
          (existing.CandidateTransactionId = t2.ImportedTransactionId AND existing.ExistingTransactionId = t1.ImportedTransactionId)
  )
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.TransactionLink AS link
      WHERE
          (link.SourceTransactionId = t1.ImportedTransactionId AND link.LinkedTransactionId = t2.ImportedTransactionId)
          OR
          (link.SourceTransactionId = t2.ImportedTransactionId AND link.LinkedTransactionId = t1.ImportedTransactionId)
  );
