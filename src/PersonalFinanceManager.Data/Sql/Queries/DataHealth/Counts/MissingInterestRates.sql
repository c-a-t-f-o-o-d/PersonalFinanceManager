SELECT COUNT(*)
FROM dbo.Account
WHERE IsActive = 1
  AND (AccountType LIKE N'%Savings%' OR AccountType LIKE N'%Credit%' OR AccountType LIKE N'%Loan%' OR AccountType LIKE N'%Mortgage%')
  AND CurrentInterestRate IS NULL;
