SELECT RuleId, RuleName, Priority, MatchDescription, MatchType, DescriptionMatchMode,
       TypeMatchMode, Category, SpendingClass, Merchant, IsIncome, IsTransfer,
       IncomeOwner, IsActive, Notes, StarterRuleCode, CreatedAtUtc, UpdatedAtUtc
FROM dbo.FinancialRule
ORDER BY Priority, RuleName;
