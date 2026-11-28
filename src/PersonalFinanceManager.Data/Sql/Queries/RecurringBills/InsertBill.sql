INSERT INTO dbo.RecurringBill
(
    AccountId, BillName, Payee, Category, SpendingClass, RequirementType, TypicalAmount,
    DueDay, NextDueDate, Frequency, IsRequired, IsActive, SourceDescription,
    FirstSeenDate, LastSeenDate, TransactionCount, Notes, PayeeAliasesJson, SourceDescriptionAliasesJson
)
OUTPUT INSERTED.RecurringBillId, INSERTED.[RowVersion]
VALUES
(
    @AccountId, @BillName, @Payee, @Category, @SpendingClass, @RequirementType, @TypicalAmount,
    @DueDay, @NextDueDate, @Frequency, @IsRequired, @IsActive, @SourceDescription,
    @FirstSeenDate, @LastSeenDate, @TransactionCount, @Notes, @PayeeAliasesJson, @SourceDescriptionAliasesJson
);
