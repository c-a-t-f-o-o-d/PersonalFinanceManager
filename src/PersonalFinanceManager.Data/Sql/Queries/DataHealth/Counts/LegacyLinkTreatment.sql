SELECT COUNT(*) FROM dbo.ImportedTransaction AS t WHERE t.LegacyLinkTreatment = 1
  AND NOT EXISTS (
      SELECT 1 FROM dbo.TransactionLink AS link
      WHERE (link.SourceTransactionId = t.ImportedTransactionId OR link.LinkedTransactionId = t.ImportedTransactionId)
        AND link.LinkType IN (N'PayPalFunding', N'PurchaseMirror', N'PurchaseFundingAllocation', N'CreditCardPaymentFunding', N'DebtPayment', N'BankTransfer', N'AccountTransfer')
  );
