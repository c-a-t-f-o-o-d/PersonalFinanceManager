# Import formats and provider exports

PersonalFinanceManager imports files that you download and choose locally. It does not connect to financial institutions, request banking credentials, or provide automatic account synchronization. Export availability, file layout, and account coverage depend on the provider, product, region, and reporting period.

**All amounts must be denominated in USD.** The application does not store exchange rates or maintain separate currency ledgers. A provider listed below may primarily offer accounts in another currency; that does not make those accounts importable. Do not relabel GBP, EUR, CAD, AUD, or another currency as USD.

## Choose an import profile

Choose the profile matching the downloaded file. The application has **11 selectable profiles: 7 provider-specific profiles, 2 standard-format profiles, and 2 normalized CSV profiles**. The 7 provider-specific profiles cover 6 CSV layouts and 1 PDF/text layout across 5 provider families. A `.csv` extension alone does not identify its columns or amount conventions.

| Profile | Accepted input | Main boundary |
| --- | --- | --- |
| Bank / Credit Card OFX or QFX | OFX 1.x SGML or OFX 2.x XML in an OFX/QFX file | One USD bank or credit-card statement with explicit account identity. See [OFX and QFX](#ofx-and-qfx). |
| Bank / Cash / Credit Card QIF (USD, US dates) | One `Bank`, `Cash`, or `CCard` transaction section | Verify USD currency and U.S./ISO dates; QIF does not declare currency. See [QIF](#qif). |
| Ally Checking CSV | Ally's `Date,Time,Amount,Type,Description` transaction layout | The `Time` column is required, although an individual time may be blank. |
| Apple Card CSV | Apple Card's transaction export, including `Amount (USD)` and `Purchased By` | This is an Apple Card profile; Apple Cash and purchases made through Apple Pay are different sources. |
| Chase / Amazon Credit Card CSV | The Chase-issued credit-card export with transaction/posting dates, category, type, amount, and memo | A Chase checking export or a store card issued by another institution is not this layout. |
| PayPal Activity Download CSV (balance impact) | The documented Activity Download CSV with balance-impact and transaction-status fields | Posted balance-affecting rows only; ambiguous posted activity is retained for classification. |
| PayPal Balance Statement CSV | The documented monthly/custom balance-statement CSV | Requires the statement's balance and transaction fields; headerless continuation files are not supported. |
| PayPal Account Statement PDF/Text | The supported account-activity layout in a text-readable PDF or extracted text | This layout is not a complete wallet balance ledger. See [PayPal PDF/text limitation](#paypal-pdftext-limitation). |
| Regions Bank Checking/Savings CSV | Regions' account, transaction/posting date, debit/credit, and long-description layout | Account identity must agree with the selected account. |
| Normalized Bank / Wallet / Card CSV | The [normalized CSV contract](#normalized-csv-contract) below | Prepare unsupported provider layouts explicitly; the importer does not infer arbitrary columns. |
| Normalized Credit Account CSV | Prepared credit-account activity using the same normalized columns | Additional type/sign validation for credit cards and other liability accounts. |

Provider CSV profiles accept their specified layouts, not every export ever produced under the same brand. Resolve changed headers, unsupported regional formats, and ambiguous signs before importing. Review any retained unclassified activity before relying on financial estimates.

## Import and verify an account

1. Create or select the account represented by the file. Keep checking, savings, cash wallets, and credit facilities separate. Select the correct account type before importing.
2. Download posted activity for a known period. Include every transaction type needed for that account's ledger, including transfers, repayments, refunds, interest, and fees. A spending-only report may omit balance movements.
3. Select the matching import profile and preview the file. Check account identity, dates, signs, currency, warnings, and parse errors against the source. A partly accepted file is not a complete statement.
4. Review duplicates and reconcile related entries across accounts. Retain the bank and credit-account sides of a repayment; link them instead of deleting a legitimate account entry. Equal amounts and nearby dates alone do not prove a duplicate or transfer.
5. Use **Reconcile Statement** with the source's opening and closing balances. Posting dates are used when available. Imported activity does not change saved account-balance snapshots.

Keep stable provider transaction IDs when converting data, and use the same import profile for overlapping downloads where possible. Switching formats can change source identifiers and require duplicate review. If an import has rows omitted because of errors, correct the source or prepare a verified normalized file before treating a balance check as complete.

## Provider export reference

The **40 provider/product reference entries** below distinguish export availability from application compatibility. Links point to official help or format documentation, reviewed in November 2026. Inclusion does not imply a native importer or the same export options for every account.

- **Native** means a dedicated profile listed above accepts the specified layout.
- **OFX/QFX** or **QIF** means use the corresponding standard profile if the file meets its account, currency, date, and record requirements. It does not mean institution-specific testing or online connectivity.
- **Normalize** means convert the provider's structured export to the normalized CSV contract. There is no dedicated parser for that provider's CSV.
- **Prepare** means obtain suitable structured data or prepare and verify normalized rows from statements. The cited source establishes statement/activity access, not a compatible transaction-file layout. General statement PDFs are not automatically parsed.

### U.S. banks and card issuers

| Institution or product | Official export or statement guidance | Application route |
| --- | --- | --- |
| Ally Bank | [Account information](https://www.ally.com/help/bank/account-information/): CSV and QFX activity downloads. | **Native** Ally CSV; **OFX/QFX** for an eligible QFX statement. |
| American Express | [Download transactions](https://www.americanexpress.com/us/customer-service/faq.download-export-transactions-software.html): CSV and financial-software downloads. | **Normalize** CSV; **OFX/QFX** only if that is the file actually offered. |
| Apple Card | [Export transactions](https://support.apple.com/en-us/102284); [format overview](https://support.apple.com/en-az/guide/apple-card-web-servicing/devb70bc6133/web) lists CSV, OFX, QFX, and QBO. | **Native** Apple Card CSV; **OFX/QFX** for eligible OFX/QFX. |
| Bank of America | [Financial management FAQ](https://www.bankofamerica.com/online-banking/online-financial-management-faqs/) describes transaction downloads in My Financial Picture without specifying the file layout. | **Prepare** or **Normalize** an available structured export, one account at a time. |
| BMO U.S. | [Digital banking FAQ](https://www.bmo.com/en-us/main/personal/digital-banking/faqs/): per-account CSV, Quicken, and QuickBooks exports. | **Normalize** CSV; **OFX/QFX** if an eligible QFX is offered. |
| Capital One | [Transaction history](https://www.capitalone.com/help-center/checking-savings/transaction-history/) and [online statements](https://www.capitalone.com/help-center/checking-savings/online-statements/) explain account access; these pages do not define a consumer CSV schema. | **Prepare** or **Normalize** an available structured export. |
| Chase | [Spending Planner export guidance](https://www.chase.com/personal/banking/education/budgeting-saving/what-is-chase-spending-planner) lists CSV, QFX, QIF, and QBO. | **Native** credit-card CSV; eligible **OFX/QFX** or **QIF**; **Normalize** other CSV layouts. |
| CIBC U.S. | [Digital banking instructions](https://us.cibc.com/en/digital-banking/how-to.html): CSV, QFX, and Excel transaction downloads. | **OFX/QFX** for eligible QFX; **Normalize** CSV/Excel. |
| Citi personal cards | [Paperless statements](https://www.cardbenefits.citi.com/Products/Paperless-Statements): saved current statements and PDF statement access. | **Prepare** or **Normalize** an available structured export; no native Citi CSV profile. |
| Columbia Bank | [Transaction export guidance](https://www.columbiabank.com/help-center/quicken-and-quickbooks/) lists CSV, OFX, QFX, QBO, and spreadsheet choices, depending on the banking platform. | **OFX/QFX** for eligible files; **Normalize** CSV/spreadsheet data. |
| Discover | [Account activity and statements](https://www.discover.com/credit-cards/card-smarts/access-account-with-mobile-apps/) describes transaction and statement access without defining a native CSV layout. | **Prepare** or **Normalize** an available structured export. |
| PNC business banking | [Online banking export information](https://www.pnc.com/en/small-business/banking/online-business-services/online-banking-and-bill-pay.html) lists spreadsheet and financial-software formats for business accounts. | **Normalize** structured exports; **OFX/QFX** only if an eligible file is offered. This source does not establish consumer-account export availability. |
| Regions Bank | [Web Connect download instructions](https://www.regions.com/help/online-banking-help/quickbooks/how-do-i-download-regions-bank-web-connect) explain choosing an account, period, and financial-software format. | **Native** for the Regions checking/savings CSV layout; **OFX/QFX** if an eligible QFX is offered. |
| SoFi Checking and Savings | [Export transaction instructions](https://support.sofi.com/hc/en-us/articles/12905767525773-Can-I-export-my-Checking-and-Savings-transactions): account-specific CSV download. | **Normalize** CSV. |
| U.S. Bank | [Download transaction history](https://www.usbank.com/customer-service/knowledge-base/KB0069323.html): CSV, QFX, and QBO. | **OFX/QFX** for eligible QFX; **Normalize** CSV. |
| Wells Fargo | [Account activity FAQ](https://www.wellsfargo.com/help/online-banking/activity-faqs/): comma-delimited activity and Quicken Web Connect downloads. | **OFX/QFX** if the download is eligible QFX; **Normalize** comma-delimited activity. |

### Credit unions

| Institution | Official export or statement guidance | Application route |
| --- | --- | --- |
| Alliant Credit Union | [Download account information](https://www.alliantcreditunion.org/help/how-do-i-download-my-alliant-account-information-to-my-desktop): CSV and financial-software choices. | **Normalize** CSV; **OFX/QFX** only if an eligible file is offered. |
| BECU | [Download transaction history](https://www.becu.org/support/download-your-transaction-history-from-online-banking): CSV and QFX; older Money Manager activity has a separate CSV path. | **OFX/QFX** for eligible QFX; **Normalize** either CSV layout. |
| Community 1st Credit Union | [Download transaction history](https://www.c1stcreditunion.com/personal/online-services/digital-banking-faq%27s/downloading-transaction-history-/): CSV, OFX, QFX, and other spreadsheet/software formats. | **OFX/QFX** for eligible files; **Normalize** CSV. |
| Navy Federal Credit Union | [Digital banking tutorials](https://www.navyfederal.org/services/mobile-online-banking/tutorials.html) explain accessing checking, savings, card, and loan statements. | **Prepare** or **Normalize** an available structured export; no native Navy Federal CSV profile. |
| Peak Credit Union | [Export transaction history](https://www.peakcu.org/help/export-or-download-transaction-history): CSV, OFX, and QFX among the available choices. | **OFX/QFX** for eligible files; **Normalize** CSV. |
| Space Coast Credit Union | [Online banking export instructions](https://www.sccu.com/personal/online-mobile-banking) describe downloading a CSV transaction list. | **Normalize** CSV. |

### International banks and digital banks

The currency restriction applies to every row here. Use these routes only for an actual USD-denominated account/export where the product offers one. A domestic-currency account cannot be imported by changing the currency label or dividing balances by an estimated exchange rate.

| Institution or product | Official export or statement guidance | Application route for eligible USD activity |
| --- | --- | --- |
| ANZ Australia | [Statements and transaction exports](https://www.anz.com.au/support/online-banking/spending-savings/statements/) describe Internet Banking transaction downloads, including CSV. | **Normalize** CSV. |
| Barclays U.K. | [Online statements](https://www.barclays.co.uk/ways-to-bank/online-banking/online-statements/) explains statement downloads; it does not establish a compatible CSV layout. | **Prepare** or **Normalize** an available structured export. |
| Commonwealth Bank of Australia | [NetBank format changes](https://www.commbank.com.au/digital-banking/new-netbank/feature-changes.html) retains CSV export and states that OFX/QIF export is no longer available. | **Normalize** CSV. |
| HSBC U.K. | [Online statements FAQ](https://www.hsbc.co.uk/help/banking-made-easy/online-statements/) distinguishes PDF statements from CSV/XLS transaction downloads. | **Normalize** CSV/XLS. |
| Lloyds business banking | [Statement export instructions](https://www.lloydsbank.com/business/help-and-support/account-management/view-your-bank-statements.html) describes CSV and QIF in Online for Business. | Eligible **QIF** or **Normalize** CSV. This source describes business banking. |
| Monzo | [Export transactions](https://monzo.com/help/budgeting-overdrafts-savings/exporting-my-transactions) lists PDF, CSV, and QIF statement exports. | Eligible **QIF** or **Normalize** CSV; do not import GBP rows as USD. |
| N26 | [Bank statements and activity reports](https://support.n26.com/en-eu/account-and-personal-details/bank-statements-and-confirmations/how-to-get-bank-statement-n26): activity reports can be CSV; monthly balance statements are PDF. | **Normalize** only if actual USD activity is available; ordinary non-USD records are unsupported. |
| National Australia Bank | [Transaction history](https://www.nab.com.au/personal/online-banking/nab-internet-banking/transaction-history) lists spreadsheet, QIF, and PDF exports. | Eligible **QIF** or **Normalize** spreadsheet data. |
| NatWest | [Transaction search](https://www.natwest.com/support-centre/help-with-your-card/transactions/how-do-i-search-for-transactions-online.html) describes spreadsheet/PDF downloads; [business guidance](https://www.natwest.com/business/frequently-asked-questions/general-banking-information/general/how-can-i-get-help-with-ebanking-for-business.html) also lists OFX and CSV. | **Normalize** structured data; **OFX/QFX** only for an eligible OFX file actually offered by the account. |
| Revolut | [Personal currency statements](https://help.revolut.com/en-US/help/profile-and-plan/managing-my-account/account-statement-per-chosen-currency/) offers PDF/Excel; [business transaction statements](https://help.revolut.com/en-US/business/help/managing-my-business/viewing-my-account-statements/finding-my-account-statement/) offers CSV/PDF. | **Normalize** the USD account's spreadsheet/CSV; no native Revolut parser. |
| Starling Bank | [Statement and feed exports](https://help.starlingbank.com/personal/topics/account-management/how-do-i-receive-my-statements/) distinguishes monthly PDFs from CSV transaction summaries. | **Normalize** eligible activity; a CSV transaction summary is not the same document as a monthly statement. |
| TD Canada Trust | [EasyWeb spreadsheet downloads](https://td.intelliresponse.com/easyweb/index.jsp?id=351&question=How+do+I+download+my+accounts+to+a+spreadsheet&requestType=NormalRequest) describes CSV export. | **Normalize** activity from an eligible USD account; CAD activity is unsupported. |
| Westpac Australia | [Export transaction lists](https://www.westpac.com.au/business-banking/online-banking/support-faqs/export-files/) lists CSV and OFX and states that the steps also apply to personal customers. | **OFX/QFX** for eligible single-account USD OFX; otherwise **Normalize** eligible CSV. |

### Wallets and payment services

| Provider or product | Official export or statement guidance | Application route |
| --- | --- | --- |
| Apple Cash | [Transaction statement instructions](https://support.apple.com/en-us/105009) provides a PDF activity statement. | **Prepare** normalized activity; the Apple Card CSV profile does not parse Apple Cash PDFs. |
| Cash App | [Account-statement instructions](https://cash.app/learn/getting-started/is-cash-app-a-bank) explains monthly statement access without specifying a compatible CSV schema. | **Prepare** or **Normalize** an available structured cash-activity export; investment/crypto records are outside the import model. |
| PayPal | [Activity Download schema](https://developer.paypal.com/reports/activity-download/) and [monthly/custom statement schema](https://developer.paypal.com/reports/monthly-custom-statements/) describe distinct CSV reports. | **Native** for the matching PayPal CSV profile; supported PDF/text activity has the limitation below. |
| Venmo | [Transaction history](https://help.venmo.com/cs/articles/transaction-history-vhel281) describes downloadable CSV statements. | **Normalize** CSV, preserving the distinction between wallet activity and its external funding. |
| Wise | [Currency-account statements](https://wise.com/help/articles/2736049/how-do-i-download-a-statement) offers CSV/QIF and other formats; [transaction history](https://wise.com/help/articles/2489458/how-do-i-download-my-transaction-history) is a different report. | Eligible **QIF** or **Normalize** the USD account statement. Preserve fees and transfers; no native Wise CSV parser. |

For an unlisted institution, use eligible OFX/QFX/QIF or normalized USD CSV. If only PDF statements are available, prepare verified transaction rows or enter them manually. Renaming a PDF, XLSX, QBO, or another file does not convert its contents.

## OFX and QFX

The standard profile reads OFX 1.x SGML and OFX 2.x XML bank/credit-card statements. It accepts one account statement per file, explicit `USD` currency, and supported bank account types `CHECKING`, `SAVINGS`, or `MONEYMRKT`, or a credit-card statement. Its account identifier must match the selected account.

Each transaction must have a provider `FITID` within the supported 100-character limit. The signed `TRNAMT` is preserved. `DTPOSTED` supplies the posting date; `DTUSER`, when present, supplies the transaction date. Source calendar dates are preserved without conversion to the computer's time zone. Files are limited to 16 MiB.

Investment statements, multiple-account files, pending transaction sections, and correction/reversal-reference records are rejected instead of being partly interpreted. A posted opposite-signed transaction is different from an instruction to replace an earlier transaction. Resolve correction records against the provider's ledger before preparing another import. The profile does not update saved balances from an OFX statement balance.

Already-converted USD amounts may retain original-currency metadata; the application does not convert them again. A foreign-currency amount override is rejected. Posted credits or adjustments with unclear purpose are retained as unclassified activity with a warning. Confirm their treatment before relying on financial estimates.

## QIF

The QIF profile accepts UTF-8/ASCII files with one `!Type:Bank`, `!Type:Cash`, or `!Type:CCard` transaction section. An optional single `!Account` header must name the selected account and have a compatible type. Investment data, multiple accounts, category lists, and memorized-transaction lists are unsupported.

Dates must use `M/d/yyyy` or `yyyy-MM-dd`, with four-digit years. Amounts use exact signed cents. **QIF does not declare currency:** the profile assumes USD and displays a warning requiring you to verify the source currency and date convention. A U.K. `03/04/2026` date cannot be interpreted safely as a U.S. date without checking the export convention.

Split amounts must equal the transaction total. A validated split is retained as one account movement, with its breakdown in `Memo`; it does not create separate category allocations. Mixed transfer/expense splits and ambiguous positive credits require classification. Preview the warning and review these entries in Transactions before relying on totals.

QIF does not supply globally unique provider transaction IDs. Reference numbers, memos, and split information contribute to repeat-import matching. Similar entries in overlapping exports can require **Duplicate Review**; a generated identity alone never proves that two QIF entries are different purchases.

Recognized `Opening Balance` entries are omitted with a warning. Enter the source balance when reconciling the statement or saving a dated account balance; an opening-balance initializer is not a transaction during the period.

## Provider-specific boundaries

### Native CSV headers

These profiles expect the following header names. A column can be required even when some of its cells are empty.

| Profile | Expected header columns |
| --- | --- |
| Ally | `Date`, `Time`, `Amount`, `Type`, `Description` |
| Apple Card | `Transaction Date`, `Clearing Date`, `Description`, `Merchant`, `Category`, `Type`, `Amount (USD)`, `Purchased By` |
| Chase credit card | `Transaction Date`, `Post Date`, `Description`, `Category`, `Type`, `Amount`, `Memo` |
| Regions | `Account`, `Transaction Date`, `Posted Date`, `No.`, `Description`, `Debit`, `Credit`, `Long Description` |
| PayPal Activity | `Date`, `Time`, `TimeZone`, `Name`, `Type`, `Status`, `Currency`, `Gross`, `Fee`, `Net`, `Transaction ID`, `Balance Impact` |
| PayPal Balance Statement | `Date`, `Time`, `Time Zone`, `Description`, `Currency`, `Gross`, `Fee`, `Net`, `Balance`, `Transaction ID`, `Name` |

The Ally profile uses ISO dates (`yyyy-MM-dd`); the other listed native CSV profiles use U.S. dates (`MM/dd/yyyy`). Native sign conventions are handled by each parser. In particular, do not invert Apple Card amounts before using its native profile. When converting to normalized CSV, follow the normalized sign contract instead.

### PayPal CSV reports

Select the profile for the report you actually exported. The [Activity Download report](https://developer.paypal.com/reports/activity-download/) and [monthly/custom balance statement](https://developer.paypal.com/reports/monthly-custom-statements/) are different schemas.

The activity profile requires balance-impact information, imports posted debit/credit activity, and reports skipped memo or recognized non-posted activity. Posted reversed originals and their separate refunds are retained. The balance-statement profile reads posted balance-affecting activity with the statement's balance field. Both require the supported English/U.S. layout and USD amounts; headerless continuation files are unsupported.

Ambiguous posted events retain their signed amounts as `Unclassified Credit` or `Unclassified Debit`, accompanied by a classification warning. Review whether they represent income, an income reversal, a refund, a fee, or a transfer. Unknown statuses, invalid currency or amounts, inconsistent gross/fee/net totals, and conflicting source IDs remain errors; they are not silently converted into valid activity.

A fee-bearing event is represented by its gross component and a separately identified signed fee component; together they equal the source net amount. Do not import the fee or net again as additional activity. Bank/card funding and withdrawals remain transfers, while supported buyer refunds retain refund treatment. Counterparty emails and bank details do not identify the owning wallet or prove a purchase-funding match. Verify the selected account and confirm related entries through Transaction Links.

These CSV profiles do not have the PDF profile's incomplete-ledger restriction. They still require a complete reporting period and resolution of every omitted or invalid record before statement reconciliation is meaningful. PayPal's [event-code reference](https://developer.paypal.com/reports/reference/t-codes/) explains why a payment code must be interpreted with its debit/credit direction.

### PayPal PDF/text limitation

The PDF/text profile imports the supported **account activity** section, which is not a proven complete wallet balance ledger. Direct bank/card-funded purchases may appear without offsetting wallet funding credits. Wallet balance checks therefore refuse periods containing this profile.

Reconcile the underlying funding accounts, or import a complete wallet balance ledger into a separate account using a supported balance-statement CSV or prepared normalized CSV. Reconcile duplicate purchase representations explicitly; do not add the same purchases to household spending twice.

The parser keeps each transaction within its own record. Detached funding details that cannot be associated unambiguously produce a warning rather than a guessed link. Unsupported or ambiguous positive transaction types produce errors rather than being classified as income or refunds. Scanned images and arbitrary PDF layouts are not supported by this profile.

## Normalized CSV contract

Choose **Normalized Bank / Wallet / Card CSV** when preparing data from an unsupported layout. **Normalized Credit Account CSV** uses the same columns with additional credit-account type checks.

Required headers are `Date,Amount,Type,Description`. Optional headers are `Account,Transaction ID,Posting Date,Time,Currency,Funding Source,Category,Category Type,Merchant,Memo`.

```csv
Date,Amount,Type,Description,Account,Transaction ID,Currency,Funding Source
2026-01-02,-60.00,Purchase,Sample merchant A,Example wallet,order-a,USD,Example checking 1234
2026-01-06,-40.00,Purchase,Sample merchant B,Example wallet,order-b,USD,Example checking 1234
2026-01-09,10.00,Refund,Partial return from merchant A,Example wallet,refund-a,USD,
```

Import those rows into the account named `Example wallet`. Import its funding account's entries in a separate file and account, then link the retained records. The names and references above are synthetic examples.

| Field or value | Requirement |
| --- | --- |
| `Date`, `Posting Date` | `yyyy-MM-dd` or U.S. `MM/dd/yyyy`. Use ISO dates when converting an international export to avoid day/month ambiguity. |
| `Amount` | Signed USD with no more than two decimal places. No currency conversion or fractional-cent rounding is performed. |
| Purchases, fees, cash withdrawals | Negative amounts, with a corresponding type such as `Purchase`, `Fee`, or `Withdrawal`. |
| Deposits, refunds, credit-account payments | Positive amounts; use `Deposit`, `Refund`, or `Payment` according to the actual event. A payment received by a liability account is not earned income. |
| Bank-to-credit-account repayment | Negative in checking; positive `Payment` in the credit account. Retain and reconcile both records. |
| `Currency` | `USD` if supplied. A blank column or omitted column assumes USD; verify the source currency yourself. |
| `Account` | The selected account's nickname, account number, or supported explicit last-four identity. A conflicting identity rejects the import. |
| `Transaction ID` | Keep a stable provider reference when available. Conflicting values for the same ID require review. Do not generate a new random ID each time you export the same transaction. |
| `Time` | `HH:mm:ss` or `HH:mm`, if supplied. |
| `Funding Source` | Preserve an explicit funding-account reference when the source provides one. An account reference alone does not prove which debit funded a purchase. |
| `Category`, `Category Type`, `Merchant`, `Memo` | Optional source information. Rules and manual classification can refine the imported treatment. |

Quote fields containing commas, quotation marks, or newlines using normal CSV quoting. Preserve all rows needed for the selected account's ledger, including separate fees and transfers. Review source totals and account balances after conversion; do not change a refund or repayment into income merely to force a matching total.
