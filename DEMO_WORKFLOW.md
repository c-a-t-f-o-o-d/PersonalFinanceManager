# Demo Workflow

This workflow is intended for public reviewers and screenshots.

## Create a clean demo database

1. Start PersonalFinanceManager.
2. Open Setup.
3. Select **Use LocalDB Now**.
4. Open Database and confirm migrations are current.
5. Return to Setup and select **Load Demo Data**.

The demo loader runs only when the database is empty. It creates synthetic accounts, transactions, rules, transaction links, and recurring bills.

## Suggested review path

1. Open Accounts and confirm the synthetic accounts loaded.
2. Open Transactions and review the demo activity.
3. Open Rules and inspect the generated demo categorization rules.
4. Open Expense Analysis and review the category breakdown.
5. Open Money In/Out and review monthly cash flow.
6. Open Bills and confirm required recurring expenses are visible.
7. Open Salary Needed and try different spending-basis options.
8. Open Runway and review projected balances.
9. Open Data Health and confirm any warnings are explainable.

## Resetting demo data

The current app does not include a destructive reset button. For a fresh demo, create a new LocalDB database or remove the existing evaluation database through your SQL Server tools.
