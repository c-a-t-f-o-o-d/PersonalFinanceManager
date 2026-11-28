using PersonalFinanceManager.Application.Services.Contracts;
using PersonalFinanceManager.Core.Models;

namespace PersonalFinanceManager.UiTests
{
    internal sealed class PreviewTransactionService : ITransactionWorkflowService
    {
        public Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Account>>([new Account
            {
                AccountId = 1,
                AccountNickname = "Household checking",
                Institution = "Example Financial",
                AccountType = "Checking"
            }]);
        }

        public Task<IReadOnlyList<string>> GetCategoryValuesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<string>>(["Groceries", "Mortgage", "Utilities"]);
        }

        public Task<IReadOnlyList<string>> GetCategoryTypeValuesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<string>>(["Food & Drink", "Housing"]);
        }

        public Task<IReadOnlyList<ImportedTransaction>> GetTransactionsAsync(TransactionFilter filter, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ImportedTransaction> rows = Enumerable.Range(1, 180).Select(index => new ImportedTransaction
            {
                ImportedTransactionId = index,
                AccountId = 1,
                AccountNickname = "Household checking",
                TransactionDate = DateOnly.FromDateTime(DateTime.Today).AddDays(-index / 3),
                TransactionTime = new TimeOnly(10, index % 60),
                Amount = -25M - index,
                Type = "Withdrawal",
                Description = "Example household purchase " + index,
                Merchant = "Example Market",
                Category = "Groceries",
                SpendingClass = "Food & Drink"
            }).ToArray();
            return Task.FromResult(rows);
        }

        public Task<ImportedTransaction?> GetTransactionAsync(long importedTransactionId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<long> SaveTransactionAsync(ImportedTransaction transaction, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteTransactionAsync(long importedTransactionId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> UpdateCategorizationAsync(IEnumerable<long> importedTransactionIds, string? category, string? categoryType, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> CreateRuleAndApplyAsync(FinancialRule rule, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
