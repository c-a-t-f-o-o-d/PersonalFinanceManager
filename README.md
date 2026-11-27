# PersonalFinanceManager

PersonalFinanceManager is a Windows desktop personal-finance application for people who want direct control over their financial data. It imports account activity, applies user-defined categorization rules, links related transactions, tracks recurring bills, and provides analysis views for expenses, cash flow, salary planning, and runway planning.

The project is intentionally self-hosted. Your database is under your control, and the application does not send telemetry or financial data to a hosted service.

## Highlights

- Windows Forms desktop application built with C# and .NET 10.
- SQL Server-backed data store with LocalDB setup for local evaluation.
- ADO.NET data access using `Microsoft.Data.SqlClient`; no Entity Framework dependency.
- User-defined transaction rules with direction-aware matching.
- Import support for multiple CSV/PDF-text export styles.
- Duplicate-review workflow for overlapping imports.
- Transaction-link workflow for transfers, PayPal funding, and credit-card payments.
- Statement balance reconciliation that retains every imported account entry.
- Expense analysis with category and category-type breakdowns.
- Money-in / money-out analysis.
- Salary-needed analysis using spending trends, bills, and paycheck assumptions.
- Recurring bill tracking and required-expense support.
- Runway analysis based on balances and required bills.
- Data-health checks to highlight records that may make reporting unreliable.
- Backup/export tools and sanitized demo data for evaluation.

## Status

PersonalFinanceManager is under active development. It is suitable for local experimentation and portfolio/code review, but it is not yet packaged as a consumer-ready finance product. The project currently prioritizes transparent local data ownership and explicit workflows over cloud synchronization or automatic bank connectivity.

## Requirements

For development:

- Windows 10 or Windows 11
- .NET SDK 10.0.x
- Visual Studio with the .NET desktop development workload, or equivalent .NET CLI tooling
- SQL Server LocalDB, SQL Server Express, SQL Server Developer Edition, or another SQL Server instance

For basic evaluation, the easiest path is SQL Server LocalDB through the first-run Setup page.

## Installation options

PersonalFinanceManager can be run from source, published as a folder, or packaged into a reviewer-friendly release ZIP.

For non-developer evaluation, create a release package:

```powershell
./scripts/package-release.ps1
```

The release package is written under:

```text
artifacts/release/
```

After extracting the package, run:

```powershell
./install-personal-finance-manager.ps1
```

or launch `PersonalFinanceManager.exe` directly from the extracted folder.

From a source checkout, create the release staging folder first:

```powershell
./scripts/package-release.ps1
./scripts/install-personal-finance-manager.ps1 -SourcePath ./artifacts/release/PersonalFinanceManager
```

For development, you can skip installation and run the app directly with `dotnet run`. See `INSTALL.md` for install, uninstall, and local-data details.

## Quick start

Clone the repository and build the solution:

```powershell
dotnet clean .\PersonalFinanceManager.sln
.\build.ps1
```

Run the application project:

```powershell
dotnet run --project .\src\PersonalFinanceManager.App\PersonalFinanceManager.App.csproj
```

On first launch, PersonalFinanceManager opens the Setup page if no usable connection string is configured.

### Fast local setup

1. Open PersonalFinanceManager.
2. On Setup, select **Use LocalDB Now**.
3. Open **Database** and confirm migrations are current.
4. Optionally select **Load Demo Data** if you are using an empty evaluation database.
5. Explore Accounts, Transactions, Rules, Expense Analysis, Money In/Out, Salary Needed, Bills, Runway, and Data Health.

### Network SQL Server setup

For a shared SQL Server, open **Settings**, choose the network SQL template, replace the placeholders, save, and test the connection.

Example shape only:

```text
Data Source=tcp:YOUR_SQL_SERVER_OR_IP,14333;Initial Catalog=PersonalFinanceManager;User ID=YOUR_SQL_LOGIN;Password=YOUR_SQL_PASSWORD;Persist Security Info=False;Pooling=True;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=True;Application Name=PersonalFinanceManager
```

Do not commit real connection strings. Saved local settings are written to:

```text
%LocalAppData%\PersonalFinanceManager\appsettings.Local.json
```

The saved connection string is protected with Windows DPAPI for the current Windows user.

## Demo data

The Setup page includes **Load Demo Data** for empty databases. Demo data creates generic sample accounts, rules, transactions, transaction links, and recurring bills.

The loader refuses to run if the database already contains accounts, rules, transactions, or recurring bills. This is intentional and helps prevent accidental mixing of demo data with real financial records.

