SELECT RuleId, RuleName, Priority, MatchDescription, MatchType, DescriptionMatchMode,
       TypeMatchMode, Category, SpendingClass, Merchant, IsIncome, IsTransfer,
       IsActive, Notes, CreatedAtUtc, UpdatedAtUtc
FROM dbo.FinancialRule
ORDER BY Priority, RuleName;
