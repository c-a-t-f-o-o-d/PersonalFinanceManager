SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
GO

IF COL_LENGTH(N'dbo.Account', N'BalanceAsOfDate') IS NULL
    ALTER TABLE dbo.Account ADD BalanceAsOfDate date NULL;

IF COL_LENGTH(N'dbo.ImportedTransaction', N'ClassificationIsManual') IS NULL
    ALTER TABLE dbo.ImportedTransaction ADD ClassificationIsManual bit NOT NULL
        CONSTRAINT DF_ImportedTransaction_ClassificationIsManual DEFAULT 0;

IF COL_LENGTH(N'dbo.ImportedTransaction', N'LegacyLinkTreatment') IS NULL
    ALTER TABLE dbo.ImportedTransaction ADD LegacyLinkTreatment bit NOT NULL
        CONSTRAINT DF_ImportedTransaction_LegacyLinkTreatment DEFAULT 0;

IF COL_LENGTH(N'dbo.ImportRow', N'AcceptedTransactionId') IS NULL
    ALTER TABLE dbo.ImportRow ADD AcceptedTransactionId bigint NULL;
GO

UPDATE dbo.ImportedTransaction
SET ClassificationIsManual = 1
WHERE IsManual = 1 AND ClassificationIsManual = 0;

UPDATE dbo.ImportedTransaction
SET LegacyLinkTreatment = 1
WHERE Category = N'Transfer'
  AND SpendingClass IN (N'PayPal Funding', N'Credit Card Payment', N'Bank Transfer')
  AND ClassificationIsManual = 0
  AND LegacyLinkTreatment = 0
  AND
  (
      LOWER(Notes) LIKE N'%linked as paypal funding transfer.%'
      OR LOWER(Notes) LIKE N'%linked as credit card payment transfer.%'
      OR LOWER(Notes) LIKE N'%linked as credit card payment funding transfer.%'
      OR LOWER(Notes) LIKE N'%marked credit card payment row as transfer.%'
      OR LOWER(Notes) LIKE N'%auto-linked as bank transfer.%'
      OR LOWER(Notes) LIKE N'%auto-normalized as likely paypal funding transfer.%'
      OR LOWER(Notes) LIKE N'%auto-normalized as likely credit card payment funding transfer.%'
      OR LOWER(Notes) LIKE N'%auto-normalized by report/data-flow integrity pass as likely paypal funding transfer.%'
      OR LOWER(Notes) LIKE N'%auto-normalized by report/data-flow integrity pass as likely credit card payment funding transfer.%'
  );

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ImportRow_AcceptedTransaction')
    ALTER TABLE dbo.ImportRow ADD CONSTRAINT FK_ImportRow_AcceptedTransaction
        FOREIGN KEY (AcceptedTransactionId) REFERENCES dbo.ImportedTransaction(ImportedTransactionId) ON DELETE SET NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ImportRow') AND name = N'UX_ImportRow_AcceptedTransaction')
    CREATE UNIQUE INDEX UX_ImportRow_AcceptedTransaction ON dbo.ImportRow(AcceptedTransactionId)
        WHERE AcceptedTransactionId IS NOT NULL;

