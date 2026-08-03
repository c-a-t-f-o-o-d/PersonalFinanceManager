SELECT COUNT(*)
FROM dbo.ImportedTransaction AS t
WHERE t.IsTransfer = 1
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.TransactionLink AS l
      WHERE l.SourceTransactionId = t.ImportedTransactionId
         OR l.LinkedTransactionId = t.ImportedTransactionId
  );
