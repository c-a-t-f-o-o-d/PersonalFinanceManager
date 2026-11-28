SELECT TOP (100) * FROM dbo.AccountReconciliation
WHERE AccountId = @AccountId
ORDER BY RecordedAtUtc DESC, AccountReconciliationId DESC;
