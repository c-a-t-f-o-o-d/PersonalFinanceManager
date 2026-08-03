SELECT COUNT(*)
FROM
(
    SELECT AccountId, TransactionDate, Amount,
           UPPER(LTRIM(RTRIM([Type]))) AS NormalizedType,
           UPPER(LTRIM(RTRIM([Description]))) AS NormalizedDescription,
           ISNULL(UPPER(LTRIM(RTRIM(ExternalTransactionId))), N'') AS NormalizedExternalId,
           ISNULL(NULLIF(UPPER(LTRIM(RTRIM(Memo))), N''), N'') AS NormalizedMemo
    FROM dbo.ImportedTransaction
    GROUP BY AccountId, TransactionDate, Amount,
             UPPER(LTRIM(RTRIM([Type]))),
             UPPER(LTRIM(RTRIM([Description]))),
             ISNULL(UPPER(LTRIM(RTRIM(ExternalTransactionId))), N''),
             ISNULL(NULLIF(UPPER(LTRIM(RTRIM(Memo))), N''), N'')
    HAVING COUNT_BIG(*) > 1
) AS DuplicateGroups;
