using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Data.Repositories;
using PersonalFinanceManager.Application.Services.Contracts;

namespace PersonalFinanceManager.Data.Services
{
    public sealed class AccountWorkflowService : IAccountWorkflowService
    {
        private readonly AccountRepository _accountRepository;
        private readonly AccountReconciliationRepository _reconciliationRepository;

        public AccountWorkflowService(AccountRepository accountRepository, AccountReconciliationRepository reconciliationRepository)
        {
            _accountRepository = accountRepository;
            _reconciliationRepository = reconciliationRepository;
        }

        public Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken cancellationToken = default)
        {
            return _accountRepository.GetAllAsync(cancellationToken);
        }

        public Task<Account?> GetAccountForEditAsync(int accountId, CancellationToken cancellationToken = default)
        {
            return _accountRepository.GetByIdAsync(accountId, includeAccountNumber: true, cancellationToken);
        }

        public Task<int> SaveAccountAsync(Account account, CancellationToken cancellationToken = default)
        {
            return _accountRepository.SaveAsync(account, cancellationToken);
        }

        public Task DeleteAccountAsync(int accountId, CancellationToken cancellationToken = default)
        {
            return _accountRepository.DeleteAsync(accountId, cancellationToken);
        }

        public Task<AccountReconciliation> CheckStatementAsync(AccountReconciliationRequest request, bool record, CancellationToken cancellationToken = default)
        {
            return _reconciliationRepository.CheckAsync(request, record, cancellationToken);
        }

        public Task<IReadOnlyList<AccountReconciliation>> GetStatementHistoryAsync(int accountId, CancellationToken cancellationToken = default)
        {
            return _reconciliationRepository.GetHistoryAsync(accountId, cancellationToken);
        }
    }
}
