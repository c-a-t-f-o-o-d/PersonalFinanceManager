using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Core.Planning;
using PersonalFinanceManager.Core.Imports;

namespace PersonalFinanceManager.Core.Reporting;

public sealed class FinancialReportCalculator
{
    private readonly ReportData _data;
    private readonly Dictionary<long, ReportTransaction> _transactions;
    private readonly ILookup<long, TransactionLinkRecord> _sourceLinks;
    private readonly ILookup<long, TransactionLinkRecord> _targetLinks;
    private readonly List<(long SourceId, long LinkedId)> _relationshipsNeedingReview = new();

    public FinancialReportCalculator(ReportData data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _transactions = data.Transactions.ToDictionary(transaction => transaction.Id);
        TransactionLinkRecord[] validLinks = ValidateStoredRelationships(data.Links);
        _sourceLinks = validLinks.ToLookup(link => link.SourceTransactionId);
        _targetLinks = validLinks.ToLookup(link => link.LinkedTransactionId);
    }

    public IReadOnlyList<ExpenseDetailItem> NetExpenses()
    {
        return ExpenseRows(_data.Transactions).ToArray();
    }

    public bool NeedsLinkReview(long transactionId)
    {
        return _relationshipsNeedingReview.Any(link => link.SourceId == transactionId || link.LinkedId == transactionId);
    }

    public bool NeedsClassificationReview(long transactionId)
    {
        return _transactions.TryGetValue(transactionId, out ReportTransaction? row) &&
            ImportClassification.RequiresReview(row.Type) &&
            !row.IsIncome &&
            !IsTransfer(row) &&
            !Ignored(row) &&
            !LinkedTransfer(row.Id) &&
            !_targetLinks[row.Id].Any(IsMirror) &&
            !(row.Amount > 0M && _sourceLinks[row.Id].Where(link => IsType(link, TransactionLinkTypes.PurchaseRefund)).Sum(link => link.AllocationAmount ?? 0M) == row.Amount);
    }

    public ExpenseReportResult Expenses(ExpenseReportFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ReportTransaction[] selected = Select(filter.AccountId, filter.FromDate, filter.ToDate);
        ReportPeriod period = ReportPeriod.Create(filter.FromDate, filter.ToDate, selected.Select(transaction => transaction.Date));
        ExpenseDetailItem[] details = ExpenseRows(selected).ToArray();
        decimal total = details.Sum(item => item.Amount);

        ExpenseSummaryItem[] summary = details.GroupBy(item => Label(string.Equals(filter.BreakdownMode, ExpenseBreakdownModes.CategoryType, StringComparison.OrdinalIgnoreCase) ? item.SpendingClass : item.Category), StringComparer.OrdinalIgnoreCase)
            .Select(group => new ExpenseSummaryItem
            {
                GroupName = group.Key,
                TotalAmount = group.Sum(item => item.Amount),
                TransactionCount = group.Select(item => item.ImportedTransactionId).Distinct().Count()
            })
            .OrderByDescending(item => item.TotalAmount).ThenBy(item => item.GroupName).ToArray();

        foreach (ExpenseSummaryItem item in summary)
        {
            item.PercentOfTotal = total > 0M ? Round(item.TotalAmount / total * 100M) : 0M;
        }

        return new ExpenseReportResult
        {
            Period = period,
            Warnings = RelationshipWarnings(selected),
            Metrics = new ExpenseMetricSummary
            {
                TotalExpenses = total,
                TransactionCount = details.Select(item => item.ImportedTransactionId).Distinct().Count(),
                FirstTransactionDate = details.Length == 0 ? null : details.Min(item => item.TransactionDate),
                LastTransactionDate = details.Length == 0 ? null : details.Max(item => item.TransactionDate)
            },
            Summary = summary,
            Monthly = ExpenseMonthly(period, details),
            Details = details.OrderByDescending(item => item.TransactionDate).ThenByDescending(item => item.ImportedTransactionId).Take(DetailLimit(filter.MaxDetailRows)).ToArray()
        };
    }

