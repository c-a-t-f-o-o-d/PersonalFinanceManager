# Contributing to PersonalFinanceManager

Thank you for considering a contribution. PersonalFinanceManager handles sensitive personal-finance data, so privacy and data hygiene are part of the development process.

## Development setup

1. Install .NET SDK 10.0.x.
2. Install Visual Studio with the .NET desktop development workload, or use the .NET CLI.
3. Clone the repository.
4. Run the build script:

```powershell
.\build.ps1
```

For a local review database, run PersonalFinanceManager and choose **Use LocalDB Now** from Setup.

## Before opening a pull request

Run:

```powershell
.\build.ps1
```

This performs the repository privacy audit, restores packages, builds Debug and Release, and runs tests.

To include SQL integration tests on Windows, start LocalDB and enable the test fixture before building:

```powershell
sqllocaldb start MSSQLLocalDB
$env:PFM_RUN_SQL_TESTS = "1"
.\build.ps1
```

These tests create and remove isolated databases named `PFM_Integrity_<guid>` on `(localdb)\MSSQLLocalDB`. They use synthetic activity to verify migration replay, duplicate approval, transfers, wallet funding, credit repayments, statement reconciliation, and backups. They do not use the application's saved connection string. The build pull-request job runs this suite; other environments skip it when the opt-in setting is absent.

If you need to check for private local terms, use an untracked file:

```powershell
.\scripts\audit-public-repo.ps1 -ExtraTermsFile .\private-audit-terms.txt
```

Do not commit that file.

## Privacy rules

Never commit:

- real bank or credit-card exports
- account statements
- SQL backups
- MDF/LDF files
- local settings files
- logs
- screenshots containing personal financial data
- account numbers or routing numbers
- personal merchant-rule lists
- private connection strings or passwords

Use sanitized demo data or synthetic test fixtures only.

## Code style

The project uses C# with explicit ADO.NET data access.

Guidelines:

- Keep non-trivial SQL in embedded resource files under `src/PersonalFinanceManager.Data/Sql/Queries`.
- Use typed SQL parameters and avoid `AddWithValue`.
- Keep parsers deterministic and culture-invariant.
- Keep UI code focused on presentation and delegate workflow logic to services.
- Avoid introducing third-party dependencies unless they are clearly justified.
- Keep build and audit scripts passing.

## Pull request checklist

- [ ] Build passes locally.
- [ ] Tests pass locally.
- [ ] Public repository audit passes.
- [ ] No private financial data is included.
- [ ] SQL changes include safe migrations when needed.
- [ ] New parsing or calculation behavior has tests.
- [ ] README or related root-level documentation is updated when user-facing behavior changes.