See `DEMO_WORKFLOW.md` for a reviewer-friendly walkthrough.

## Supported workflows

### Accounts

Accounts store institution details, account type, owner, balance fields, a balance-as-of date, interest/APR fields, due dates, payment amounts, and optional protected account-number data. Use separate accounts for a PayPal wallet and PayPal Credit or Smart Connect: a wallet holds cash, while a credit account holds debt. Apple Pay is a payment method; Apple Cash is a cash account, and Apple Card is a credit account.

Account balances are entered snapshots. Importing transactions does not update them. Runway uses available balance before current balance, retains overdrafts, and excludes credit limits and loan balances from spendable cash.

Select an account and choose **Reconcile Statement** to compare its opening balance plus posted activity with its closing statement balance. For credit and loan accounts, enter the amount owed as a positive balance; charges increase it and payments or refunds reduce it. The check includes transfers, repayments, ignored rows, and mirrored purchases because those entries still belong on their respective account statements. Posting dates take precedence over transaction dates; the result identifies rows that needed the transaction-date fallback. A recorded check preserves the result at that time and does not change transactions or balances.

### Imports

PersonalFinanceManager provides parsers for these export formats:

- Ally transaction CSV exports
- PayPal statement PDF/text exports
- Chase / Amazon credit-card CSV exports
- Apple Card CSV exports
- Regions checking and savings CSV exports
- Normalized bank, wallet, or card activity CSV, including a separate normalized Smart Connect profile

Native provider formats can vary. Preview each import and review parser errors before saving. PayPal PDF/text parsing keeps each transaction bounded to its own record. If a detached funding section cannot be associated unambiguously, the economic transactions remain available and the preview reports missing funding metadata. Supply a normalized CSV when the native layout cannot preserve those relationships reliably.

The PayPal PDF/text profile imports its **account activity** section, which is not a proven complete PayPal balance ledger. Direct bank/card-funded purchases may lack offsetting wallet funding credits. Wallet balance checks refuse periods containing this profile; reconcile the funding accounts, or import a complete normalized wallet balance ledger into a separate account and reconcile duplicate purchase representations explicitly. Unsupported or ambiguous positive PayPal transaction types are reported as parse errors rather than guessed as income or refunds.

Import every account's statement into that account. Matching entries on different providers are retained for reconciliation. Exact repeated rows can be skipped automatically; **Duplicate Review** also exposes exact skips so a legitimate repeat purchase can be recovered. Equal amounts and nearby dates alone do not prove a duplicate or a transfer. Reimporting a file checks its individual rows; a recognized previously deleted source entry goes to Duplicate Review before restoration.

#### Normalized CSV contract

Choose **Normalized Bank / Wallet / Card CSV** for a provider without a supported native parser, or **PayPal Smart Connect (normalized CSV)** for prepared Smart Connect activity. The latter does not claim support for an undocumented native Smart Connect export format.

Required headers are `Date,Amount,Type,Description`. Optional headers are `Account,Transaction ID,Posting Date,Time,Currency,Funding Source,Category,Category Type,Merchant,Memo`.

```csv
Date,Amount,Type,Description,Account,Transaction ID,Currency,Funding Source
2026-01-02,-60.00,Purchase,Sample merchant A,Sample PayPal,order-a,USD,Sample checking 1234
2026-01-06,-40.00,Purchase,Sample merchant B,Sample PayPal,order-b,USD,Sample checking 1234
```

- Dates use `yyyy-MM-dd` or `MM/dd/yyyy`; amounts must represent exact cents.
- Purchases, fees, and cash withdrawals are negative. Deposits, refunds, and credit-account payments are positive. A payment from checking to a credit account is negative in checking and positive on the credit account.
- `Currency`, when supplied, must be `USD`. Currency conversion is not modeled.
- Use stable provider transaction IDs when available. `Account`, when supplied, must identify the selected account by nickname, account number, or last four digits. Conflicting account identity rejects the import.
- Preserve refund and repayment types. Do not turn a payment into income to force a matching total.

### Rules

Rules assign category, category type, income/transfer flags, and optional owner information. Rule matching uses the amount's sign for `Deposit` or `Withdrawal`, even when a source uses a misleading type label. Manual entries and classifications marked **Keep these choices when rules are reapplied** are preserved. Link relationships remain separate from merchant categorization, so updating a merchant rule does not break a transfer or funding match.

### Transaction links

