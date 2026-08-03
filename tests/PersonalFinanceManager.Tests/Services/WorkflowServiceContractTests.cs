using PersonalFinanceManager.Data.Services;
using PersonalFinanceManager.Application.Services.Contracts;

namespace PersonalFinanceManager.Tests.Services
{
    [TestClass]
    public sealed class WorkflowServiceContractTests
    {
        [TestMethod]
        public void WorkflowServices_ImplementUiFacingContracts()
        {
            AssertImplements<IAccountWorkflowService, AccountWorkflowService>();
            AssertImplements<IAnalysisService, AnalysisService>();
            AssertImplements<IDataHealthWorkflowService, DataHealthWorkflowService>();
            AssertImplements<IDatabaseBackupService, DatabaseBackupService>();
            AssertImplements<IDuplicateReviewWorkflowService, DuplicateReviewWorkflowService>();
            AssertImplements<IImportWorkflowService, ImportWorkflowService>();
            AssertImplements<IRecurringBillWorkflowService, RecurringBillWorkflowService>();
            AssertImplements<IRuleWorkflowService, RuleWorkflowService>();
            AssertImplements<IRunwayWorkflowService, RunwayWorkflowService>();
            AssertImplements<ITransactionLinkWorkflowService, TransactionLinkWorkflowService>();
            AssertImplements<ITransactionWorkflowService, TransactionWorkflowService>();
        }

        private static void AssertImplements<TContract, TImplementation>()
        {
            Assert.IsTrue(typeof(TContract).IsAssignableFrom(typeof(TImplementation)), $"{typeof(TImplementation).Name} should implement {typeof(TContract).Name}.");
        }
    }
}
