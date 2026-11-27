SELECT t.ImportedTransactionId, t.AccountId, a.AccountNickname, a.AccountType, a.AccountOwner,
       t.TransactionDate, t.Amount, t.[Type], t.[Description], t.Category, t.SpendingClass,
       t.Merchant, t.IncomeOwner, t.IsIncome, t.IsTransfer, t.ClassificationIsManual, t.LegacyLinkTreatment
FROM dbo.ImportedTransaction AS t
INNER JOIN dbo.Account AS a ON a.AccountId = t.AccountId;

SELECT TransactionLinkId, SourceTransactionId, LinkedTransactionId, LinkType, AllocationAmount, ConfidenceScore, Notes
FROM dbo.TransactionLink;

SELECT RecurringBillId, AccountId, BillName, Payee, Category, SpendingClass, RequirementType,
       TypicalAmount, Frequency, IsActive, IsRequired, SourceDescription
FROM dbo.RecurringBill;
