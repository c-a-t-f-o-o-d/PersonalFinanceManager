using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Core.Rules;
using PersonalFinanceManager.Data.Repositories;
using PersonalFinanceManager.Application.Services.Contracts;

namespace PersonalFinanceManager.Data.Services
{
    public sealed class RuleWorkflowService : IRuleWorkflowService
    {
        private readonly ImportService _importService;
        private readonly RuleRepository _ruleRepository;

        public RuleWorkflowService(RuleRepository ruleRepository, ImportService importService)
        {
            _ruleRepository = ruleRepository;
            _importService = importService;
        }

        public Task<IReadOnlyList<FinancialRule>> GetRulesAsync(CancellationToken cancellationToken = default)
        {
            return _ruleRepository.GetAllAsync(cancellationToken);
        }

        public Task<IReadOnlyList<string>> GetCategoryValuesAsync(CancellationToken cancellationToken = default)
        {
            return _ruleRepository.GetCategoryValuesAsync(cancellationToken);
        }

        public Task<IReadOnlyList<string>> GetCategoryTypeValuesAsync(CancellationToken cancellationToken = default)
        {
            return _ruleRepository.GetCategoryTypeValuesAsync(cancellationToken);
        }

        public Task<int> SaveRuleAsync(FinancialRule rule, CancellationToken cancellationToken = default)
        {
            return _ruleRepository.SaveAsync(rule, cancellationToken);
        }

        public Task DeleteRuleAsync(int ruleId, CancellationToken cancellationToken = default)
        {
            return _ruleRepository.DeleteAsync(ruleId, cancellationToken);
        }

        public RuleImportResult ParseWorkbook(string workbookPath)
        {
            RuleWorkbookParser parser = new();
            return parser.Parse(workbookPath);
        }

        public Task ReplaceRulesWithWorkbookRowsAsync(IEnumerable<RuleImportRow> rows, CancellationToken cancellationToken = default)
        {
            return _ruleRepository.ReplaceWithWorkbookRowsAsync(rows, cancellationToken);
        }

        public Task<int> ReapplyRulesAsync(CancellationToken cancellationToken = default)
        {
            return _importService.ReapplyRulesAsync(cancellationToken);
        }
    }
}
