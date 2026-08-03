SELECT
    CAST(a.AccountId AS nvarchar(30)) AS GroupKey,
    CAST(N'Account' AS nvarchar(50)) AS Source,
    CAST(NULL AS bigint) AS TransactionId,
    a.AccountId,
    a.AccountNickname AS Account,
    a.AccountType,
    a.AccountOwner,
    CAST(NULL AS date) AS [Date],
    COALESCE(a.CurrentBalance, a.PrincipalBalance) AS Amount,
    CAST(NULL AS nvarchar(100)) AS [Type],
    CAST(N'Balance missing or incomplete' AS nvarchar(1000)) AS [Description],
    CAST(NULL AS nvarchar(200)) AS Category,
    CAST(NULL AS nvarchar(100)) AS CategoryType,
    CAST(NULL AS nvarchar(50)) AS IncomeOwner,
    CAST(NULL AS nvarchar(1000)) AS Memo,
    CAST(N'Cash accounts need CurrentBalance. Credit/loan/mortgage accounts can use CurrentBalance or PrincipalBalance.' AS nvarchar(1000)) AS Details
FROM dbo.Account AS a
WHERE a.IsActive = 1
  AND
  (
      ((a.AccountType LIKE N'%Checking%' OR a.AccountType LIKE N'%Savings%' OR a.AccountType LIKE N'%Cash%') AND a.CurrentBalance IS NULL)
      OR
      ((a.AccountType LIKE N'%Credit%' OR a.AccountType LIKE N'%Loan%' OR a.AccountType LIKE N'%Mortgage%') AND a.CurrentBalance IS NULL AND a.PrincipalBalance IS NULL)
      OR
      ((a.AccountType NOT LIKE N'%Checking%' AND a.AccountType NOT LIKE N'%Savings%' AND a.AccountType NOT LIKE N'%Cash%' AND a.AccountType NOT LIKE N'%Credit%' AND a.AccountType NOT LIKE N'%Loan%' AND a.AccountType NOT LIKE N'%Mortgage%') AND a.CurrentBalance IS NULL AND a.PrincipalBalance IS NULL)
  )
ORDER BY a.AccountNickname;
