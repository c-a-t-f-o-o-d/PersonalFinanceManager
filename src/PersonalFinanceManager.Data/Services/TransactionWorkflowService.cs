using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Data.Repositories;
using PersonalFinanceManager.Application.Services.Contracts;

namespace PersonalFinanceManager.Data.Services
{
    public sealed class TransactionWorkflowService : ITransactionWorkflowService
    {
        private readonly AccountRepository _accountRepository;
        private readonly RuleRepository _ruleRepository;
        private readonly TransactionRepository _transactionRepository;

        public TransactionWorkflowService(AccountRepository accountRepository, RuleRepository ruleRepository, TransactionRepository transactionRepository)
        {
            _accountRepository = accountRepository;
            _ruleRepository = ruleRepository;
            _transactionRepository = transactionRepository;
        }

        public Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken cancellationToken = default)
        {
            return _accountRepository.GetAllAsync(cancellationToken);
        }

        public Task<IReadOnlyList<string>> GetCategoryValuesAsync(CancellationToken cancellationToken = default)
        {
            return _transactionRepository.GetCategoryValuesAsync(cancellationToken);
        }

        public Task<IReadOnlyList<string>> GetCategoryTypeValuesAsync(CancellationToken cancellationToken = default)
        {
            return _transactionRepository.GetCategoryTypeValuesAsync(cancellationToken);
        }

        public Task<IReadOnlyList<ImportedTransaction>> GetTransactionsAsync(TransactionFilter filter, CancellationToken cancellationToken = default)
        {
            return _transactionRepository.GetAsync(filter, cancellationToken);
        }

        public Task<ImportedTransaction?> GetTransactionAsync(long importedTransactionId, CancellationToken cancellationToken = default)
        {
            return _transactionRepository.GetByIdAsync(importedTransactionId, cancellationToken);
        }

        public Task<long> SaveTransactionAsync(ImportedTransaction transaction, CancellationToken cancellationToken = default)
        {
            return _transactionRepository.SaveAsync(transaction, cancellationToken);
        }

        public Task DeleteTransactionAsync(long importedTransactionId, CancellationToken cancellationToken = default)
        {
            return _transactionRepository.DeleteAsync(importedTransactionId, cancellationToken);
        }

        public Task<int> UpdateCategorizationAsync(IEnumerable<long> importedTransactionIds, string? category, string? categoryType, CancellationToken cancellationToken = default)
        {
            return _transactionRepository.UpdateCategorizationAsync(importedTransactionIds, category, categoryType, cancellationToken);
        }

        public async Task<int> CreateRuleAndApplyAsync(FinancialRule rule, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(rule);
            int ruleId = await _ruleRepository.SaveAsync(rule, cancellationToken).ConfigureAwait(false);
            rule.RuleId = ruleId;
            return await _transactionRepository.ApplyRuleAsync(rule, cancellationToken).ConfigureAwait(false);
        }
    }
}
