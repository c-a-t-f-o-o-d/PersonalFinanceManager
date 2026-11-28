# Roadmap

PersonalFinanceManager is under active development. This roadmap focuses on public-readiness, maintainability, and user-facing reliability.

## Near term
- Continue reducing hardcoded product identity strings by using `ApplicationIdentity` for user-facing names and setup defaults.

- Finish public-release cleanup.
- Keep SQL resources organized by feature.
- Continue improving test coverage around importers, duplicate handling, transaction links, and salary/runway calculations.
- Improve first-run setup and error messages.
- Validate the demo-data workflow end to end.

## Data and architecture

- Continue separating UI, workflow services, data access, and calculation logic.
- Keep `PersonalFinanceManager.Application` focused on service contracts and application-level abstractions, not data access implementations.
- Add more service-level tests using mockable interfaces.
- Consolidate schema documentation around the baseline script and migration chain.
- Keep ADO.NET explicit while avoiding large inline SQL strings.
- Evaluate whether a future lightweight local data provider is appropriate for non-technical users.

## User-facing features

- Improve import-profile management and import preview behavior.
- Add richer duplicate-review workflows.
- Add more transaction-link review tools for credit accounts, digital wallets, and internal transfers.
- Improve recurring bill detection and required-expense modeling.
- Expand salary-needed, runway, and cash-flow analysis.
- Add savings-goal and account-purpose tracking.

## Public usability

- Add a simple release package.
- Add a guided setup walkthrough.
- Improve documentation for common SQL Server and LocalDB setup problems.

## Long-term possibilities

- Optional SQLite or single-file local data provider.
- Optional app-level encryption key for multi-machine database usage.
- Import-profile editor.
- Plugin-style import profile model.
- Better report export and chart customization.

## Release packaging polish

- Add installer script and uninstall script for local per-user installs.
- Add release package generation with SHA-256 hash output.
- Keep the package format simple until a full installer is justified.

## Recent cleanup checkpoint

- SQL resource final sweep moved schema/bootstrap SQL into embedded resources.
- Remaining near-term focus: review service contracts, polish first-run setup, and keep public documentation aligned with the application.


## Release verification

- Keep the public baseline verification script as the final gate before public pushes, pull requests, and release packages.
- Expand the release-package audit if new supported file types or package layouts are introduced.

- Full public baseline verification script added to support release readiness.

## Documentation and release polish

- Keep `KNOWN_LIMITATIONS.md` current as storage providers, import formats, and calculation features evolve.


## Public baseline polish notes

- PowerShell 5.1 script compatibility is part of the public baseline because many Visual Studio users run scripts from Package Manager Console or Windows PowerShell.
