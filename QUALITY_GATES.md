# Quality Gates

PersonalFinanceManager includes a few lightweight repository and release checks intended to make the project safer to publish and easier to maintain.

## Repository privacy audit

Run:

```powershell
./scripts/audit-public-repo.ps1
```

This checks for private files, local settings, legacy naming, obvious connection-string mistakes, database artifacts, and optional private search terms.

## Source consistency check

Run:

```powershell
./scripts/check-source-consistency.ps1
```

This checks core project boundaries and summarizes the current source layout. It verifies that:

- `PersonalFinanceManager.Core` does not reference UI, data, SQL Server, or WinForms implementation details.
- `PersonalFinanceManager.Application` stays free of App/Data implementation references.
- `PersonalFinanceManager.Data` does not reference WinForms or the App project.
- WinForms views do not reference repositories directly.
- removed or prohibited patterns such as `AddWithValue` and the old WinForms charting namespace are not present in source.

The script also prints the count of embedded SQL resources and runtime migrations so the SQL-resource refactor remains visible during review.

## Public baseline verification

Run:

```powershell
./scripts/verify-public-baseline.ps1
```

This is the preferred pre-release command. It runs the repository audit, source consistency check, build/test pass, release packaging, and release package audit.

## Optional private term file

You can keep a local untracked file named `private-audit-terms.txt` and pass it to verification:

```powershell
./scripts/verify-public-baseline.ps1 -ExtraTermsFile .\private-audit-terms.txt
```

Do not commit the private terms file.
