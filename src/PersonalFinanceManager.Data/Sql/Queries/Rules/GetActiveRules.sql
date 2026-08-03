SELECT *
FROM dbo.FinancialRule
WHERE IsActive = 1
ORDER BY Priority, RuleName;
