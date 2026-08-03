SELECT DISTINCT LTRIM(RTRIM(LinkType)) AS LinkType
FROM dbo.TransactionLink
WHERE LinkType IS NOT NULL AND LTRIM(RTRIM(LinkType)) <> N''
ORDER BY LinkType;