Transaction links describe how retained account entries relate to the same purchase or movement of money. On **Transaction Links**, choose **Add Link**, select the source and linked entries, and choose the relationship:

| Relationship | Source → linked entry | Effect on spending |
| --- | --- | --- |
| `BankTransfer` | Checking withdrawal → equal savings deposit | Neither entry is income or expense. Both remain in their account ledgers. |
| `PurchaseMirror` | Original PayPal or Apple Pay purchase → equal signed bank/card entry | Count the original purchase once. The other entry is its funding representation. |
| `PurchaseFundingAllocation` | Wallet purchase → a combined bank/card debit | Enter the portion funded. Several purchases may share the debit, and a purchase may be split across funding debits. Any unallocated debit remains visible. |
| `CreditCardPaymentFunding` | Positive card/Smart Connect payment → equal negative checking payment | Repayment of debt is separate from new purchases. Its amount and date need not match individual purchases. |
| `CreditCardPaymentAllocation` | Positive credit-account payment → a charge on that same credit account | Optional payment attribution, bounded by the payment and purchase amounts; it does not add spending. |
| `PurchaseRefund` | Positive refund → its original negative purchase in the same account | Reduce the purchase's expense category by the refunded portion. A later refund is allowed after the purchase has been paid. |
| `ManualReview` | Two entries requiring investigation | Record a relationship without changing financial treatment. |

For example, two PayPal purchases of $60 and $40 followed by a $100 direct checking debit can use two funding allocations. If those purchases instead used Smart Connect, import its charges as well, link duplicate purchase representations, and link the later Smart Connect payment to checking. The $100 repayment does not become another $100 expense.

PayPal may also list a `Buyer Credit Payment` for that same repayment. Its matching `PurchaseMirror` and the Smart Connect `CreditCardPaymentFunding` relationship may share the same checking entry when the account types, payment types, and amounts agree. This preserves all three records while counting one cash outflow. Ordinary purchase mirrors cannot reuse a repayment debit this way.

Automatic matching requires explicit account-reference evidence and an unambiguous counterpart. Wallet purchase matches also require corroborating provider, merchant, or transaction-reference information; a bank account reference and equal amount alone are insufficient. **Repair Data Links** uses the same rules. Ambiguous records need manual review; the application does not guess which older purchases an aggregate payment settled. Allocation totals cannot exceed the participating entries. Editing or removing a link updates reporting without rewriting merchant categories, and a removed automatic match is remembered so repair does not immediately recreate it. Remove links before changing a linked entry's amount, account, date, or type.

Earlier releases sometimes overwrote transaction categories while classifying funding rows. The upgrade marks recognizable legacy rows and moves earlier automatic links, along with structurally invalid links, to `ManualReview`. It retains their endpoints, amounts, and original relationship in the review notes. Confirm those relationships against statements before giving them financial effect again. Data Health highlights both the links and categories needing review. Previously overwritten merchant categories cannot be reconstructed from a balance or amount alone.

### Expense analysis

Expense Analysis summarizes purchases net of refunds by category or category type, shows monthly spending patterns, and exposes detailed transaction rows for review. Transfers, confirmed debt repayments, linked funding representations, and `Ignore` categories are excluded consistently. Unmatched bank debits remain visible until reconciled. Refunds reduce net expense and retain their entered category until assigned or linked to a purchase. Averages use the selected date interval, including months without activity.

### Money In/Out

Money In/Out shows cash-account activity. At household level it excludes internal cash transfers and duplicate wallet funding representations. Credit repayments remain cash outflows even though they are excluded from purchase spending. An individual account view retains that account's transfer movements. This view and Expense Analysis answer different questions: when cash moved and when a purchase occurred.

### Salary Needed

Salary Needed estimates the gross salary required to support selected spending patterns. It can use full expense trends, required bills, selected spending buckets, or combinations of those inputs. Income and expenses share the same observation period; an all-history estimate includes income-only months and identifies its observed coverage.

Choose either posted spouse net income or modeled spouse gross income. Posted take-home pay is not added to gross pay and taxed a second time. In the posted-net mode, a supplied spouse gross amount is used only for the household marginal-tax context. When planned bills and historical spending are combined, clearly matched historical bill payments are replaced by the plan. Uncertain overlaps and missing required inputs produce review notes instead of a reliable-looking zero estimate. Tax calculations remain estimates based on the configured assumptions.

### Recurring bills