    public CashFlowReportResult CashFlow(ExpenseReportFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ReportTransaction[] selected = Select(filter.AccountId, filter.FromDate, filter.ToDate).Where(transaction => AccountClassification.IsCash(transaction.AccountType)).ToArray();
        ReportPeriod period = ReportPeriod.Create(filter.FromDate, filter.ToDate, selected.Select(transaction => transaction.Date));

        var rows = selected.Where(transaction => IncludeCash(transaction, filter.AccountId.HasValue))
            .Select(transaction => (Transaction: transaction, Amount: CashAmount(transaction)))
            .Where(row => row.Amount != 0M).ToArray();

        return new CashFlowReportResult
        {
            Period = period,
            Warnings = RelationshipWarnings(selected),
            Summary = new MoneyInOutSummary
            {
                TotalMoneyIn = rows.Where(item => item.Amount > 0).Sum(item => item.Amount),
                TotalMoneyOut = -rows.Where(item => item.Amount < 0).Sum(item => item.Amount),
                TransactionCount = rows.Length,
                FirstTransactionDate = rows.Length == 0 ? null : rows.Min(item => item.Transaction.Date),
                LastTransactionDate = rows.Length == 0 ? null : rows.Max(item => item.Transaction.Date)
            },
            Monthly = period.Months().Select(month =>
            {
                var monthRows = rows.Where(row => row.Transaction.Date.Year == month.Year && row.Transaction.Date.Month == month.Month).ToArray();
                return new MoneyInOutMonthlyItem
                {
                    MonthStart = month,
                    MoneyIn = monthRows.Where(row => row.Amount > 0M).Sum(row => row.Amount),
                    MoneyOut = -monthRows.Where(row => row.Amount < 0M).Sum(row => row.Amount),
                    TransactionCount = monthRows.Length
                };
            }).ToArray(),
            Details = rows.OrderByDescending(item => item.Transaction.Date).ThenByDescending(item => item.Transaction.Id).Take(DetailLimit(filter.MaxDetailRows))
                .Select(item => new MoneyInOutDetailItem
                {
                    ImportedTransactionId = item.Transaction.Id,
                    AccountNickname = item.Transaction.AccountNickname,
                    TransactionDate = item.Transaction.Date,
                    Amount = item.Amount,
                    Type = item.Transaction.Type,
                    Description = item.Transaction.Description,
                    Category = item.Transaction.Category,
                    SpendingClass = item.Transaction.SpendingClass,
                    Merchant = item.Transaction.Merchant
                }).ToArray()
        };
    }

    public IReadOnlyList<IncomeOwnerSummary> Income(SalarySpendingFilter filter)
    {
        ReportTransaction[] selected = Select(filter.AccountId, filter.FromDate, filter.ToDate);
        ReportPeriod period = ReportPeriod.Create(filter.FromDate, filter.ToDate, selected.Select(transaction => transaction.Date));

        return selected.Where(transaction => transaction.IsIncome &&
            !Ignored(transaction) && !IsTransfer(transaction) &&
            !LinkedTransfer(transaction.Id) &&
            !_targetLinks[transaction.Id].Any(IsMirror))
            .GroupBy(transaction => OwnerValues.Normalize(string.IsNullOrWhiteSpace(transaction.IncomeOwner) ? transaction.AccountOwner : transaction.IncomeOwner) ?? OwnerValues.Unassigned, StringComparer.OrdinalIgnoreCase)
            .Select(group => new IncomeOwnerSummary
            {
                IncomeOwner = group.Key,
                TotalNetIncome = group.Sum(IncomeAmount),
                TransactionCount = group.Count(),
                PeriodMonthCount = period.MonthCount,
                MonthlyAverageNetIncome = MonthlyAverage(group.Sum(IncomeAmount), period),
                AnnualizedNetIncome = MonthlyAverage(group.Sum(IncomeAmount), period) * 12M
            }).OrderByDescending(item => item.TotalNetIncome).ToArray();
    }

