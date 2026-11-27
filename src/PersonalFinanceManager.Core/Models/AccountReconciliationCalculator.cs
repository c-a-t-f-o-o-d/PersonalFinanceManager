namespace PersonalFinanceManager.Core.Models
{
    public static class AccountReconciliationCalculator
    {
        private const decimal MaximumBalance = 9999999999999999.99M;

        public static AccountReconciliation Calculate(AccountReconciliationRequest request, string accountType, IEnumerable<ImportedTransaction> transactions)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(transactions);
            if (request.AccountId <= 0 || request.FromDate > request.ToDate)
            {
                throw new ArgumentException("Select an account and a statement period whose end is on or after its start.");
            }

            if (decimal.Round(request.OpeningBalance, 2) != request.OpeningBalance || decimal.Round(request.ClosingBalance, 2) != request.ClosingBalance)
            {
                throw new ArgumentException("Statement balances must use no more than two decimal places.");
            }

            if (Math.Abs(request.OpeningBalance) > MaximumBalance || Math.Abs(request.ClosingBalance) > MaximumBalance)
            {
                throw new ArgumentOutOfRangeException(nameof(request), "Statement balances are outside the supported range.");
            }

            List<ImportedTransaction> rows = transactions.Where(row => row.AccountId == request.AccountId
                && (row.PostingDate ?? row.TransactionDate) >= request.FromDate
                && (row.PostingDate ?? row.TransactionDate) <= request.ToDate).ToList();
            decimal netActivity = rows.Sum(row => row.Amount);
            bool isLiability = AccountClassification.IsLiability(accountType);
            decimal expectedClosingBalance = request.OpeningBalance + (isLiability ? -netActivity : netActivity);
            if (Math.Abs(netActivity) > MaximumBalance || Math.Abs(expectedClosingBalance) > MaximumBalance)
            {
                throw new ArgumentOutOfRangeException(nameof(transactions), "The calculated statement activity or balance is outside the supported range.");
            }

            return new AccountReconciliation
            {
                AccountId = request.AccountId,
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                OpeningBalance = request.OpeningBalance,
                ClosingBalance = request.ClosingBalance,
                NetActivity = netActivity,
                ExpectedClosingBalance = expectedClosingBalance,
                TransactionCount = rows.Count,
                DateFallbackCount = rows.Count(row => !row.PostingDate.HasValue),
                UsesLiabilityBalances = isLiability
            };
        }
    }
}
