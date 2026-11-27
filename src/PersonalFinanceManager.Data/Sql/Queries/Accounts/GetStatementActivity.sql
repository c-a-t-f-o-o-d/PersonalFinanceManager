SELECT t.ImportedTransactionId, t.AccountId, t.TransactionDate, t.PostingDate, t.Amount, b.ImportProfileCode
FROM dbo.ImportedTransaction AS t
LEFT JOIN dbo.ImportBatch AS b ON b.ImportBatchId = t.ImportBatchId
WHERE t.AccountId = @AccountId
  AND COALESCE(t.PostingDate, t.TransactionDate) BETWEEN @FromDate AND @ToDate
ORDER BY COALESCE(t.PostingDate, t.TransactionDate), t.ImportedTransactionId;