    public IReadOnlyList<SalarySpendingScopeOption> SpendingOptions(SalarySpendingFilter filter)
    {
        ReportTransaction[] selected = Select(filter.AccountId, filter.FromDate, filter.ToDate);
        ReportPeriod period = ReportPeriod.Create(filter.FromDate, filter.ToDate, selected.Select(transaction => transaction.Date));

        return ExpenseRows(selected)
            .GroupBy(item => (Category: Label(item.Category), SpendingClass: Label(item.SpendingClass)), SpendingBucketComparer.Instance)
            .Select(group => new SalarySpendingScopeOption
            {
                Category = group.Key.Category,
                SpendingClass = group.Key.SpendingClass,
                TotalAmount = group.Sum(item => item.Amount),
                MonthlyAverage = MonthlyAverage(group.Sum(item => item.Amount), period),
                PeriodMonthCount = period.MonthCount,
                ActiveMonthCount = group.Select(item => (item.TransactionDate.Year, item.TransactionDate.Month)).Distinct().Count(),
                TransactionCount = group.Select(item => item.ImportedTransactionId).Distinct().Count()
            }).OrderByDescending(item => item.TotalAmount).ToArray();
    }

    public SalarySpendingAnalysis SalarySpending(SalarySpendingFilter filter)
    {
        ReportTransaction[] selected = Select(filter.AccountId, filter.FromDate, filter.ToDate);
        ReportPeriod period = ReportPeriod.Create(filter.FromDate, filter.ToDate, selected.Select(transaction => transaction.Date));
        bool useBills = filter.SpendingMode is SalarySpendingMode.RequiredBillsOnly or SalarySpendingMode.RequiredBillsPlusSelectedCategories;
        bool hasSelectedBuckets = filter.SelectedSpendingBuckets.Count + filter.SelectedCategories.Count + filter.SelectedCategoryTypes.Count > 0;

        bool useHistory = filter.SpendingMode is SalarySpendingMode.AllExpenseTrend or SalarySpendingMode.SelectedCategoriesOnly ||
            (filter.SpendingMode == SalarySpendingMode.RequiredBillsPlusSelectedCategories && hasSelectedBuckets);

        RecurringBill[] bills = useBills ? _data.Bills.Where(bill => BillSchedule.IsRequiredExpense(bill) && (!filter.AccountId.HasValue || bill.AccountId == filter.AccountId)).ToArray() : [];
        ExpenseDetailItem[] expenses = useHistory ? ExpenseRows(selected).Where(item => SelectedBucket(item, filter)).ToArray() : [];
        bool hasHistoryForBasis = !useHistory || expenses.Length > 0;
        IReadOnlyList<string> relationshipWarnings = RelationshipWarnings(selected);
        List<string> warnings = [period.CoverageNote, .. relationshipWarnings];

        if (!hasHistoryForBasis)
        {
            warnings.Add("No expense history exists for the chosen spending basis. Select spending buckets or import the corresponding statements.");
        }

        if (!useHistory && bills.Length == 0)
        {
            warnings.Add("No active required bills define this spending basis. Add the required bills before estimating salary.");
        }

        IReadOnlyList<string> billIdentityWarnings = RecurringBillMatcher.OverlapWarnings(bills);
        warnings.AddRange(billIdentityWarnings);
        bool unresolvedOverlap = billIdentityWarnings.Count > 0;

        if (useBills && useHistory)
        {
            foreach (RecurringBill bill in bills)
            {
                if (expenses.Any(item => RecurringBillMatcher.MatchesStatementDescription(item, bill) && !SameBucket(item, bill)))
                {
                    unresolvedOverlap = true;
                    warnings.Add($"Review classification for '{bill.BillName}': an exact payment description has a different category or category type. Correct its classification before combining history with this bill.");
                }

                ExpenseDetailItem[] bucket = expenses.Where(item => SameBucket(item, bill)).ToArray();

                if (bucket.Length > 0 && !bucket.Any(item => MatchesBill(item, bill)))
                {
                    unresolvedOverlap = true;
                    warnings.Add($"Review overlap for '{bill.BillName}': its spending bucket is selected, but no exact payee/source match identifies the historical bill. Deselect that bucket or correct the bill's payee/source description.");
                }
            }

            if (expenses.Any(item => bills.Count(bill => MatchesBill(item, bill)) > 1))
            {
                unresolvedOverlap = true;
                warnings.Add("A historical payment matches more than one required bill. Resolve the overlapping identities before estimating salary.");
            }

            expenses = expenses.Where(item => !bills.Any(bill => MatchesBill(item, bill))).ToArray();
        }

        List<SalarySpendingInputLine> lines = expenses
            .GroupBy(item => (Category: Label(item.Category), SpendingClass: Label(item.SpendingClass)), SpendingBucketComparer.Instance)
            .Select(group => new SalarySpendingInputLine
            {
                Source = "Transactions",
                Name = group.Key.Category + " / " + group.Key.SpendingClass,
                Category = group.Key.Category,
                SpendingClass = group.Key.SpendingClass,
                Count = group.Select(item => item.ImportedTransactionId).Distinct().Count(),
                MonthlyAmount = MonthlyAverage(group.Sum(item => item.Amount), period),
                AnnualizedAmount = MonthlyAverage(group.Sum(item => item.Amount), period) * 12M,
                Notes = useBills ? "Net expenses; historical payments represented by required bills are excluded." : "Net expenses after refunds over the selected observation period."
            }).ToList();

        decimal transactionMonthly = lines.Sum(item => item.MonthlyAmount);
        decimal billMonthly = 0M;
        bool invalidBills = false;

        foreach (RecurringBill bill in bills)
        {
            try
            {
                if (bill.TypicalAmount <= 0M)
                {
                    throw new ArgumentException("A positive amount is required.");
                }

                decimal amount = BillSchedule.MonthlyEquivalent(bill.TypicalAmount, bill.Frequency);
                billMonthly += amount;

                lines.Add(new SalarySpendingInputLine
                {
                    Source = "Required Bill",
                    Name = bill.BillName,
                    Category = bill.Category,
                    SpendingClass = bill.SpendingClass,
                    MonthlyAmount = amount,
                    AnnualizedAmount = amount * 12M,
                    Count = 1,
                    Notes = $"{bill.Frequency}; replaces exactly matched historical bill transactions."
                });
            }
            catch (ArgumentException)
            {
                invalidBills = true;
                warnings.Add($"'{bill.BillName}' needs a positive amount and a supported frequency.");
            }
        }

        return new SalarySpendingAnalysis
        {
            SpendingMode = filter.SpendingMode,
            Period = period,
            CanEstimate = !unresolvedOverlap && !invalidBills && relationshipWarnings.Count == 0 && hasHistoryForBasis && (useHistory || bills.Length > 0),
            Warnings = warnings,
            IncomeOwnerSummaries = Income(filter),
            TargetMonthlySpending = Math.Max(0M, transactionMonthly + billMonthly),
            TransactionMonthlySpending = transactionMonthly,
            RequiredBillsMonthlySpending = billMonthly,
            InputLines = lines,
            MonthlyTrend = ExpenseMonthly(period, expenses),
            ActiveMonthCount = expenses.Select(item => (item.TransactionDate.Year, item.TransactionDate.Month)).Distinct().Count(),
            FirstTransactionDate = period.FromDate,
            LastTransactionDate = period.ToDate
        };
    }

