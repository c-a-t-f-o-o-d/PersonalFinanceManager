SELECT p.ImportedTransactionId, p.TransactionDate, p.Amount, p.[Description], p.[Type], p.FundingSource
FROM dbo.ImportedTransaction AS p
WHERE p.FundingSource IS NOT NULL
  AND p.Amount < 0
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.TransactionLink AS l
      WHERE l.SourceTransactionId = p.ImportedTransactionId
        AND l.LinkType = N'PayPalFunding'
  );
