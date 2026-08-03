SELECT COUNT(*)
FROM
(
    SELECT AccountId, TransactionDate, Amount, [Type], [Description], ISNULL(NULLIF(UPPER(LTRIM(RTRIM(Memo))), N''), N'') AS NormalizedMemo
    FROM dbo.ImportedTransaction
    GROUP BY AccountId, TransactionDate, Amount, [Type], [Description], ISNULL(NULLIF(UPPER(LTRIM(RTRIM(Memo))), N''), N'')
    HAVING COUNT_BIG(*) > 1
) AS duplicateGroups;
