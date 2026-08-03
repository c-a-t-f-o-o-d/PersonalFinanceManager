SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.AppLookup', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppLookup
    (
        AppLookupId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppLookup PRIMARY KEY,
        LookupGroup nvarchar(100) NOT NULL,
        LookupCode nvarchar(100) NOT NULL,
        DisplayName nvarchar(200) NOT NULL,
        SortOrder int NOT NULL CONSTRAINT DF_AppLookup_SortOrder DEFAULT 100,
        IsActive bit NOT NULL CONSTRAINT DF_AppLookup_IsActive DEFAULT 1,
        CONSTRAINT UQ_AppLookup_Group_Code UNIQUE (LookupGroup, LookupCode)
    );
END;

MERGE dbo.AppLookup AS target
USING
(
    VALUES
    (N'AccountType', N'CHECKING', N'Checking', 10),
    (N'AccountType', N'SAVINGS', N'Savings', 20),
    (N'AccountType', N'CREDIT_CARD', N'Credit Card', 30),
    (N'AccountType', N'MORTGAGE', N'Mortgage', 40),
    (N'AccountType', N'VEHICLE_LOAN', N'Vehicle Loan', 50),
    (N'AccountType', N'PERSONAL_LOAN', N'Personal Loan', 60),
    (N'AccountType', N'INVESTMENT', N'Investment', 70),
    (N'AccountType', N'401K', N'401k', 80),
    (N'AccountType', N'IRA', N'IRA', 90),
    (N'AccountType', N'PAYPAL_SMART_CONNECT', N'PayPal Smart Connect', 100),
    (N'AccountType', N'OTHER', N'Other', 900),
    (N'ImportProfile', N'ALLY_CHECKING_CSV', N'Ally Checking CSV', 10)
) AS source (LookupGroup, LookupCode, DisplayName, SortOrder)
ON target.LookupGroup = source.LookupGroup AND target.LookupCode = source.LookupCode
WHEN MATCHED THEN
    UPDATE SET DisplayName = source.DisplayName, SortOrder = source.SortOrder, IsActive = 1
WHEN NOT MATCHED THEN
    INSERT (LookupGroup, LookupCode, DisplayName, SortOrder)
    VALUES (source.LookupGroup, source.LookupCode, source.DisplayName, source.SortOrder);
