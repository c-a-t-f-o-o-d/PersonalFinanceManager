SELECT b.*, a.AccountNickname
FROM dbo.RecurringBill AS b
LEFT JOIN dbo.Account AS a ON a.AccountId = b.AccountId
ORDER BY b.IsActive DESC, b.IsRequired DESC, b.NextDueDate, b.BillName;
