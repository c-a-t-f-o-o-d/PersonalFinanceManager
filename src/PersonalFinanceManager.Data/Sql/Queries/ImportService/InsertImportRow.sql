INSERT INTO dbo.ImportRow
(
    ImportBatchId, RowNumber, [Status], ErrorMessage, TransactionDate, PostingDate, TransactionTime,
    Amount, [Type], [Description], ExternalTransactionId, FundingSource, SourceCategory, Memo,
    AppliedRuleId, Category, SpendingClass, Merchant, IsIncome, IsTransfer, IncomeOwner,
    AcceptedTransactionId, DuplicateMatchTransactionId, DuplicateReason, DuplicateConfidence
)
VALUES
(
    @ImportBatchId, @RowNumber, @Status, @ErrorMessage, @TransactionDate, @PostingDate, @TransactionTime,
    @Amount, @Type, @Description, @ExternalTransactionId, @FundingSource, @SourceCategory, @Memo,
    @AppliedRuleId, @Category, @SpendingClass, @Merchant, @IsIncome, @IsTransfer, @IncomeOwner,
    @AcceptedTransactionId, @DuplicateMatchTransactionId, @DuplicateReason, @DuplicateConfidence
);
