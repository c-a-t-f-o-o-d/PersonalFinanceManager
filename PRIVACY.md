# Privacy and Data Model

PersonalFinanceManager is designed as a local-first personal finance application. It does not send financial data to a hosted PersonalFinanceManager service and does not include telemetry.

## Where data lives

Financial data is stored in the SQL Server database configured by the user. For evaluation, this is usually LocalDB. Advanced users can configure a network SQL Server instance.

Local application settings are stored under:

```text
%LocalAppData%\PersonalFinanceManager\
```

Connection strings saved by the app are protected with Windows DPAPI for the current Windows user.

## What should not be committed

Do not commit:

- real CSV exports,
- PDF statements,
- Excel workbooks,
- database backups,
- MDF or LDF files,
- local settings,
- logs,
- screenshots with real balances or transactions,
- account numbers,
- routing numbers,
- real SQL connection strings,
- private merchant/payee rule lists.

Use demo data for public screenshots and examples.

## Account-number protection

PersonalFinanceManager supports storing account numbers as DPAPI-protected values. DPAPI protection is tied to the Windows user profile. If you restore the same database on a different machine or under a different Windows user, previously protected account numbers may not decrypt.

For multi-machine shared databases, a future app-level encryption option may be more appropriate.

## Public repository audit

Run this before publishing or sharing changes:

```powershell
./scripts/audit-public-repo.ps1
```

For a stricter local check, create an untracked file named `private-audit-terms.txt` and add personal strings to search for, one per line. Then run:

```powershell
./scripts/audit-public-repo.ps1 -ExtraTermsFile .\private-audit-terms.txt
```
