SELECT
    CAST(t.ImportedTransactionId AS nvarchar(30)) AS GroupKey,
    CAST(N'Transaction' AS nvarchar(50)) AS Source,
    t.ImportedTransactionId AS TransactionId,
    t.AccountId,
    a.AccountNickname AS Account,
    a.AccountType,
    a.AccountOwner,
    t.TransactionDate AS [Date],
    t.Amount,
    t.[Type],
    t.[Description],
    t.Category,
    t.SpendingClass AS CategoryType,
    t.IncomeOwner,
    t.Memo,
    CAST(N'An older funding/transfer action overwrote this classification, but no financial link remains. Check the source statement, edit its category and transfer flag, or reapply rules. The original category was not retained by the older version.' AS nvarchar(1000)) AS Details
FROM dbo.ImportedTransaction AS t
INNER JOIN dbo.Account AS a ON a.AccountId = t.AccountId
WHERE t.LegacyLinkTreatment = 1
  AND NOT EXISTS (
      SELECT 1 FROM dbo.TransactionLink AS link
      WHERE (link.SourceTransactionId = t.ImportedTransactionId OR link.LinkedTransactionId = t.ImportedTransactionId)
        AND link.LinkType IN (N'PayPalFunding', N'PurchaseMirror', N'PurchaseFundingAllocation', N'CreditCardPaymentFunding', N'DebtPayment', N'BankTransfer', N'AccountTransfer')
  )
ORDER BY t.TransactionDate DESC, t.ImportedTransactionId DESC;
