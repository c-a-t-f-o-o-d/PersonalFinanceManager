SELECT
    CASE
        WHEN NULLIF(LTRIM(RTRIM(t.IncomeOwner)), N'') IS NOT NULL THEN LTRIM(RTRIM(t.IncomeOwner))
        WHEN a.AccountOwner IN (N'Me', N'Spouse', N'Joint', N'Household') THEN a.AccountOwner
        ELSE N'Unassigned'
    END AS IncomeOwner,
    SUM(t.Amount) AS TotalNetIncome,
    COUNT(*) AS TransactionCount
FROM dbo.ImportedTransaction AS t
INNER JOIN dbo.Account AS a ON a.AccountId = t.AccountId
WHERE t.IsIncome = 1
  AND t.IsTransfer = 0
  AND t.Amount > 0
  AND (@AccountId IS NULL OR t.AccountId = @AccountId)
  AND (@FromDate IS NULL OR t.TransactionDate >= @FromDate)
  AND (@ToDate IS NULL OR t.TransactionDate <= @ToDate)
GROUP BY CASE
    WHEN NULLIF(LTRIM(RTRIM(t.IncomeOwner)), N'') IS NOT NULL THEN LTRIM(RTRIM(t.IncomeOwner))
    WHEN a.AccountOwner IN (N'Me', N'Spouse', N'Joint', N'Household') THEN a.AccountOwner
    ELSE N'Unassigned'
END
ORDER BY TotalNetIncome DESC;
