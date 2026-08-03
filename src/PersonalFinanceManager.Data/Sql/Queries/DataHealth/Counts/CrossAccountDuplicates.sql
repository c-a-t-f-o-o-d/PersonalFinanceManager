SELECT COUNT(*)
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
  );
