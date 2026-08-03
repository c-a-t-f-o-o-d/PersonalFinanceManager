# Changelog

All notable public-facing changes to PersonalFinanceManager will be documented in this file after the first public release.


## Unreleased

### Changed
- Improved `install-personal-finance-manager.ps1` so running it from a source checkout shows a clear source-vs-release-package message and can install from `artifacts/release/PersonalFinanceManager` when a release staging folder exists.


### Iteration 64

- Fixed release-package invocation from public baseline verification so PowerShell 5.1 does not pass `-SkipBuild` or `-SkipPublicAudit` as positional package arguments.
- Hardened `package-release.ps1` with clearer parameter validation and checked PowerShell/native command execution helpers.


- Cleaned the remaining MSTest analyzer warnings in `ApplicationIdentityTests` so the current public baseline can build without warnings.
- Fixed PowerShell audit/build compatibility so native-tool exit codes inside audit scripts do not cause false build failures.
- Qualified WinForms application and product identity references after introducing the PersonalFinanceManager.Application project.

### Added

- Added a public-ready architecture note in `ARCHITECTURE.md`.
- Added first-run setup guidance and a one-click LocalDB setup path.
- Added a sanitized demo-data loader for empty evaluation databases.
- Added release packaging, install, and uninstall scripts.
- Added public repository audit tooling.
- Added release-package audit tooling.
- Added public baseline verification script.
- Added source consistency checks for project boundaries and architecture hygiene.
- Added `QUALITY_GATES.md` to document repository, source, and release checks.
- Added GitHub issue templates, pull request template, contribution guidance, security guidance, privacy guidance, roadmap, and release checklist.
- Added `PersonalFinanceManager.Application` for UI-facing workflow service contracts.
- Added tests guarding SQL resource loading and service-boundary expectations.

### Changed

- Centralized public application identity values in `ApplicationIdentity` to reduce future rename and branding friction.
- Made the public repository audit script compatible with Windows PowerShell 5.1.
- Renamed the public application/project to `PersonalFinanceManager`.
- Removed private connection strings, local settings, personal rule seeds, and generated documentation from the public package.
- Moved SQL statements into embedded SQL resource files grouped by feature.
- Reworked WinForms views to depend on workflow/application service contracts instead of repositories.
- Moved workflow service interfaces out of `PersonalFinanceManager.Data` so presentation code depends on application-level contracts.
- Moved import workflow DTOs into `PersonalFinanceManager.Core.Models` so public service contracts do not expose data-layer implementation types.
- Organized database scripts into `sql/migrations` and `sql/baseline`.
- Added migration tracking through `dbo.SchemaMigration`.
- Added shared project metadata and an MIT license.
- Added public screenshot guidance, demo workflow notes, and development documentation.

### Removed

- Removed personal/default rule data from source-controlled migrations.
- Removed obsolete V2 naming from source, project files, settings templates, scripts, and documentation.

## Iteration 63

- Cleaned `ApplicationIdentityTests` to avoid MSTest analyzer argument-order warnings and support a zero-warning public baseline build.

## Iteration 62

- Fixed Windows PowerShell parsing in `scripts/check-source-consistency.ps1` by replacing a double-quoted regular expression containing raw string literal quotes with a single-quoted pattern.
- This unblocks `build.ps1` after the public repository audit when the source consistency check runs under Package Manager Console / Windows PowerShell.

## Iteration 60

- Fixed PowerShell 5.1 compatibility in public repository and release package audit scripts by replacing `string.Contains(value, comparison)` usage with a compatible helper.
- Added `KNOWN_LIMITATIONS.md` for public release transparency.
- Linked known limitations from release/public documentation.
