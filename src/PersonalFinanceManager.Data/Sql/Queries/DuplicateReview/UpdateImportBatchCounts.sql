UPDATE dbo.ImportBatch
SET ImportedRowCount =
    (
        SELECT COUNT(*)
        FROM dbo.ImportRow
        WHERE ImportBatchId = @ImportBatchId
          AND [Status] IN (N'Imported', N'DuplicateAccepted')
    ),
    DuplicateRowCount =
    (
        SELECT COUNT(*)
        FROM dbo.ImportRow
        WHERE ImportBatchId = @ImportBatchId
          AND [Status] IN (N'ExactDuplicateSkipped', N'DuplicateReview', N'DuplicateIgnored')
    ),
    ErrorRowCount =
    (
        SELECT COUNT(*)
        FROM dbo.ImportRow
        WHERE ImportBatchId = @ImportBatchId
          AND [Status] = N'Error'
    )
WHERE ImportBatchId = @ImportBatchId;