Bills can be added manually or created from recurring transaction candidates. Candidate frequency is inferred from the observed dates; ambiguous histories require a frequency selection. Required bills feed salary and runway calculations. Runway begins on the projection date and schedules known bill due dates. Income and bills with unknown due dates are explicitly shown as smoothed estimates, and missing or stale balance snapshots are highlighted.

### Data Health

Data Health identifies records that may make reports unreliable, such as missing categorization, unowned income, missing or stale balance snapshots, missing interest/APR values, duplicate-looking rows, transfer-like rows without links, and legacy funding classifications requiring review. A matched statement balance is an additional check; offsetting omissions can still produce a matching total.

### Database backups and exports

SQL backups use the database specified by the active connection string and verify the resulting backup. CSV exports capture a consistent snapshot of accounts, rules, transactions, recurring bills, transaction links, and recorded statement checks. A SQL backup is still required for full-fidelity database restoration.

## Privacy and security

PersonalFinanceManager is designed for local or self-hosted use.

- The application does not send telemetry.
- Real exports, statements, backups, logs, and local settings are ignored by Git.
- Local connection strings are saved in a DPAPI-protected local settings file.
- Optional account-number fields are protected with Windows DPAPI.

DPAPI protection is tied to the Windows user profile. If the database is shared across machines, encrypted account-number values created on one Windows profile may not be decryptable on another machine. PersonalFinanceManager handles that condition without crashing, but multi-machine account-number viewing will eventually need a different shared-key model.


### Public baseline verification

Before opening a pull request or creating a public release package, run:

```powershell
./scripts/verify-public-baseline.ps1
```

A public baseline should complete with zero errors and zero solution-owned warnings.

For a source-only architecture/privacy check without packaging, run:

```powershell
./scripts/audit-public-repo.ps1
./scripts/check-source-consistency.ps1
```

This command runs the public repository audit, a clean build/test pass, release packaging, and a release-package audit. To run only the source audit/build/test portion:

```powershell
./scripts/verify-public-baseline.ps1 -SkipPackage
```

If you maintain a private untracked list of terms that should never appear in public source or release packages, pass it with:

```powershell
./scripts/verify-public-baseline.ps1 -ExtraTermsFile .\private-audit-terms.txt
```

## Public repository audit

Before publishing or opening a pull request, run:

```powershell
.\scripts\audit-public-repo.ps1
```

The audit checks for common mistakes such as:

- local settings files
- database backups
- real exports or statements
- legacy product naming
- private LAN addresses
- plaintext SQL passwords
- committed `docs/` content

You can also provide private search terms from an untracked file:

```powershell
.\scripts\audit-public-repo.ps1 -ExtraTermsFile .\private-audit-terms.txt
```

The file should contain one private term per line, such as personal merchant names, account fragments, employers, or other strings that should never be committed.

## Build and test

The build script runs the public repository audit and source consistency gate before restoring, building, and testing.


Use the build script:

```powershell
.\build.ps1
```

The script runs:

1. public repository audit
2. package restore
3. Debug build
4. Release build
5. tests

Skip only the audit step when needed:

```powershell
.\build.ps1 -SkipPublicAudit
```

## Publish locally

Create a framework-dependent local publish output under `artifacts/publish`:

```powershell
.\publish.ps1
```

Create a larger self-contained package:

```powershell
.\publish.ps1 -SelfContained
```

The generated `artifacts/` folder is ignored by Git.

## Branding and project identity

The public application identity is centralized in `PersonalFinanceManager.Core.Application.ApplicationIdentity`. Display names, LocalAppData folder naming, backup file prefixes, and safe connection-string templates should use that shared identity rather than hardcoding the product name in new code. This keeps future renaming less error-prone.

## Architecture overview

See `ARCHITECTURE.md` for a fuller architecture note. See `DEVELOPMENT.md` for build, SQL-resource, service-boundary, and privacy conventions. The solution is organized into four primary projects:

```text
src/PersonalFinanceManager.App          Windows Forms UI and composition root
src/PersonalFinanceManager.Application  UI-facing workflow service contracts
src/PersonalFinanceManager.Core         Domain models, parsing, rules, calculations
src/PersonalFinanceManager.Data         SQL Server access, repositories, service implementations, settings, logging
```

The UI uses workflow/application service contracts from `PersonalFinanceManager.Application` rather than directly coordinating repositories. SQL statements are stored as embedded SQL resources under:

```text
src/PersonalFinanceManager.Data/Sql/Queries
```

Repositories and services load those SQL files through `SqlResourceReader`, preserving explicit ADO.NET behavior while avoiding large inline SQL blocks in C# methods. Schema/bootstrap queries also use the same resource pattern so application code does not carry database DDL or status SQL inline.

