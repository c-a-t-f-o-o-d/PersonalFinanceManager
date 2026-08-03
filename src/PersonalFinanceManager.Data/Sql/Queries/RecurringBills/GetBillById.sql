SELECT b.*, a.AccountNickname
FROM dbo.RecurringBill AS b
LEFT JOIN dbo.Account AS a ON a.AccountId = b.AccountId
WHERE b.RecurringBillId = @RecurringBillId;
