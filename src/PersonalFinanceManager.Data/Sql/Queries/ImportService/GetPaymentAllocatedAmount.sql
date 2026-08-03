SELECT ISNULL(SUM(AllocationAmount), 0)
FROM dbo.TransactionLink
WHERE SourceTransactionId = @PaymentId
  AND LinkType = N'CreditCardPaymentAllocation';