## Schema organization

Schema scripts are organized under:

```text
sql/
  baseline/
    create_schema.sql
  migrations/
    *.sql
```

At runtime, PersonalFinanceManager executes ordered scripts from `sql/migrations`. The consolidated baseline script is available for review and clean provisioning, but it is not executed by the application initializer.

Migration execution is tracked in:

```text
dbo.SchemaMigration
```

The older `dbo.SchemaMetadata` markers are still used for compatibility with databases created before migration tracking was introduced.

## Import and data-quality model

PersonalFinanceManager intentionally distinguishes between:

- the original imported transaction row
- categorization rules
- duplicate-review rows
- transaction links
- recurring-bill records
- analysis outputs

This separation lets PersonalFinanceManager preserve traceability while also giving the user control over cleanup and reporting semantics.

## Development guidelines

- Keep real financial exports and backups out of the repository.
- Prefer explicit workflow services over view-level orchestration.
- Keep SQL in embedded resource files when queries are non-trivial.
- Use typed SQL parameters; do not use `AddWithValue`.
- Keep import parsers deterministic and covered by tests.
- Add demo/synthetic fixtures only; do not commit real financial rows.
- Run `build.ps1` before commits.

## Contributing

See `CONTRIBUTING.md` for development workflow, privacy requirements, and pull-request expectations.

## Security

See `SECURITY.md` for how to report security issues and for the current security model.

## Roadmap

Near-term cleanup and public-readiness work:

- Complete installer/non-technical setup decisions.
- Improve sample/demo data coverage.
- Finish remaining service-boundary cleanup.
- Validate clean-database provisioning from the consolidated schema baseline.
- Expand tests around imports, linking, and reports.
- Add screenshots after the UI and demo data stabilize. See `SCREENSHOTS.md` for the privacy checklist.
- Decide on final product name before the first public release tag if `PersonalFinanceManager` is not kept.


## Release checklist

Before publishing or tagging a release, review `RELEASE_CHECKLIST.md`. It contains privacy checks, demo-data validation, build/test commands, and packaging steps.

## Supporting documents

- `ARCHITECTURE.md` summarizes project boundaries and data-access design.
- `DEVELOPMENT.md` covers local development conventions.
- `DEMO_WORKFLOW.md` provides a safe demo-data walkthrough.
- `SCREENSHOTS.md` explains how to capture public-safe screenshots.
- `ROADMAP.md` covers near-term cleanup and long-term feature direction.
- `PRIVACY.md` covers the local data model, DPAPI account-number behavior, and public repository safety guidance.

## Privacy

See `PRIVACY.md` for the local data model, DPAPI account-number behavior, and public repository safety guidance.

## License

PersonalFinanceManager is available under the MIT License. See `LICENSE`.

## Release package creation

Use this command to create a Windows release ZIP that includes the published app, install scripts, license, README, and privacy notes:

```powershell
./scripts/package-release.ps1
```

For a self-contained Windows package:

```powershell
./scripts/package-release.ps1 -SelfContained
```

The release ZIP and SHA-256 hash file are written to:

```text
artifacts/release/
```

The release package includes `install-personal-finance-manager.ps1` and `uninstall-personal-finance-manager.ps1` for a local per-user install under `%LocalAppData%\Programs\PersonalFinanceManager`.

## GitHub release package workflow

The repository includes a manual GitHub Actions workflow named **Release Package**. It builds a Windows release package and uploads the ZIP and SHA-256 hash as workflow artifacts. Use this for public review packages once the local release checklist passes.


See `QUALITY_GATES.md` for repository, source, and release verification checks.

## Known limitations

See [KNOWN_LIMITATIONS.md](KNOWN_LIMITATIONS.md) for current limitations around platform support, data storage, security, import coverage, and calculation assumptions.


## PowerShell compatibility note

The build, audit, and source consistency scripts are intended to run in Windows PowerShell 5.1 and PowerShell 7+. The scripts avoid newer .NET-only PowerShell conveniences and reset stale native command exit codes after helper scripts so successful audits are not reported as failures because of an internal Git probe.


### Packaging verification note

If `verify-public-baseline.ps1` fails during packaging, run `./scripts/package-release.ps1 -SkipBuild -SkipPublicAudit` directly. The packaging scripts intentionally use explicit named-parameter calls so PowerShell 5.1 does not misinterpret switch names as positional arguments.
