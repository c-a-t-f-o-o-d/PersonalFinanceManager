using PersonalFinanceManager.Data.Configuration;
using PersonalFinanceManager.Data.Database;
using PersonalFinanceManager.Data.Logging;
using PersonalFinanceManager.Data.Repositories;
using PersonalFinanceManager.Data.Security;
using PersonalFinanceManager.Data.Services;
using PersonalFinanceManager.Application.Services.Contracts;
using PersonalFinanceManager.Data.Sql;

namespace PersonalFinanceManager.App.UI
{
    public sealed class AppServices
    {
        public AppServices(AppSettings settings, FileLogger logger)
        {
            Settings = settings;
            Logger = logger;
            ConnectionFactory = new SqlConnectionFactory(settings.ConnectionString);
            AccountDataProtector dataProtector = new();
            AccountRepository accountRepository = new(ConnectionFactory, dataProtector);
            RuleRepository ruleRepository = new(ConnectionFactory);
            TransactionRepository transactionRepository = new(ConnectionFactory);
            TransactionLinkRepository transactionLinkRepository = new(ConnectionFactory);
            DuplicateReviewRepository duplicateReviewRepository = new(ConnectionFactory);
            DataHealthRepository dataHealthRepository = new(ConnectionFactory);
            ReportRepository reportRepository = new(ConnectionFactory);
            RecurringBillRepository recurringBillRepository = new(ConnectionFactory);
            DatabaseInitializer = new DatabaseInitializer(ConnectionFactory, logger);
            SchemaStatusService = new SchemaStatusService(ConnectionFactory);
            DatabaseBackupService = new DatabaseBackupService(ConnectionFactory);
            DemoDataService = new DemoDataService(ConnectionFactory);
            ImportService importService = new(ConnectionFactory, ruleRepository);
            AnalysisService = new AnalysisService(accountRepository, reportRepository);
            AccountWorkflowService = new AccountWorkflowService(accountRepository, new AccountReconciliationRepository(ConnectionFactory));
            RuleWorkflowService = new RuleWorkflowService(ruleRepository, importService);
            TransactionWorkflowService = new TransactionWorkflowService(accountRepository, ruleRepository, transactionRepository);
            ImportWorkflowService = new ImportWorkflowService(accountRepository, importService);
            DuplicateReviewWorkflowService = new DuplicateReviewWorkflowService(accountRepository, duplicateReviewRepository);
            RecurringBillWorkflowService = new RecurringBillWorkflowService(accountRepository, recurringBillRepository, transactionRepository);
            DataHealthWorkflowService = new DataHealthWorkflowService(dataHealthRepository);
            TransactionLinkWorkflowService = new TransactionLinkWorkflowService(accountRepository, transactionLinkRepository, importService);
            RunwayWorkflowService = new RunwayWorkflowService(accountRepository, recurringBillRepository);
        }

        public IAccountWorkflowService AccountWorkflowService { get; }
        public IAnalysisService AnalysisService { get; }
        public AppSettings Settings { get; }
        public IDatabaseBackupService DatabaseBackupService { get; }
        public IDemoDataService DemoDataService { get; }
        public DatabaseInitializer DatabaseInitializer { get; }
        public IDataHealthWorkflowService DataHealthWorkflowService { get; }
        public IDuplicateReviewWorkflowService DuplicateReviewWorkflowService { get; }
        public FileLogger Logger { get; }
        public IImportWorkflowService ImportWorkflowService { get; }
        public IRecurringBillWorkflowService RecurringBillWorkflowService { get; }
        public IRuleWorkflowService RuleWorkflowService { get; }
        public IRunwayWorkflowService RunwayWorkflowService { get; }
        public SchemaStatusService SchemaStatusService { get; }
        public SqlConnectionFactory ConnectionFactory { get; }
        public ITransactionLinkWorkflowService TransactionLinkWorkflowService { get; }
        public ITransactionWorkflowService TransactionWorkflowService { get; }
    }
}
