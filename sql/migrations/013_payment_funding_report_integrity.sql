SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF COL_LENGTH(N'dbo.ImportedTransaction', N'FundingSource') IS NOT NULL
BEGIN
    EXEC(N'
        UPDATE bank
        SET IsTransfer = 1,
            Category = N''Transfer'',
            SpendingClass = N''PayPal Funding'',
            Notes = CASE
                WHEN bank.Notes IS NULL OR LTRIM(RTRIM(bank.Notes)) = N'''' THEN N''Auto-normalized by report/data-flow integrity pass as likely PayPal funding transfer.''
                WHEN bank.Notes NOT LIKE N''%PayPal funding%'' THEN bank.Notes + N'' Auto-normalized by report/data-flow integrity pass as likely PayPal funding transfer.''
                ELSE bank.Notes
            END,
            UpdatedAtUtc = SYSUTCDATETIME()
        FROM dbo.ImportedTransaction AS bank
        INNER JOIN dbo.Account AS bankAccount ON bankAccount.AccountId = bank.AccountId
        WHERE bank.Amount < 0
          AND UPPER(bankAccount.AccountType) NOT LIKE N''%PAYPAL%''
          AND
          (
              UPPER(ISNULL(bank.Category, N'''')) = N''PAYPAL''
              OR UPPER(ISNULL(bank.SpendingClass, N'''')) IN (N''PAYPAL'', N''PAYPAL FUNDING'')
              OR UPPER(bank.[Description]) LIKE N''%PAYPAL%''
          )
          AND EXISTS
          (
              SELECT 1
              FROM dbo.ImportedTransaction AS payPal
              INNER JOIN dbo.Account AS payPalAccount ON payPalAccount.AccountId = payPal.AccountId
              WHERE payPal.ImportedTransactionId <> bank.ImportedTransactionId
                AND payPal.Amount = bank.Amount
                AND payPal.TransactionDate BETWEEN DATEADD(day, -5, bank.TransactionDate) AND DATEADD(day, 7, bank.TransactionDate)
                AND
                (
                    UPPER(payPalAccount.AccountType) LIKE N''%PAYPAL%''
                    OR payPal.FundingSource IS NOT NULL
                    OR UPPER(ISNULL(payPal.[Type], N'''')) LIKE N''%PAYPAL%''
                )
          )
          AND
          (
              bank.IsTransfer = 0
              OR ISNULL(bank.Category, N'''') <> N''Transfer''
              OR ISNULL(bank.SpendingClass, N'''') <> N''PayPal Funding''
          );
    ');
END;

EXEC(N'
    UPDATE bank
    SET IsTransfer = 1,
        Category = N''Transfer'',
        SpendingClass = N''Credit Card Payment'',
        Notes = CASE
            WHEN bank.Notes IS NULL OR LTRIM(RTRIM(bank.Notes)) = N'''' THEN N''Auto-normalized by report/data-flow integrity pass as likely credit card payment funding transfer.''
            WHEN bank.Notes NOT LIKE N''%credit card payment%'' THEN bank.Notes + N'' Auto-normalized by report/data-flow integrity pass as likely credit card payment funding transfer.''
            ELSE bank.Notes
        END,
        UpdatedAtUtc = SYSUTCDATETIME()
    FROM dbo.ImportedTransaction AS bank
    INNER JOIN dbo.Account AS bankAccount ON bankAccount.AccountId = bank.AccountId
    WHERE bank.Amount < 0
      AND UPPER(bankAccount.AccountType) NOT LIKE N''%CREDIT%''
      AND
      (
          UPPER(ISNULL(bank.Category, N'''')) IN (N''DEBT (CREDIT CARD)'', N''CREDIT CARD PAYMENT'')
          OR UPPER(ISNULL(bank.SpendingClass, N'''')) IN (N''PAYMENT'', N''CREDIT CARD PAYMENT'')
          OR UPPER(bank.[Description]) LIKE N''%APPLECARD%''
          OR UPPER(bank.[Description]) LIKE N''%GSBANK%''
          OR UPPER(bank.[Description]) LIKE N''%CHASE CREDIT%''
          OR UPPER(bank.[Description]) LIKE N''%CREDIT CRD%''
          OR UPPER(bank.[Description]) LIKE N''%CARD PAYMENT%''
      )
      AND EXISTS
      (
          SELECT 1
          FROM dbo.ImportedTransaction AS cardPayment
          INNER JOIN dbo.Account AS cardAccount ON cardAccount.AccountId = cardPayment.AccountId
          WHERE cardPayment.ImportedTransactionId <> bank.ImportedTransactionId
            AND UPPER(cardAccount.AccountType) LIKE N''%CREDIT%''
            AND cardPayment.Amount = -bank.Amount
            AND cardPayment.Amount > 0
            AND UPPER(LTRIM(RTRIM(cardPayment.[Type]))) = N''PAYMENT''
            AND cardPayment.TransactionDate BETWEEN DATEADD(day, -7, bank.TransactionDate) AND DATEADD(day, 10, bank.TransactionDate)
      )
      AND
      (
          bank.IsTransfer = 0
          OR ISNULL(bank.Category, N'''') <> N''Transfer''
          OR ISNULL(bank.SpendingClass, N'''') <> N''Credit Card Payment''
      );
');

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.10.0')
BEGIN
    INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'2.10.0');
END;
