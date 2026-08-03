using PersonalFinanceManager.Core.Models;

namespace PersonalFinanceManager.Application.Services.Contracts
{
    public interface IDuplicateReviewWorkflowService
    {
        Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<DuplicateReviewItem>> GetAsync(DuplicateReviewFilter filter, CancellationToken cancellationToken = default);

        Task<int> ScanCrossAccountDuplicatesAsync(CancellationToken cancellationToken = default);

        Task<long> ApproveImportCandidateAsync(long importRowId, CancellationToken cancellationToken = default);

        Task IgnoreImportCandidateAsync(long importRowId, CancellationToken cancellationToken = default);

        Task KeepCrossAccountAsync(long crossAccountDuplicateReviewId, CancellationToken cancellationToken = default);

        Task DeleteCrossAccountCandidateAsync(long crossAccountDuplicateReviewId, CancellationToken cancellationToken = default);

        Task DeleteCrossAccountExistingAsync(long crossAccountDuplicateReviewId, CancellationToken cancellationToken = default);
    }
}
