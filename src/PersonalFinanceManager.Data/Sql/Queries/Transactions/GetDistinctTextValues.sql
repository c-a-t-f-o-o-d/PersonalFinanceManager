SELECT DISTINCT Value
FROM
(
    SELECT LTRIM(RTRIM(/*COLUMN_NAME*/)) AS Value
    FROM dbo.ImportedTransaction
    WHERE /*COLUMN_NAME*/ IS NOT NULL AND LTRIM(RTRIM(/*COLUMN_NAME*/)) <> ''
    UNION
    SELECT LTRIM(RTRIM(/*COLUMN_NAME*/)) AS Value
    FROM dbo.FinancialRule
    WHERE /*COLUMN_NAME*/ IS NOT NULL AND LTRIM(RTRIM(/*COLUMN_NAME*/)) <> ''
    UNION
    SELECT LTRIM(RTRIM(DisplayName)) AS Value
    FROM dbo.AppLookup
    WHERE LookupGroup = @LookupGroup AND IsActive = 1 AND LTRIM(RTRIM(DisplayName)) <> ''
) AS valueset
ORDER BY Value;
