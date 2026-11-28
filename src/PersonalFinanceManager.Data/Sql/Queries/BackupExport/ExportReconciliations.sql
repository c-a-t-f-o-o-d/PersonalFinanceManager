SELECT AccountReconciliationId, AccountId, FromDate, ToDate, OpeningBalance, ClosingBalance,
       ExpectedClosingBalance, NetActivity, TransactionCount, DateFallbackCount, UsesLiabilityBalances, RecordedAtUtc
FROM dbo.AccountReconciliation
ORDER BY AccountId, RecordedAtUtc, AccountReconciliationId;
