INSERT INTO dbo.TransactionLinkRejection(SourceTransactionId, LinkedTransactionId, LinkType)
SELECT link.SourceTransactionId, link.LinkedTransactionId,
       CASE link.LinkType WHEN N'AccountTransfer' THEN N'BankTransfer' WHEN N'PayPalFunding' THEN N'PurchaseMirror'
            WHEN N'DebtPayment' THEN N'CreditCardPaymentFunding' ELSE link.LinkType END
FROM dbo.TransactionLink AS link
WHERE link.TransactionLinkId = @TransactionLinkId
  AND NOT EXISTS (SELECT 1 FROM dbo.TransactionLinkRejection AS rejection
      WHERE rejection.SourceTransactionId = link.SourceTransactionId AND rejection.LinkedTransactionId = link.LinkedTransactionId
        AND rejection.LinkType = CASE link.LinkType WHEN N'AccountTransfer' THEN N'BankTransfer' WHEN N'PayPalFunding' THEN N'PurchaseMirror'
            WHEN N'DebtPayment' THEN N'CreditCardPaymentFunding' ELSE link.LinkType END);
