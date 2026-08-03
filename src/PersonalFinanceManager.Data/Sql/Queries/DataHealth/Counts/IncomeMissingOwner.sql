SELECT COUNT(*)
FROM dbo.ImportedTransaction AS t
INNER JOIN dbo.Account AS a ON a.AccountId = t.AccountId
WHERE t.IsIncome = 1
  AND t.IsTransfer = 0
  AND t.Amount > 0
  AND (t.IncomeOwner IS NULL OR LTRIM(RTRIM(t.IncomeOwner)) = N'' OR t.IncomeOwner = N'Unassigned')
  AND (a.AccountOwner IS NULL OR a.AccountOwner NOT IN (N'Me', N'Spouse', N'Joint', N'Household'));
