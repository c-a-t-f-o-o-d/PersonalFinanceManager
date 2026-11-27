UPDATE dbo.ImportRow
SET [Status] = @Status,
    AcceptedTransactionId = @AcceptedTransactionId,
    ErrorMessage = CASE
        WHEN ErrorMessage IS NULL OR LTRIM(RTRIM(ErrorMessage)) = N'' THEN @Note
        ELSE ErrorMessage + N' ' + @Note
    END,
    ResolvedAtUtc = SYSUTCDATETIME()
WHERE ImportRowId = @ImportRowId
  AND [Status] IN (N'DuplicateReview', N'ExactDuplicateSkipped');
