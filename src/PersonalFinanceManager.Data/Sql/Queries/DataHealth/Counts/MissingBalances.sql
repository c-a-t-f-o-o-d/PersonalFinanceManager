SELECT COUNT(*)
FROM dbo.Account
WHERE IsActive = 1
  AND
  (
      ((AccountType LIKE N'%Checking%' OR AccountType LIKE N'%Savings%' OR AccountType LIKE N'%Cash%') AND CurrentBalance IS NULL)
      OR
      ((AccountType LIKE N'%Credit%' OR AccountType LIKE N'%Loan%' OR AccountType LIKE N'%Mortgage%') AND CurrentBalance IS NULL AND PrincipalBalance IS NULL)
      OR
      ((AccountType NOT LIKE N'%Checking%' AND AccountType NOT LIKE N'%Savings%' AND AccountType NOT LIKE N'%Cash%' AND AccountType NOT LIKE N'%Credit%' AND AccountType NOT LIKE N'%Loan%' AND AccountType NOT LIKE N'%Mortgage%') AND CurrentBalance IS NULL AND PrincipalBalance IS NULL)
  );
