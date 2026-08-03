SELECT COUNT(*)
FROM dbo.RecurringBill
WHERE IsActive = 1
  AND IsRequired = 1
  AND (TypicalAmount IS NULL OR TypicalAmount <= 0);
