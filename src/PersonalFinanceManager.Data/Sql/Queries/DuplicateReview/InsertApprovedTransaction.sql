INSERT INTO dbo.ImportedTransaction
(
    AccountId, ImportBatchId, TransactionDate, PostingDate, TransactionTime, Amount, [Type], [Description],
    ExternalTransactionId, FundingSource, SourceCategory, Memo, AppliedRuleId, Category, SpendingClass, Merchant,
    IsIncome, IsTransfer, IsManual, IncomeOwner, Notes, RawRowHash, UpdatedAtUtc
)
OUTPUT INSERTED.ImportedTransactionId
VALUES
(
    @AccountId, @ImportBatchId, @TransactionDate, @PostingDate, @TransactionTime, @Amount, @Type, @Description,
    @ExternalTransactionId, @FundingSource, @SourceCategory, @Memo, @AppliedRuleId, @Category, @SpendingClass, @Merchant,
    @IsIncome, @IsTransfer, 0, @IncomeOwner, @Notes, @RawRowHash, SYSUTCDATETIME()
);
