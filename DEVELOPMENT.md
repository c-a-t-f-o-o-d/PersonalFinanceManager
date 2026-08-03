# Development Notes

This file summarizes the development workflow for contributors and future maintainers.

## Build

```powershell
./build.ps1
```

The build script runs the public repository audit, restore, Debug build, Release build, and tests.

## Public baseline verification

Before publishing or tagging a release:

```powershell
./scripts/verify-public-baseline.ps1
```

For source-only verification:

```powershell
./scripts/verify-public-baseline.ps1 -SkipPackage
```

## SQL strategy

PersonalFinanceManager intentionally uses ADO.NET and explicit SQL rather than Entity Framework. Non-trivial SQL lives in embedded resource files under:

```text
src/PersonalFinanceManager.Data/Sql/Queries/
```

Schema scripts live under:

```text
sql/baseline/
sql/migrations/
```

The runtime initializer executes only `sql/migrations/*.sql` and tracks execution in `dbo.SchemaMigration`.

## Service boundaries

UI views should depend on contracts from `PersonalFinanceManager.Application`, not repositories. Concrete workflow services live in `PersonalFinanceManager.Data` and are created in the WinForms composition root.

## Privacy rules

Never commit real financial exports, real screenshots, account statements, database backups, local settings files, or private audit term files.
