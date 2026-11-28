SELECT b.*, CAST(NULL AS nvarchar(200)) AS AccountNickname
FROM dbo.RecurringBill AS b WITH (UPDLOCK, HOLDLOCK)
WHERE b.IsActive = 1 AND b.RecurringBillId <> @RecurringBillId;
