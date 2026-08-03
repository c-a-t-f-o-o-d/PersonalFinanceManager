SELECT DISTINCT LTRIM(RTRIM(RequirementType)) AS RequirementType
FROM dbo.RecurringBill
WHERE RequirementType IS NOT NULL AND LTRIM(RTRIM(RequirementType)) <> N''
ORDER BY RequirementType;
