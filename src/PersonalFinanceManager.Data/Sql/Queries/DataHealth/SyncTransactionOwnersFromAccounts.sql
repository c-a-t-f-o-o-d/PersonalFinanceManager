UPDATE t
SET IncomeOwner = a.AccountOwner,
    UpdatedAtUtc = SYSUTCDATETIME()
FROM dbo.ImportedTransaction AS t
INNER JOIN dbo.Account AS a ON a.AccountId = t.AccountId
WHERE a.AccountOwner IN (N'Me', N'Spouse', N'Joint', N'Household')
  AND ISNULL(t.IncomeOwner, N'') <> a.AccountOwner;

UPDATE r
SET IncomeOwner = a.AccountOwner
FROM dbo.ImportRow AS r
INNER JOIN dbo.ImportBatch AS b ON b.ImportBatchId = r.ImportBatchId
INNER JOIN dbo.Account AS a ON a.AccountId = b.AccountId
WHERE a.AccountOwner IN (N'Me', N'Spouse', N'Joint', N'Household')
  AND ISNULL(r.IncomeOwner, N'') <> a.AccountOwner;
