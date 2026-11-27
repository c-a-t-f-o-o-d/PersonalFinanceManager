SELECT COUNT(*)
FROM dbo.ImportedTransaction AS t
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
  ), 0));
