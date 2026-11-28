SELECT COUNT(*) FROM dbo.Account AS a WHERE a.IsActive = 1
  AND (a.CurrentBalance IS NOT NULL OR a.PrincipalBalance IS NOT NULL OR a.AvailableBalance IS NOT NULL)
  AND (a.BalanceAsOfDate IS NULL OR a.BalanceAsOfDate < DATEADD(day, -31, CONVERT(date, SYSDATETIME()))
       OR a.BalanceAsOfDate > CONVERT(date, SYSDATETIME()));
