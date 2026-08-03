SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.CrossAccountDuplicateReview', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CrossAccountDuplicateReview
    (
        CrossAccountDuplicateReviewId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_CrossAccountDuplicateReview PRIMARY KEY,
        CandidateTransactionId bigint NULL,
        ExistingTransactionId bigint NULL,
        [Status] nvarchar(30) NOT NULL CONSTRAINT DF_CrossAccountDuplicateReview_Status DEFAULT N'Pending',
        Reason nvarchar(1000) NULL,
        Confidence nvarchar(30) NULL,
        CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_CrossAccountDuplicateReview_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        ResolvedAtUtc datetime2(7) NULL,
        CONSTRAINT CK_CrossAccountDuplicateReview_Status CHECK ([Status] IN (N'Pending', N'Kept', N'CandidateDeleted', N'ExistingDeleted'))
    );
END;
GO

IF OBJECT_ID(N'dbo.CrossAccountDuplicateReview', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.CrossAccountDuplicateReview', N'CandidateTransactionId') IS NULL
    BEGIN
        ALTER TABLE dbo.CrossAccountDuplicateReview ADD CandidateTransactionId bigint NULL;
    END;

    IF COL_LENGTH(N'dbo.CrossAccountDuplicateReview', N'ExistingTransactionId') IS NULL
    BEGIN
        ALTER TABLE dbo.CrossAccountDuplicateReview ADD ExistingTransactionId bigint NULL;
    END;

    IF COL_LENGTH(N'dbo.CrossAccountDuplicateReview', N'Status') IS NULL
    BEGIN
        ALTER TABLE dbo.CrossAccountDuplicateReview ADD [Status] nvarchar(30) NOT NULL CONSTRAINT DF_CrossAccountDuplicateReview_Status2 DEFAULT N'Pending';
    END;

    IF COL_LENGTH(N'dbo.CrossAccountDuplicateReview', N'Reason') IS NULL
    BEGIN
        ALTER TABLE dbo.CrossAccountDuplicateReview ADD Reason nvarchar(1000) NULL;
    END;

    IF COL_LENGTH(N'dbo.CrossAccountDuplicateReview', N'Confidence') IS NULL
    BEGIN
        ALTER TABLE dbo.CrossAccountDuplicateReview ADD Confidence nvarchar(30) NULL;
    END;

    IF COL_LENGTH(N'dbo.CrossAccountDuplicateReview', N'CreatedAtUtc') IS NULL
    BEGIN
        ALTER TABLE dbo.CrossAccountDuplicateReview ADD CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_CrossAccountDuplicateReview_CreatedAtUtc2 DEFAULT SYSUTCDATETIME();
    END;

    IF COL_LENGTH(N'dbo.CrossAccountDuplicateReview', N'ResolvedAtUtc') IS NULL
    BEGIN
        ALTER TABLE dbo.CrossAccountDuplicateReview ADD ResolvedAtUtc datetime2(7) NULL;
    END;
END;
GO

IF OBJECT_ID(N'dbo.CrossAccountDuplicateReview', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.ImportedTransaction', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.CrossAccountDuplicateReview', N'CandidateTransactionId') IS NOT NULL
   AND COL_LENGTH(N'dbo.CrossAccountDuplicateReview', N'ExistingTransactionId') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.CrossAccountDuplicateReview') AND name = N'UX_CrossAccountDuplicateReview_Pair')
    BEGIN
        EXEC(N'CREATE UNIQUE INDEX UX_CrossAccountDuplicateReview_Pair
            ON dbo.CrossAccountDuplicateReview(CandidateTransactionId, ExistingTransactionId)
            WHERE CandidateTransactionId IS NOT NULL AND ExistingTransactionId IS NOT NULL;');
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.CrossAccountDuplicateReview') AND name = N'IX_CrossAccountDuplicateReview_Status')
    BEGIN
        EXEC(N'CREATE INDEX IX_CrossAccountDuplicateReview_Status
            ON dbo.CrossAccountDuplicateReview([Status], CreatedAtUtc DESC)
            INCLUDE (CandidateTransactionId, ExistingTransactionId, Confidence);');
    END;
END;
GO

IF OBJECT_ID(N'dbo.ImportedTransaction', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ImportedTransaction') AND name = N'IX_ImportedTransaction_CrossAccountDuplicateScan')
    BEGIN
        EXEC(N'CREATE INDEX IX_ImportedTransaction_CrossAccountDuplicateScan
            ON dbo.ImportedTransaction(TransactionDate, Amount, [Type], [Description])
            INCLUDE (AccountId, PostingDate, TransactionTime, Category, SpendingClass);');
    END;
END;
GO

IF OBJECT_ID(N'dbo.CrossAccountDuplicateReview', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.ImportedTransaction', N'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.CrossAccountDuplicateReview
    (
        CandidateTransactionId,
        ExistingTransactionId,
        [Status],
        Reason,
        Confidence
    )
    SELECT
        t1.ImportedTransactionId,
        t2.ImportedTransactionId,
        N'Pending',
        N'Same transaction date, amount, type, and description appear under different accounts. Verify whether one account import was selected incorrectly or whether both entries are legitimate.',
        N'Needs Review'
    FROM dbo.ImportedTransaction AS t1
    INNER JOIN dbo.ImportedTransaction AS t2 ON t1.ImportedTransactionId < t2.ImportedTransactionId
    WHERE t1.AccountId <> t2.AccountId
      AND t1.TransactionDate = t2.TransactionDate
      AND t1.Amount = t2.Amount
      AND UPPER(LTRIM(RTRIM(t1.[Type]))) = UPPER(LTRIM(RTRIM(t2.[Type])))
      AND UPPER(LTRIM(RTRIM(t1.[Description]))) = UPPER(LTRIM(RTRIM(t2.[Description])))
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.CrossAccountDuplicateReview AS existing
          WHERE
              (existing.CandidateTransactionId = t1.ImportedTransactionId AND existing.ExistingTransactionId = t2.ImportedTransactionId)
              OR
              (existing.CandidateTransactionId = t2.ImportedTransactionId AND existing.ExistingTransactionId = t1.ImportedTransactionId)
      )
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.TransactionLink AS link
          WHERE
              (link.SourceTransactionId = t1.ImportedTransactionId AND link.LinkedTransactionId = t2.ImportedTransactionId)
              OR
              (link.SourceTransactionId = t2.ImportedTransactionId AND link.LinkedTransactionId = t1.ImportedTransactionId)
      );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.18.0')
BEGIN
    INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'2.18.0');
END;
