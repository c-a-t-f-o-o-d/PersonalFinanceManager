SELECT CAST(t.ImportedTransactionId AS nvarchar(30)) AS GroupKey,
    CAST(N'Transaction' AS nvarchar(50)) AS Source, t.ImportedTransactionId AS TransactionId,
    t.AccountId, a.AccountNickname AS Account, a.AccountType, a.AccountOwner,
    t.TransactionDate AS [Date], t.Amount, t.[Type], t.[Description], t.Category,
    t.SpendingClass AS CategoryType, t.IncomeOwner, t.Memo,
    CAST(N'The source describes a money movement without enough information to determine income, refund, expense, or transfer treatment. Confirm the type and financial flags in Transactions. Unclassified credits remain in cash activity but do not reduce expenses or increase income.' AS nvarchar(1000)) AS Details
FROM dbo.ImportedTransaction AS t
INNER JOIN dbo.Account AS a ON a.AccountId = t.AccountId
WHERE t.[Type] IN (N'Unclassified Credit', N'Unclassified Debit')
  AND t.IsIncome = 0 AND t.IsTransfer = 0
  AND ISNULL(t.Category, N'') <> N'Ignore' AND ISNULL(t.SpendingClass, N'') <> N'Ignore'
  AND NOT EXISTS (
      SELECT 1 FROM dbo.TransactionLink AS link
      WHERE ((link.SourceTransactionId = t.ImportedTransactionId OR link.LinkedTransactionId = t.ImportedTransactionId)
             AND link.LinkType IN (N'BankTransfer', N'AccountTransfer', N'CreditCardPaymentFunding', N'DebtPayment'))
         OR (link.LinkedTransactionId = t.ImportedTransactionId AND link.LinkType IN (N'PurchaseMirror', N'PayPalFunding'))
         OR (link.SourceTransactionId = t.ImportedTransactionId AND link.LinkType = N'CreditCardPaymentAllocation')
  )
  AND NOT (t.Amount > 0 AND t.Amount = ISNULL((
      SELECT SUM(link.AllocationAmount) FROM dbo.TransactionLink AS link
      WHERE link.SourceTransactionId = t.ImportedTransactionId AND link.LinkType = N'PurchaseRefund'
  ), 0))
ORDER BY t.TransactionDate DESC, t.ImportedTransactionId DESC;
