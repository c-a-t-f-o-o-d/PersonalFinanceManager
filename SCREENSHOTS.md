# Screenshot Guide

Screenshots are useful for the public repository, but they must be created from the built-in demo data or from a clean synthetic database.

## Recommended screenshot set

Use the Setup page to configure LocalDB and load demo data, then capture:

1. Setup
2. Accounts
3. Transactions
4. Rules
5. Expense Analysis
6. Money In/Out
7. Salary Needed
8. Bills
9. Runway
10. Data Health

Store public screenshots under:

```text
assets/screenshots/
```

## Privacy checklist

Before committing screenshots, verify they do not contain:

- real account names or account numbers
- real merchants or payees
- real employer names
- real balances or debts
- real connection strings
- real import file paths
- real transaction descriptions
- private machine names, user folders, or IP addresses

If you are unsure whether a screenshot is safe, recreate it from the demo database.

## Private screenshots

Private screenshots may be useful while debugging local data. Do not commit them. The repository ignores:

```text
screenshots/private/
assets/screenshots/private/
```
