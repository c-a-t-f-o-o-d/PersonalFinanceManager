INSERT INTO dbo.RecurringBill
(
    AccountId, BillName, Payee, Category, SpendingClass, RequirementType, TypicalAmount,
    DueDay, NextDueDate, Frequency, IsRequired, IsActive, SourceDescription,
    FirstSeenDate, LastSeenDate, TransactionCount, Notes
)
OUTPUT INSERTED.RecurringBillId
VALUES
(
    @AccountId, @BillName, @Payee, @Category, @SpendingClass, @RequirementType, @TypicalAmount,
    @DueDay, @NextDueDate, @Frequency, @IsRequired, @IsActive, @SourceDescription,
    @FirstSeenDate, @LastSeenDate, @TransactionCount, @Notes
);
