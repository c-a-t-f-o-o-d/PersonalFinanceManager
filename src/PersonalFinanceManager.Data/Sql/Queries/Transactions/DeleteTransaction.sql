DECLARE @AffectedBatches TABLE (ImportBatchId bigint PRIMARY KEY);
INSERT INTO @AffectedBatches(ImportBatchId)
SELECT ImportBatchId FROM dbo.ImportRow WHERE AcceptedTransactionId = @ImportedTransactionId
UNION SELECT ImportBatchId FROM dbo.ImportedTransaction WHERE ImportedTransactionId = @ImportedTransactionId AND ImportBatchId IS NOT NULL;
UPDATE dbo.ImportRow
SET DuplicateMatchTransactionId = NULL
WHERE DuplicateMatchTransactionId = @ImportedTransactionId;
UPDATE dbo.ImportRow
SET AcceptedTransactionId = NULL, [Status] = N'Deleted', ResolvedAtUtc = SYSUTCDATETIME()
WHERE AcceptedTransactionId = @ImportedTransactionId;
UPDATE dbo.CrossAccountDuplicateReview
SET CandidateTransactionId = NULL, [Status] = N'CandidateDeleted', ResolvedAtUtc = SYSUTCDATETIME()
WHERE CandidateTransactionId = @ImportedTransactionId;
UPDATE dbo.CrossAccountDuplicateReview
SET ExistingTransactionId = NULL, [Status] = N'ExistingDeleted', ResolvedAtUtc = SYSUTCDATETIME()
WHERE ExistingTransactionId = @ImportedTransactionId;
DELETE FROM dbo.TransactionLinkRejection
WHERE SourceTransactionId = @ImportedTransactionId OR LinkedTransactionId = @ImportedTransactionId;
DELETE FROM dbo.TransactionLink
WHERE SourceTransactionId = @ImportedTransactionId OR LinkedTransactionId = @ImportedTransactionId;
DELETE FROM dbo.ImportedTransaction
WHERE ImportedTransactionId = @ImportedTransactionId;

UPDATE batch
SET ImportedRowCount = (SELECT COUNT(*) FROM dbo.ImportedTransaction AS t WHERE t.ImportBatchId = batch.ImportBatchId)
FROM dbo.ImportBatch AS batch
JOIN @AffectedBatches AS affected ON affected.ImportBatchId = batch.ImportBatchId;
