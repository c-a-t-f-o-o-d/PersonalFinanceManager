SELECT
    b.BillName,
    b.Payee,
    b.Category,
    b.SpendingClass,
    b.RequirementType,
    b.TypicalAmount,
    b.Frequency,
    b.AccountId
FROM dbo.RecurringBill AS b
WHERE b.IsActive = 1
  AND b.IsRequired = 1
  AND UPPER(ISNULL(b.RequirementType, N'')) <> N'IGNORE'
  AND (@AccountId IS NULL OR b.AccountId = @AccountId)
ORDER BY b.BillName;
