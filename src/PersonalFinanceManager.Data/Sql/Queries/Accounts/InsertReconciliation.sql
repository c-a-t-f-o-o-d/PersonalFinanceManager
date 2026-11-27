INSERT INTO dbo.AccountReconciliation
    (AccountId, FromDate, ToDate, OpeningBalance, ClosingBalance, ExpectedClosingBalance,
     NetActivity, TransactionCount, DateFallbackCount, UsesLiabilityBalances)
OUTPUT INSERTED.AccountReconciliationId
VALUES
    (@AccountId, @FromDate, @ToDate, @OpeningBalance, @ClosingBalance, @ExpectedClosingBalance,
     @NetActivity, @TransactionCount, @DateFallbackCount, @UsesLiabilityBalances);
