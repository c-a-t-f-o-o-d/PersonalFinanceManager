UPDATE dbo.ImportedTransaction
SET AppliedRuleId = @AppliedRuleId,
    Category = @Category,
    SpendingClass = @SpendingClass,
    Merchant = @Merchant,
    IsIncome = @IsIncome,
    IsTransfer = @IsTransfer,
    IncomeOwner = CASE WHEN @IncomeOwner IS NULL THEN IncomeOwner ELSE @IncomeOwner END,
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE ImportedTransactionId = @ImportedTransactionId
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.TransactionLink AS l
      WHERE (l.LinkedTransactionId = dbo.ImportedTransaction.ImportedTransactionId OR l.SourceTransactionId = dbo.ImportedTransaction.ImportedTransactionId)
        AND l.LinkType IN (N'PayPalFunding', N'CreditCardPaymentFunding')
  );
