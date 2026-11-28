SELECT
    (SELECT COUNT(*) FROM dbo.Account) AS AccountCount,
    (SELECT COUNT(*) FROM dbo.ImportedTransaction) AS TransactionCount,
    (SELECT COUNT(*) FROM dbo.FinancialRule WHERE StarterRuleCode IS NULL) AS RuleCount,
    (SELECT COUNT(*) FROM dbo.RecurringBill) AS BillCount;
