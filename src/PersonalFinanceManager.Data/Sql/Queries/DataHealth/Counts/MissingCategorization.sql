SELECT COUNT(*)
FROM dbo.ImportedTransaction
WHERE IsTransfer = 0
  AND (Category IS NULL OR LTRIM(RTRIM(Category)) = N'' OR SpendingClass IS NULL OR LTRIM(RTRIM(SpendingClass)) = N'');
