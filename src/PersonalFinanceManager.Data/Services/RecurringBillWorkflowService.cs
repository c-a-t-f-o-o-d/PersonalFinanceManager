using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Data.Repositories;
using PersonalFinanceManager.Application.Services.Contracts;

namespace PersonalFinanceManager.Data.Services
{
    public sealed class RecurringBillWorkflowService : IRecurringBillWorkflowService
    {
        private readonly AccountRepository _accountRepository;
        private readonly RecurringBillRepository _billRepository;
        private readonly TransactionRepository _transactionRepository;

        public RecurringBillWorkflowService(AccountRepository accountRepository, RecurringBillRepository billRepository, TransactionRepository transactionRepository)
        {
            _accountRepository = accountRepository;
            _billRepository = billRepository;
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

        public Task<IReadOnlyList<string>> GetRequirementTypesAsync(CancellationToken cancellationToken = default)
        {
            return _billRepository.GetRequirementTypesAsync(cancellationToken);
        }

        public Task<IReadOnlyList<RecurringBill>> GetBillsAsync(CancellationToken cancellationToken = default)
        {
            return _billRepository.GetBillsAsync(cancellationToken);
        }

        public Task<RecurringBill?> GetBillAsync(int recurringBillId, CancellationToken cancellationToken = default)
        {
            return _billRepository.GetByIdAsync(recurringBillId, cancellationToken);
        }

        public Task<int> SaveBillAsync(RecurringBill bill, CancellationToken cancellationToken = default)
        {
            return _billRepository.SaveAsync(bill, cancellationToken);
        }

        public Task DeleteBillAsync(int recurringBillId, CancellationToken cancellationToken = default)
        {
            return _billRepository.DeleteAsync(recurringBillId, cancellationToken);
        }

        public Task<IReadOnlyList<RecurringBillCandidate>> GetMonthlyCandidatesAsync(int minimumTransactions, int minimumMonths, CancellationToken cancellationToken = default)
        {
            return _billRepository.GetMonthlyCandidatesAsync(minimumTransactions, minimumMonths, cancellationToken);
        }
    }
}
