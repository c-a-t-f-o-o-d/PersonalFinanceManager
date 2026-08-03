SELECT t.ImportedTransactionId,
       (-t.Amount - ISNULL(SUM(l.AllocationAmount), 0)) AS OpenAmount
FROM dbo.ImportedTransaction AS t
LEFT JOIN dbo.TransactionLink AS l ON l.LinkedTransactionId = t.ImportedTransactionId
    AND l.LinkType = N'CreditCardPaymentAllocation'
WHERE t.AccountId = @AccountId
  AND t.Amount < 0
  AND t.TransactionDate <= @PaymentDate
  AND UPPER(LTRIM(RTRIM(t.[Type]))) <> N'PAYMENT'
GROUP BY t.ImportedTransactionId, t.TransactionDate, t.Amount
HAVING (-t.Amount - ISNULL(SUM(l.AllocationAmount), 0)) > 0
ORDER BY t.TransactionDate, t.ImportedTransactionId;
