using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Core.Rules;

namespace PersonalFinanceManager.Application.Services.Contracts
{
    public interface IRuleWorkflowService
    {
        Task<IReadOnlyList<FinancialRule>> GetRulesAsync(CancellationToken cancellationToken = default);

        Task<int> SaveRuleAsync(FinancialRule rule, CancellationToken cancellationToken = default);

        Task DeleteRuleAsync(int ruleId, CancellationToken cancellationToken = default);

        RuleImportResult ParseWorkbook(string workbookPath);

        Task ReplaceRulesWithWorkbookRowsAsync(IEnumerable<RuleImportRow> rows, CancellationToken cancellationToken = default);

        Task<int> ReapplyRulesAsync(CancellationToken cancellationToken = default);
    }
}
