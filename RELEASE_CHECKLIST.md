# Release Checklist

Use this checklist before making a public GitHub commit, creating a release tag, or sharing screenshots.

## Privacy and repository safety

- [ ] Run `./scripts/audit-public-repo.ps1`.
- [ ] Run `./scripts/check-source-consistency.ps1` successfully.
- Optionally create an untracked `private-audit-terms.txt` file with personal merchant names, employer names, account fragments, SQL server names, and other private search terms, then run:

```powershell
./scripts/audit-public-repo.ps1 -ExtraTermsFile .\private-audit-terms.txt
```

- Confirm no real financial exports are committed.
- Confirm no database backups, MDF, LDF, DACPAC, BACPAC, PDF statements, Excel workbooks, screenshots with real balances, logs, or local settings are committed.
- Confirm no real connection string, SQL login, password, IP address, account number, routing number, or merchant-specific private rule set is committed.
- Confirm screenshots use demo data or are redacted.

## Build and test

- [ ] Run `./scripts/verify-public-baseline.ps1` from a clean checkout.
- [ ] Confirm Debug build, Release build, and tests complete with zero errors and zero solution-owned warnings.
- [ ] Confirm repository audit, source consistency checks, Debug build, Release build, and tests pass.
- [ ] Confirm the generated release ZIP and `.sha256` file are created under `artifacts/release/`.
- [ ] Confirm the release-package audit passes.

Developer-only build check:

```powershell
dotnet clean .\PersonalFinanceManager.sln
.\build.ps1
```

The build script runs the public repository audit, source consistency check, package restore, Debug build, Release build, and tests.

## First-run smoke test

Use a clean LocalDB test database or disposable network SQL database.

1. Delete or move `%LocalAppData%\PersonalFinanceManager\appsettings.Local.json`.
2. Launch PersonalFinanceManager.
3. Confirm the Setup page opens.
4. Click **Use LocalDB Now**.
5. Open **Database** and confirm migrations are current.
6. Click **Load Demo Data** from Setup if the database is empty.
7. Confirm the following screens open without errors:
   - Accounts
   - Transactions
   - Rules
   - Imports
   - Duplicate Review
   - Transaction Links
   - Expense Analysis
   - Money In/Out
   - Salary Needed
   - Bills
   - Runway
   - Data Health
   - Backup/Export
   - Settings
   - Database

## Demo-data verification

- Confirm demo data loads only into an empty database.
- Confirm demo data contains no real institutions, merchants, balances, account numbers, or transaction descriptions.
- Confirm Expense Analysis, Money In/Out, and Runway show useful non-empty demo output.

## Public documentation

- Review `README.md` from the perspective of a first-time user.
- Confirm setup instructions match current application behavior.
- Confirm supported import formats are accurate.
- Confirm security and privacy wording is current.
- Confirm limitations are honest and visible.
- Confirm `CHANGELOG.md` is updated.
- Confirm `ROADMAP.md` reflects the next planned work.

## Publish package

Framework-dependent publish:

```powershell
.\publish.ps1
```

Self-contained publish:

```powershell
.\publish.ps1 -SelfContained
```

Review output under `artifacts/publish/`. Do not commit generated artifacts.

## Tagging

Suggested versioning before the first public release:

```text
v0.1.0-alpha
v0.2.0-alpha
v1.0.0
```

Use pre-1.0 tags until the database setup, importer behavior, backup/export flow, and demo-data workflow have been repeatedly verified.

## Release package validation

- [ ] Run release package script: `./scripts/package-release.ps1`.
- [ ] Confirm `artifacts/release/PersonalFinanceManager-win-x64-framework-dependent.zip` is created.
- [ ] Confirm the matching `.sha256` file is created.
- [ ] Extract the release ZIP to a clean temporary folder.
- [ ] Confirm the package contains `PersonalFinanceManager.exe`, `install-personal-finance-manager.ps1`, `uninstall-personal-finance-manager.ps1`, `README-FIRST.txt`, `INSTALL.md`, `README.md`, `PRIVACY.md`, and `LICENSE`.
- [ ] Launch `PersonalFinanceManager.exe` directly from the extracted folder.
- [ ] Run `install-personal-finance-manager.ps1` and verify the Start Menu shortcut opens PersonalFinanceManager.
- [ ] Run `uninstall-personal-finance-manager.ps1` and verify installed app files are removed while local data is preserved.

## Screenshot and demo-data review

- Capture public screenshots only from demo data or a synthetic database.
- Review `SCREENSHOTS.md` before committing screenshots.
- Confirm no screenshots contain account numbers, private merchants, employers, real balances, user folders, machine names, or private IP addresses.
- Confirm `assets/screenshots/private/` and `screenshots/private/` are not tracked.

## Limitation review

- [ ] Review `KNOWN_LIMITATIONS.md` for accuracy.
- [ ] Confirm public README links to known limitations.
- [ ] Confirm release notes do not overstate calculation accuracy or import coverage.

- [ ] If packaging is run manually, verify `package-release.ps1` uses named parameters such as `-SkipBuild` and `-SkipPublicAudit`; do not pass switch names through positional arrays.
