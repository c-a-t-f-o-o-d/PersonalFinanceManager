INSERT INTO dbo.ImportedTransaction
(
    AccountId, ImportBatchId, TransactionDate, TransactionTime, Amount, [Type], [Description],
    AppliedRuleId, Category, SpendingClass, Merchant, IsIncome, IsTransfer, IsManual, ClassificationIsManual, IncomeOwner, Notes, RawRowHash, UpdatedAtUtc
)
OUTPUT INSERTED.ImportedTransactionId
VALUES
(
    @AccountId, @ImportBatchId, @TransactionDate, @TransactionTime, @Amount, @Type, @Description,
    @AppliedRuleId, @Category, @SpendingClass, @Merchant, @IsIncome, @IsTransfer, @IsManual, @ClassificationIsManual, @IncomeOwner, @Notes, @RawRowHash, SYSUTCDATETIME()
);
