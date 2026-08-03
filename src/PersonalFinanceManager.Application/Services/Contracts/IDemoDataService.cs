using PersonalFinanceManager.Core.Models;

namespace PersonalFinanceManager.Application.Services.Contracts
{
    public interface IDemoDataService
    {
        Task<DemoDataSummary> GetSummaryAsync(CancellationToken cancellationToken = default);
        Task<DemoDataLoadResult> LoadDemoDataAsync(CancellationToken cancellationToken = default);
    }
}