    private ReportTransaction[] Select(int? accountId, DateOnly? fromDate, DateOnly? toDate)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
            throw new ArgumentException("The start date must be on or before the end date.");
        return _data.Transactions.Where(transaction => (!accountId.HasValue || transaction.AccountId == accountId)
            && (!fromDate.HasValue || transaction.Date >= fromDate) && (!toDate.HasValue || transaction.Date <= toDate)).ToArray();
    }

    private TransactionLinkRecord[] ValidateStoredRelationships(IReadOnlyList<TransactionLinkRecord> storedLinks)
    {
        FinancialLink[] relationships = storedLinks
            .Select((link, index) => new FinancialLink(
                link.TransactionLinkId > 0 ? link.TransactionLinkId : -index - 1,
                link.SourceTransactionId,
                link.LinkedTransactionId,
                TransactionLinkTypes.Normalize(link.LinkType),
                link.AllocationAmount))
            .ToArray();

        IReadOnlyDictionary<long, FinancialLinkTransaction> validationTransactions = _transactions.Values.Select(ForLinkValidation).ToDictionary(t => t.Id);
        List<TransactionLinkRecord> valid = new();

        for (int index = 0; index < storedLinks.Count; index++)
        {
            TransactionLinkRecord link = storedLinks[index];
            FinancialLink relationship = relationships[index];

            if (relationship.Type == TransactionLinkTypes.ManualReview)
            {
                if (link.Notes?.StartsWith("Review required after upgrade:", StringComparison.OrdinalIgnoreCase) == true)
                {
                    _relationshipsNeedingReview.Add((link.SourceTransactionId, link.LinkedTransactionId));
                }

                continue;
            }

            if (!_transactions.TryGetValue(link.SourceTransactionId, out ReportTransaction? source) ||
                !_transactions.TryGetValue(link.LinkedTransactionId, out ReportTransaction? linked))
            {
                _relationshipsNeedingReview.Add((link.SourceTransactionId, link.LinkedTransactionId));
                continue;
            }
            try
            {
                FinancialLinkRules.Validate(new TransactionLinkEditModel
                {
                    TransactionLinkId = relationship.Id,
                    SourceTransactionId = relationship.SourceId,
                    LinkedTransactionId = relationship.LinkedId,
                    LinkType = relationship.Type,
                    AllocationAmount = relationship.AllocationAmount,
                    ConfidenceScore = link.ConfidenceScore
                },
                ForLinkValidation(source), ForLinkValidation(linked), relationships, validationTransactions);
                valid.Add(link);
            }
            catch (InvalidOperationException)
            {
                _relationshipsNeedingReview.Add((link.SourceTransactionId, link.LinkedTransactionId));
            }
        }
        return valid.ToArray();
    }

    private static FinancialLinkTransaction ForLinkValidation(ReportTransaction transaction)
    {
        return new(transaction.Id, transaction.AccountId,
        transaction.AccountType, transaction.AccountNickname, string.Empty, null, transaction.Date, transaction.Amount,
        transaction.Type, transaction.Description, null, null, transaction.Category, transaction.IsTransfer);
    }

    private IReadOnlyList<string> RelationshipWarnings(IEnumerable<ReportTransaction> selected)
    {
        HashSet<long> ids = selected.Select(transaction => transaction.Id).ToHashSet();
        int count = _relationshipsNeedingReview.Count(link => ids.Contains(link.SourceId) || ids.Contains(link.LinkedId));
        List<string> warnings = new();

        if (count > 0)
        {
            warnings.Add($"{count} transaction relationship(s) require review. Invalid or unverified links do not hide ledger entries; totals may include unresolved purchase/funding representations. Review Transaction Links and Data Health before relying on them.");
        }

        int unclassified = ids.Count(NeedsClassificationReview);

        if (unclassified > 0)
        {
            warnings.Add($"{unclassified} imported transaction(s) require classification. Unclassified credits remain in account activity but are excluded from income and refunds; unclassified debits remain visible in spending. Review Transactions and Data Health before relying on totals.");
        }

        return warnings;
    }

    private IEnumerable<ExpenseDetailItem> ExpenseRows(IEnumerable<ReportTransaction> transactions)
    {
        foreach (ReportTransaction transaction in transactions)
        {
            if (Ignored(transaction) || IsTransfer(transaction) || LinkedTransfer(transaction.Id) || _targetLinks[transaction.Id].Any(IsMirror))
            {
                continue;
            }

            decimal refundAllocated = 0M;

            if (transaction.Amount > 0M)
            {
                foreach (TransactionLinkRecord refund in _sourceLinks[transaction.Id].Where(link => IsType(link, TransactionLinkTypes.PurchaseRefund)))
                {
                    if (!_transactions.TryGetValue(refund.LinkedTransactionId, out ReportTransaction? purchase))
                    {
                        continue;
                    }

                    decimal allocated = refund.AllocationAmount ?? 0M;
                    refundAllocated += allocated;
                    yield return ExpenseLine(transaction, -allocated, purchase.Category, purchase.SpendingClass);
                }
            }

            if (transaction.IsIncome || (transaction.Amount > 0M && NeedsClassificationReview(transaction.Id)))
            {
                continue;
            }

            decimal amount = -transaction.Amount + refundAllocated;

            if (transaction.Amount < 0M)
            {
                amount -= _targetLinks[transaction.Id].Where(link => IsType(link, TransactionLinkTypes.PurchaseFundingAllocation)).Sum(link => link.AllocationAmount ?? 0M);
            }

            if (amount != 0M)
            {
                yield return ExpenseLine(transaction, amount, transaction.Category, transaction.SpendingClass);
            }
        }
    }

    private static ExpenseDetailItem ExpenseLine(ReportTransaction transaction, decimal amount, string? category, string? spendingClass) => new()
    {
        ImportedTransactionId = transaction.Id,
        AccountId = transaction.AccountId,
        AccountNickname = transaction.AccountNickname,
        TransactionDate = transaction.Date,
        Amount = amount,
        Type = transaction.Type,
        Description = transaction.Description,
        Category = category,
        SpendingClass = spendingClass,
        Merchant = transaction.Merchant
    };

    private decimal IncomeAmount(ReportTransaction transaction)
    {
        return transaction.Amount - _sourceLinks[transaction.Id].Where(link => IsType(link, TransactionLinkTypes.PurchaseRefund)).Sum(link => link.AllocationAmount ?? 0M);
    }

    private decimal CashAmount(ReportTransaction transaction)
    {
        if (!AccountClassification.IsWallet(transaction.AccountType))
        {
            return transaction.Amount;
        }

        if (_sourceLinks[transaction.Id].Any(IsMirror))
        {
            return 0M;
        }

        return transaction.Amount + _sourceLinks[transaction.Id]
            .Where(link => IsType(link, TransactionLinkTypes.PurchaseFundingAllocation))
            .Sum(link => link.AllocationAmount ?? 0M);
    }

    private bool IncludeCash(ReportTransaction transaction, bool accountScoped)
    {
        if (Ignored(transaction))
        {
            return false;
        }

        if (accountScoped)
        {
            return true;
        }

        return !_sourceLinks[transaction.Id].Concat(_targetLinks[transaction.Id]).Any(link => IsBankTransfer(link) &&
            _transactions.TryGetValue(link.SourceTransactionId, out ReportTransaction? source) && AccountClassification.IsCash(source.AccountType) &&
            _transactions.TryGetValue(link.LinkedTransactionId, out ReportTransaction? linked) && AccountClassification.IsCash(linked.AccountType));
    }

    private bool LinkedTransfer(long id)
    {
        return _sourceLinks[id].Concat(_targetLinks[id]).Any(link => IsBankTransfer(link) || IsType(link, "CreditCardPaymentFunding")) ||
            _sourceLinks[id].Any(link => IsType(link, "CreditCardPaymentAllocation"));
    }

    private static bool IsTransfer(ReportTransaction transaction)
    {
        return (transaction.IsTransfer && (!transaction.LegacyLinkTreatment || transaction.ClassificationIsManual)) ||
            (AccountClassification.IsLiability(transaction.AccountType) && (Equal(transaction.Type, "Payment") || Equal(transaction.Type, "Buyer Credit Payment")));
    }

    private static bool IsBankTransfer(TransactionLinkRecord link)
    {
        return IsType(link, "BankTransfer") || IsType(link, "AccountTransfer");
    }

    private static bool IsMirror(TransactionLinkRecord link)
    {
        return IsType(link, "PurchaseMirror") || IsType(link, "PayPalFunding");
    }

    private static bool IsType(TransactionLinkRecord link, string type)
    {
        return TransactionLinkTypes.Normalize(link.LinkType) == TransactionLinkTypes.Normalize(type);
    }

    private static bool Ignored(ReportTransaction item)
    {
        return Equal(item.Category, "Ignore") || Equal(item.SpendingClass, "Ignore");
    }

    private static string Label(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(Uncategorized)" : value.Trim();
    }

    private static bool Equal(string? left, string? right)
    {
        return string.Equals(left?.Trim() ?? string.Empty, right?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static int DetailLimit(int value)
    {
        return Math.Clamp(value <= 0 ? 1000 : value, 1, 10000);
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal MonthlyAverage(decimal total, ReportPeriod period)
    {
        return period.MonthEquivalent > 0M ? Round(total / period.MonthEquivalent) : 0M;
    }

    private static bool SameBucket(ExpenseDetailItem item, RecurringBill bill)
    {
        return RecurringBillMatcher.SameBucket(item, bill);
    }

    private static bool MatchesBill(ExpenseDetailItem item, RecurringBill bill)
    {
        return RecurringBillMatcher.Matches(item, bill);
    }

    private static bool SelectedBucket(ExpenseDetailItem item, SalarySpendingFilter filter)
    {
        return filter.SpendingMode == SalarySpendingMode.AllExpenseTrend ||
            filter.SelectedSpendingBuckets.Any(bucket => Equal(Label(item.Category), bucket.Category) && Equal(Label(item.SpendingClass), bucket.SpendingClass)) ||
            (filter.SelectedSpendingBuckets.Count == 0 && (filter.SelectedCategories.Any(category => Equal(item.Category, category)) ||
            filter.SelectedCategoryTypes.Any(type => Equal(item.SpendingClass, type))));
    }

    private static IReadOnlyList<MonthlyExpenseTotalItem> ExpenseMonthly(ReportPeriod period, IReadOnlyList<ExpenseDetailItem> rows)
    {
        return period.Months()
            .Select(month => new MonthlyExpenseTotalItem
            {
                MonthStart = month,
                TotalAmount = rows.Where(row => row.TransactionDate.Year == month.Year && row.TransactionDate.Month == month.Month).Sum(row => row.Amount),
                TransactionCount = rows.Where(row => row.TransactionDate.Year == month.Year && row.TransactionDate.Month == month.Month).Select(row => row.ImportedTransactionId).Distinct().Count()
            })
            .ToArray();
    }

    private sealed class SpendingBucketComparer : IEqualityComparer<(string Category, string SpendingClass)>
    {
        public static readonly SpendingBucketComparer Instance = new();

        public bool Equals((string Category, string SpendingClass) left, (string Category, string SpendingClass) right)
        {
            return Equal(left.Category, right.Category) && Equal(left.SpendingClass, right.SpendingClass);
        }

        public int GetHashCode((string Category, string SpendingClass) bucket)
        {
            return HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(bucket.Category), StringComparer.OrdinalIgnoreCase.GetHashCode(bucket.SpendingClass));
        }
    }
}
