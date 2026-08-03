SELECT
    CAST(a.AccountId AS nvarchar(30)) AS GroupKey,
    CAST(N'Account' AS nvarchar(50)) AS Source,
    CAST(NULL AS bigint) AS TransactionId,
    a.AccountId,
    a.AccountNickname AS Account,
    a.AccountType,
    a.AccountOwner,
    CAST(NULL AS date) AS [Date],
    a.CurrentInterestRate AS Amount,
    CAST(NULL AS nvarchar(100)) AS [Type],
    CAST(N'Interest/APR missing' AS nvarchar(1000)) AS [Description],
    CAST(NULL AS nvarchar(200)) AS Category,
    CAST(NULL AS nvarchar(100)) AS CategoryType,
    CAST(NULL AS nvarchar(50)) AS IncomeOwner,
    CAST(NULL AS nvarchar(1000)) AS Memo,
    CAST(N'Enter the current APY/APR/rate when it materially affects projections.' AS nvarchar(1000)) AS Details
FROM dbo.Account AS a
WHERE a.IsActive = 1
  AND (a.AccountType LIKE N'%Savings%' OR a.AccountType LIKE N'%Credit%' OR a.AccountType LIKE N'%Loan%' OR a.AccountType LIKE N'%Mortgage%')
  AND a.CurrentInterestRate IS NULL
ORDER BY a.AccountNickname;