IF OBJECT_ID(N'dbo.TransactionLinkRejection', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TransactionLinkRejection
    (
        SourceTransactionId bigint NOT NULL,
        LinkedTransactionId bigint NOT NULL,
        LinkType nvarchar(100) NOT NULL,
        CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_TransactionLinkRejection_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_TransactionLinkRejection PRIMARY KEY (SourceTransactionId, LinkedTransactionId, LinkType),
        CONSTRAINT FK_TransactionLinkRejection_Source FOREIGN KEY (SourceTransactionId) REFERENCES dbo.ImportedTransaction(ImportedTransactionId),
        CONSTRAINT FK_TransactionLinkRejection_Linked FOREIGN KEY (LinkedTransactionId) REFERENCES dbo.ImportedTransaction(ImportedTransactionId)
    );
END;

ALTER TABLE dbo.TransactionLink ALTER COLUMN Notes nvarchar(2000) NULL;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.22.0')
BEGIN
    DECLARE @ReviewLinks TABLE
    (
        TransactionLinkId bigint PRIMARY KEY,
        SourceTransactionId bigint NOT NULL,
        LinkedTransactionId bigint NOT NULL,
        OriginalType nvarchar(100) NOT NULL
    );

    INSERT INTO @ReviewLinks (TransactionLinkId, SourceTransactionId, LinkedTransactionId, OriginalType)
    SELECT link.TransactionLinkId, link.SourceTransactionId, link.LinkedTransactionId, link.LinkType
    FROM dbo.TransactionLink AS link
    JOIN dbo.ImportedTransaction AS source ON source.ImportedTransactionId = link.SourceTransactionId
    JOIN dbo.ImportedTransaction AS linked ON linked.ImportedTransactionId = link.LinkedTransactionId
    WHERE link.LinkType <> N'ManualReview'
      AND
      (
          link.ConfidenceScore < 100
          OR LOWER(link.Notes) LIKE N'matched paypal %'
          OR LOWER(link.Notes) LIKE N'matched credit card payment:%'
          OR LOWER(link.Notes) LIKE N'fifo allocation of credit card payment%'
          OR LOWER(link.Notes) LIKE N'auto-linked as bank transfer%'
          OR source.Amount = 0 OR linked.Amount = 0
          OR link.LinkType NOT IN (N'BankTransfer', N'AccountTransfer', N'PurchaseMirror', N'PayPalFunding',
              N'CreditCardPaymentFunding', N'DebtPayment', N'CreditCardPaymentAllocation', N'PurchaseFundingAllocation', N'PurchaseRefund')
          OR (link.LinkType IN (N'BankTransfer', N'AccountTransfer', N'CreditCardPaymentFunding', N'DebtPayment')
              AND (source.AccountId = linked.AccountId OR source.Amount <> -linked.Amount))
          OR (link.LinkType IN (N'PurchaseMirror', N'PayPalFunding')
              AND (source.AccountId = linked.AccountId OR source.Amount <> linked.Amount))
          OR (link.LinkType IN (N'CreditCardPaymentAllocation', N'PurchaseRefund')
              AND (source.AccountId <> linked.AccountId OR source.Amount <= 0 OR linked.Amount >= 0))
          OR (link.LinkType = N'PurchaseFundingAllocation'
              AND (source.AccountId = linked.AccountId OR source.Amount >= 0 OR linked.Amount >= 0))
          OR (link.LinkType IN (N'CreditCardPaymentAllocation', N'PurchaseFundingAllocation', N'PurchaseRefund')
              AND (ISNULL(link.AllocationAmount, 0) <= 0
                  OR (SELECT SUM(other.AllocationAmount) FROM dbo.TransactionLink AS other
                      WHERE other.LinkType = link.LinkType AND other.SourceTransactionId = source.ImportedTransactionId) > ABS(source.Amount)
                  OR (SELECT SUM(other.AllocationAmount) FROM dbo.TransactionLink AS other
                      WHERE other.LinkType = link.LinkType AND other.LinkedTransactionId = linked.ImportedTransactionId) > ABS(linked.Amount)))
      );

    INSERT INTO dbo.TransactionLinkRejection (SourceTransactionId, LinkedTransactionId, LinkType)
    SELECT DISTINCT review.SourceTransactionId, review.LinkedTransactionId, canonical.LinkType
    FROM @ReviewLinks AS review
    CROSS APPLY (SELECT CASE review.OriginalType
        WHEN N'AccountTransfer' THEN N'BankTransfer'
        WHEN N'PayPalFunding' THEN N'PurchaseMirror'
        WHEN N'DebtPayment' THEN N'CreditCardPaymentFunding'
        ELSE review.OriginalType END AS LinkType) AS canonical
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.TransactionLinkRejection AS rejection
        WHERE rejection.SourceTransactionId = review.SourceTransactionId
          AND rejection.LinkedTransactionId = review.LinkedTransactionId
          AND rejection.LinkType = canonical.LinkType
    );

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TransactionLink') AND name = N'UX_TransactionLink_Source_Linked_Type')
        DROP INDEX UX_TransactionLink_Source_Linked_Type ON dbo.TransactionLink;

    UPDATE link
    SET LinkType = N'ManualReview',
        Notes = N'Review required after upgrade: previous ' + review.OriginalType
            + N' link used unverified automatic matching or violates financial constraints. Confirm the statements and edit the relationship. Original notes: '
            + ISNULL(link.Notes, N''),
        UpdatedAtUtc = SYSUTCDATETIME()
    FROM dbo.TransactionLink AS link
    JOIN @ReviewLinks AS review ON review.TransactionLinkId = link.TransactionLinkId;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TransactionLink') AND name = N'UX_TransactionLink_Source_Linked_Type')
    CREATE UNIQUE INDEX UX_TransactionLink_Source_Linked_Type
        ON dbo.TransactionLink(SourceTransactionId, LinkedTransactionId, LinkType)
        WHERE LinkType <> N'ManualReview';

IF OBJECT_ID(N'dbo.AccountReconciliation', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AccountReconciliation
    (
        AccountReconciliationId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_AccountReconciliation PRIMARY KEY,
        AccountId int NOT NULL,
        FromDate date NOT NULL,
        ToDate date NOT NULL,
        OpeningBalance decimal(18,2) NOT NULL,
        ClosingBalance decimal(18,2) NOT NULL,
        ExpectedClosingBalance decimal(18,2) NOT NULL,
        NetActivity decimal(18,2) NOT NULL,
        TransactionCount int NOT NULL,
        DateFallbackCount int NOT NULL,
        UsesLiabilityBalances bit NOT NULL,
        RecordedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_AccountReconciliation_RecordedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_AccountReconciliation_Account FOREIGN KEY (AccountId) REFERENCES dbo.Account(AccountId),
        CONSTRAINT CK_AccountReconciliation_Dates CHECK (FromDate <= ToDate)
    );
    CREATE INDEX IX_AccountReconciliation_Account_Date ON dbo.AccountReconciliation(AccountId, RecordedAtUtc DESC);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.22.0')
    INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'2.22.0');

COMMIT TRANSACTION;
