SELECT CAST(a.AccountId AS nvarchar(30)) AS GroupKey, CAST(N'Account' AS nvarchar(50)) AS Source,
       CAST(NULL AS bigint) AS TransactionId, a.AccountId, a.AccountNickname AS Account, a.AccountType, a.AccountOwner,
       a.BalanceAsOfDate AS [Date], COALESCE(a.CurrentBalance, a.PrincipalBalance, a.AvailableBalance) AS Amount,
       CAST(NULL AS nvarchar(100)) AS [Type], CAST(N'Balance snapshot date missing, stale, or in the future' AS nvarchar(1000)) AS [Description],
       CAST(NULL AS nvarchar(200)) AS Category, CAST(NULL AS nvarchar(100)) AS CategoryType,
       CAST(NULL AS nvarchar(50)) AS IncomeOwner, CAST(NULL AS nvarchar(1000)) AS Memo,
       CAST(N'Update the balance and its as-of date from a statement, then reconcile the statement period. Runway uses the saved snapshot; imports do not advance that balance automatically.' AS nvarchar(1000)) AS Details
FROM dbo.Account AS a
WHERE a.IsActive = 1
  AND (a.CurrentBalance IS NOT NULL OR a.PrincipalBalance IS NOT NULL OR a.AvailableBalance IS NOT NULL)
  AND (a.BalanceAsOfDate IS NULL OR a.BalanceAsOfDate < DATEADD(day, -31, CONVERT(date, SYSDATETIME()))
       OR a.BalanceAsOfDate > CONVERT(date, SYSDATETIME()))
ORDER BY a.AccountNickname;
