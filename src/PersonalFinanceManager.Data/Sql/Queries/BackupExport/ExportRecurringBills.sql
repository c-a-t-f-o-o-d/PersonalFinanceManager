SELECT RecurringBillId, AccountId, BillName, Payee, Category, SpendingClass,
       RequirementType, TypicalAmount, DueDay, NextDueDate, Frequency,
       IsRequired, IsActive, SourceDescription, FirstSeenDate, LastSeenDate,
       TransactionCount, Notes, CreatedAtUtc, UpdatedAtUtc
FROM dbo.RecurringBill
ORDER BY IsActive DESC, IsRequired DESC, NextDueDate, BillName;
