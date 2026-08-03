# Security Policy

PersonalFinanceManager is a local/self-hosted personal-finance application. It can store sensitive financial metadata, so security issues should be handled carefully.

## Reporting a vulnerability

Please do not open a public issue for vulnerabilities that could expose financial data, credentials, account numbers, or local files.

Use a private reporting channel provided by the repository owner. If no private channel is listed, contact the maintainer before disclosing technical details publicly.

## Current security model

PersonalFinanceManager stores application data in SQL Server. Local configuration is stored under:

```text
%LocalAppData%\PersonalFinanceManager
```

Saved local connection strings are protected with Windows DPAPI for the current Windows user. Optional account-number fields are also protected with Windows DPAPI.

## Important limitation

DPAPI `CurrentUser` protection is tied to a Windows user profile. If the database is shared across machines, protected account-number values created on one machine may not be decryptable on another. PersonalFinanceManager should preserve those values safely, but multi-machine decryption requires a future shared-key design.

## Data that should never be committed

- Real exports or statements
- Database backups
- MDF/LDF files
- Local settings files
- Logs
- Screenshots containing personal finance data
- Private merchant rules
- Passwords or connection strings

Run the audit script before publishing:

```powershell
.\scripts\audit-public-repo.ps1
```
