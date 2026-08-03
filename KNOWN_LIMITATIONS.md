# Known Limitations

PersonalFinanceManager is usable for local personal-finance analysis, but it is still a desktop project with several intentional limitations.

## Platform

- PersonalFinanceManager is Windows-only.
- The user interface is built with Windows Forms.
- The main tested target is Windows 11.

## Data storage

- SQL Server LocalDB is the easiest first-run option for reviewers and contributors.
- Network SQL Server is supported for advanced users.
- SQLite or another single-file database provider is not implemented yet.
- Database schema migration is intentionally lightweight and SQL-script based.

## Security

- Connection strings are stored locally using Windows DPAPI.
- DPAPI-protected values are tied to the Windows user profile that saved them.
- Full account numbers protected on one machine/user may not be decryptable on another machine/user.
- PersonalFinanceManager is not a multi-user web application and does not provide role-based authorization.

## Imports

- Import formats are built from observed exports and may need updates if a financial institution changes its CSV or statement format.
- PDF parsing is best-effort and intentionally avoids third-party PDF libraries.
- Imported data should be reviewed through the transaction, duplicate review, transaction links, and data health screens.

## Calculations

- Salary, tax, runway, and reporting calculations are planning estimates, not tax, legal, or financial advice.
- Credit-card and PayPal funding links improve reporting accuracy, but unusual payment flows may require manual review.
- Loan, interest, and amortization estimates may differ from lender-specific calculations.

## Public demo mode

- Demo data is synthetic and intended only for screenshots, review, and development.
- Demo data should not be interpreted as financial guidance.
