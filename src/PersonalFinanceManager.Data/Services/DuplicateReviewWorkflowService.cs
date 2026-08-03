using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Data.Repositories;
using PersonalFinanceManager.Application.Services.Contracts;

namespace PersonalFinanceManager.Data.Services
{
    public sealed class DuplicateReviewWorkflowService : IDuplicateReviewWorkflowService
    {
        private readonly AccountRepository _accountRepository;
        private readonly DuplicateReviewRepository _duplicateReviewRepository;

        public DuplicateReviewWorkflowService(AccountRepository accountRepository, DuplicateReviewRepository duplicateReviewRepository)
        {
            _accountRepository = accountRepository;
            _duplicateReviewRepository = duplicateReviewRepository;
        }

        public Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken cancellationToken = default)
        {
            return _accountRepository.GetAllAsync(cancellationToken);
        }

        public Task<IReadOnlyList<DuplicateReviewItem>> GetAsync(DuplicateReviewFilter filter, CancellationToken cancellationToken = default)
        {
            return _duplicateReviewRepository.GetAsync(filter, cancellationToken);
        }

        public Task<int> ScanCrossAccountDuplicatesAsync(CancellationToken cancellationToken = default)
        {
            return _duplicateReviewRepository.ScanCrossAccountDuplicatesAsync(cancellationToken);
        }

        public Task<long> ApproveImportCandidateAsync(long importRowId, CancellationToken cancellationToken = default)
        {
            return _duplicateReviewRepository.ApproveAsync(importRowId, cancellationToken);
        }

        public Task IgnoreImportCandidateAsync(long importRowId, CancellationToken cancellationToken = default)
        {
            return _duplicateReviewRepository.IgnoreAsync(importRowId, cancellationToken);
        }

        public Task KeepCrossAccountAsync(long crossAccountDuplicateReviewId, CancellationToken cancellationToken = default)
        {
            return _duplicateReviewRepository.KeepCrossAccountAsync(crossAccountDuplicateReviewId, cancellationToken);
        }

        public Task DeleteCrossAccountCandidateAsync(long crossAccountDuplicateReviewId, CancellationToken cancellationToken = default)
        {
            return _duplicateReviewRepository.DeleteCrossAccountCandidateAsync(crossAccountDuplicateReviewId, cancellationToken);
        }

        public Task DeleteCrossAccountExistingAsync(long crossAccountDuplicateReviewId, CancellationToken cancellationToken = default)
        {
            return _duplicateReviewRepository.DeleteCrossAccountExistingAsync(crossAccountDuplicateReviewId, cancellationToken);
        }
    }
}
