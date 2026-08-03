UPDATE dbo.ImportBatch
SET TotalRowCount = @TotalRowCount,
    ImportedRowCount = @ImportedRowCount,
    DuplicateRowCount = @DuplicateRowCount,
    ErrorRowCount = @ErrorRowCount
WHERE ImportBatchId = @ImportBatchId;
