SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF EXISTS (
    SELECT 1 FROM dbo.ImportedTransaction AS t WITH (UPDLOCK, HOLDLOCK)
    WHERE t.ImportedTransactionId = @ImportedTransactionId
      AND (t.AccountId <> @AccountId OR t.Amount <> @Amount OR t.TransactionDate <> @TransactionDate OR t.[Type] <> @Type)
      AND EXISTS (SELECT 1 FROM dbo.TransactionLink AS link
          WHERE link.SourceTransactionId = t.ImportedTransactionId OR link.LinkedTransactionId = t.ImportedTransactionId)
)
BEGIN
    ROLLBACK TRANSACTION;
    THROW 50001, 'Remove the transaction links before changing an account, amount, date, or transaction type. Reconcile them again after the correction.', 1;
END;
UPDATE dbo.ImportedTransaction
SET AccountId = @AccountId,
    TransactionDate = @TransactionDate,
    TransactionTime = @TransactionTime,
    Amount = @Amount,
    [Type] = @Type,
    [Description] = @Description,
    AppliedRuleId = @AppliedRuleId,
    Category = @Category,
    SpendingClass = @SpendingClass,
    Merchant = @Merchant,
    IsIncome = @IsIncome,
    IsTransfer = @IsTransfer,
    IsManual = @IsManual,
    ClassificationIsManual = @ClassificationIsManual,
    LegacyLinkTreatment = CASE WHEN ISNULL(Category,N'') <> ISNULL(@Category,N'') OR ISNULL(SpendingClass,N'') <> ISNULL(@SpendingClass,N'')
        OR IsTransfer <> @IsTransfer OR IsIncome <> @IsIncome THEN 0 ELSE LegacyLinkTreatment END,
    IncomeOwner = @IncomeOwner,
    Notes = @Notes,
    RawRowHash = CASE WHEN ImportBatchId IS NULL THEN @RawRowHash ELSE RawRowHash END,
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE ImportedTransactionId = @ImportedTransactionId;

COMMIT TRANSACTION;
