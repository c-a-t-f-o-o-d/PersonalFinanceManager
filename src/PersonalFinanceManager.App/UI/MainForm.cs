using PersonalFinanceManager.App.UI.Controls;
using PersonalFinanceManager.App.UI.Styling;
using PersonalFinanceManager.App.UI.Views;
using PersonalFinanceManagerIdentity = PersonalFinanceManager.Core.Application.ApplicationIdentity;
using PersonalFinanceManager.Data.Configuration;
using PersonalFinanceManager.Data.Logging;

namespace PersonalFinanceManager.App.UI
{
    public sealed class MainForm : Form
    {
        private readonly Dictionary<string, Button> _navButtons = new(StringComparer.OrdinalIgnoreCase);
        private readonly FileLogger _logger;
        private readonly Label _pageSubtitleLabel = new();
        private readonly Label _statusDetailLabel = new();
        private readonly Label _statusLabel = new();
        private readonly Label _titleLabel = new();
        private readonly Panel _contentPanel = new();
        private readonly SettingsService _settingsService;
        private AppServices _services;
        private string _selectedNavigation = "Setup";

        public MainForm(SettingsService settingsService, FileLogger logger)
        {
            _settingsService = settingsService;
            _logger = logger;
            _services = new AppServices(_settingsService.Load(), _logger);
            InitializeLayout();
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            bool databaseReady = await InitializeDatabaseAsync().ConfigureAwait(true);

            if (databaseReady)
            {
                ShowAccounts();
                return;
            }

            ShowSetup();
        }

        private async Task<bool> InitializeDatabaseAsync()
        {
            if (!_services.ConnectionFactory.HasConnectionString)
            {
                SetStatus("Setup required", "Configure Settings", false);
                return false;
            }

            try
            {
                SetStatus("Database", "Initializing database...", false);
                await _services.DatabaseInitializer.InitializeAsync().ConfigureAwait(true);
                SetStatus("Database ready", "Connected", true);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("Startup", "Database initialization failed.", ex);
                SetStatus("Database unavailable", "Check Settings or Database", false);
                MessageBox.Show(this, ex.Message, "Database initialization failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        private void InitializeLayout()
        {
            Text = PersonalFinanceManagerIdentity.ProductName;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            MinimumSize = new Size(1320, 820);
            Width = 1640;
            Height = 940;
            Font = UiTheme.BodyFont;
            BackColor = UiTheme.AppBackground;

            TableLayoutPanel root = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.AppBackground
            };

            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 252));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(root);
            Panel sidebar = BuildSidebar();
            root.Controls.Add(sidebar, 0, 0);

            TableLayoutPanel main = new()
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                BackColor = UiTheme.AppBackground,
                Padding = new Padding(18, 18, 18, 14)
            };

            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(main, 1, 0);

            Panel header = new()
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.AppBackground
            };

            main.Controls.Add(header, 0, 0);
            _titleLabel.Text = "Accounts";
            _titleLabel.Font = UiTheme.TitleFont;
            _titleLabel.ForeColor = UiTheme.TextPrimary;
            _titleLabel.Location = new Point(0, 0);
            _titleLabel.Size = new Size(500, 31);
            header.Controls.Add(_titleLabel);
            _pageSubtitleLabel.Text = "Manage financial accounts";
            _pageSubtitleLabel.Font = UiTheme.PageSubtitleFont;
            _pageSubtitleLabel.ForeColor = UiTheme.TextSecondary;
            _pageSubtitleLabel.Location = new Point(2, 33);
            _pageSubtitleLabel.Size = new Size(700, 22);
            header.Controls.Add(_pageSubtitleLabel);
            _contentPanel.Dock = DockStyle.Fill;
            _contentPanel.BackColor = UiTheme.AppBackground;
            main.Controls.Add(_contentPanel, 0, 1);
        }

        private Panel BuildSidebar()
        {
            Panel sidebar = new()
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.SidebarBackground,
                Padding = new Padding(10, 18, 10, 14)
            };

            TableLayoutPanel sidebarLayout = new()
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                BackColor = UiTheme.SidebarBackground
            };

            sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
            sidebar.Controls.Add(sidebarLayout);

            Panel brand = new()
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.SidebarBackground
            };

            sidebarLayout.Controls.Add(brand, 0, 0);

            Label logo = new()
            {
                Text = "▮▮▮",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = UiTheme.Accent,
                Location = new Point(4, 10),
                Size = new Size(42, 32)
            };

            brand.Controls.Add(logo);

            Label appName = new()
            {
                Text = PersonalFinanceManagerIdentity.ProductName,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Location = new Point(50, 11),
                Size = new Size(140, 32)
            };

            brand.Controls.Add(appName);

            Label tagline = new()
            {
                Text = "Personal Finance",
                Font = UiTheme.SmallFont,
                ForeColor = UiTheme.TextMuted,
                Location = new Point(52, 43),
                Size = new Size(140, 20)
            };

            brand.Controls.Add(tagline);

            FlowLayoutPanel nav = new()
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(0, 18, 0, 0),
                AutoScroll = true,
                BackColor = UiTheme.SidebarBackground,
                Tag = "AllowScroll"
            };

            sidebarLayout.Controls.Add(nav, 0, 1);
            AddNavButton(nav, "Setup", "◇  Setup", ShowSetup);
            AddNavButton(nav, "Accounts", "☰  Accounts", ShowAccounts);
            AddNavButton(nav, "Transactions", "≡  Transactions", ShowTransactions);
            AddNavButton(nav, "Rules", "◇  Rules", ShowRules);
            AddNavButton(nav, "Imports", "↓  Imports", ShowImports);
            AddNavButton(nav, "Duplicate Review", "≋  Duplicate Review", ShowDuplicateReview);
            AddNavButton(nav, "Data Health", "✓  Data Health", ShowDataHealth);
            AddNavButton(nav, "Transaction Links", "⇆  Transaction Links", ShowTransactionLinks);
            AddNavButton(nav, "Expense Analysis", "▣  Expense Analysis", ShowReports);
            AddNavButton(nav, "Money In/Out", "⇄  Money In/Out", ShowMoneyInOut);
            AddNavButton(nav, "Salary Needed", "$  Salary Needed", ShowSalaryAnalysis);
            AddNavButton(nav, "Runway", "⌁  Runway", ShowRunway);
            AddNavButton(nav, "Bills", "□  Bills", ShowBills);
            AddNavButton(nav, "Backup/Export", "☁  Backup/Export", ShowBackupExport);
            AddNavButton(nav, "Settings", "⚙  Settings", ShowSettings);
            AddNavButton(nav, "Database", "◎  Database", ShowDatabase);

            CardPanel statusCard = new()
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                Margin = new Padding(0)
            };

            sidebarLayout.Controls.Add(statusCard, 0, 2);
            _statusLabel.Text = "Starting";
            _statusLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Regular);
            _statusLabel.ForeColor = UiTheme.TextPrimary;
            _statusLabel.Location = new Point(14, 12);
            _statusLabel.Size = new Size(160, 22);
            statusCard.Controls.Add(_statusLabel);
            _statusDetailLabel.Text = "Initializing";
            _statusDetailLabel.Font = UiTheme.SmallFont;
            _statusDetailLabel.ForeColor = UiTheme.TextSecondary;
            _statusDetailLabel.Location = new Point(14, 36);
            _statusDetailLabel.Size = new Size(160, 22);
            statusCard.Controls.Add(_statusDetailLabel);
            return sidebar;
        }

        private void AddNavButton(FlowLayoutPanel nav, string key, string text, Action action)
        {
            Button button = new()
            {
                Text = text,
                Width = 220,
                Height = 40,
                Margin = new Padding(0, 0, 0, 8)
            };

            UiTheme.StyleNavButton(button, false);
            button.Click += (_, _) => action();
            _navButtons[key] = button;
            nav.Controls.Add(button);
        }

        private void SetStatus(string title, string detail, bool connected)
        {
            _statusLabel.Text = connected ? "● " + title : "● " + title;
            _statusLabel.ForeColor = connected ? UiTheme.Success : Color.FromArgb(220, 38, 38);
            _statusDetailLabel.Text = detail;
        }

        private void ShowSetup()
        {
            ShowView("Setup", "Configure the database connection and first-run workflow", new SetupView(_services.Settings, ShowSettings, ShowDatabase, ConfigureConnectionAndInitializeAsync, LoadDemoDataAsync));
        }

        private async Task ConfigureConnectionAndInitializeAsync(string connectionString)
        {
            AppSettings settings = new()
            {
                ConnectionString = connectionString,
                ConnectionProfileName = AppSettings.ResolveProfileName(connectionString),
                BackupFolder = _services.Settings.BackupFolder,
                DefaultImportFolder = _services.Settings.DefaultImportFolder,
                MaskAccountNumbers = _services.Settings.MaskAccountNumbers
            };

            _settingsService.Save(settings);
            _services = new AppServices(_settingsService.Load(), _logger);
            bool databaseReady = await InitializeDatabaseAsync().ConfigureAwait(true);

            if (databaseReady)
            {
                ShowDatabase();
                return;
            }

            ShowSettings();
        }

        private async Task LoadDemoDataAsync()
        {
            if (!_services.ConnectionFactory.HasConnectionString)
            {
                MessageBox.Show(this, "Configure a database connection before loading demo data.", "Setup required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool databaseReady = await InitializeDatabaseAsync().ConfigureAwait(true);
            if (!databaseReady)
            {
                return;
            }

            DialogResult result = MessageBox.Show(this, "Load sanitized demo accounts, transactions, rules, and bills into the current empty database? Demo data can only be loaded when the database has no accounts, rules, transactions, or bills.", "Load demo data", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes)
            {
                return;
            }

            try
            {
                PersonalFinanceManager.Core.Models.DemoDataLoadResult loadResult = await _services.DemoDataService.LoadDemoDataAsync().ConfigureAwait(true);
                MessageBox.Show(this, loadResult.Message, "Demo data loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ShowAccounts();
            }
            catch (Exception ex)
            {
                _logger.Error("DemoData", "Demo data load failed.", ex);
                MessageBox.Show(this, ex.Message, "Demo data load failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ShowAccounts()
        {
            ShowView("Accounts", "Manage financial accounts", new AccountsView(_services.AccountWorkflowService));
        }

        private void ShowTransactions()
        {
            ShowView("Transactions", "Review, clean up, and categorize imported activity", new TransactionsView(_services.TransactionWorkflowService));
        }

        private void ShowRules()
        {
            ShowView("Rules", "Maintain categorization rules for future imports", new RulesView(_services.RuleWorkflowService));
        }

        private void ShowImports()
        {
            ShowView("Imports", "Import account exports and apply rules", new ImportsView(_services.ImportWorkflowService));
        }

        private void ShowDuplicateReview()
        {
            ShowView("Duplicate Review", "Review questionable import overlap before adding transactions", new DuplicateReviewView(_services.DuplicateReviewWorkflowService));
        }

        private void ShowTransactionLinks()
        {
            ShowView("Transaction Links", "Review and edit links between PayPal, bank, credit, and transfer rows", new TransactionLinksView(_services.TransactionLinkWorkflowService));
        }

        private void ShowDataHealth()
        {
            ShowView("Data Health", "Review data-quality checks before trusting analysis pages", new DataHealthView(_services.DataHealthWorkflowService));
        }

        private void ShowReports()
        {
            ShowView("Expense Analysis", "Where spending is going by category and time period", new ReportsView(_services.AnalysisService));
        }

        private void ShowMoneyInOut()
        {
            ShowView("Money In/Out", "Compare income, spending, and monthly net cash flow", new MoneyInOutView(_services.AnalysisService));
        }

        private void ShowSalaryAnalysis()
        {
            ShowView("Salary Needed", "Estimate gross salary required to sustain current spending after taxes", new SalaryAnalysisView(_services.AnalysisService));
        }

        private void ShowRunway()
        {
            ShowView("Runway", "Estimate how long available funds last against required bills", new RunwayView(_services.RunwayWorkflowService));
        }

        private void ShowBills()
        {
            ShowView("Bills", "Track recurring required and optional monthly expenses", new RecurringBillsView(_services.RecurringBillWorkflowService));
        }

        private void ShowBackupExport()
        {
            ShowView("Backup/Export", "Backup the database or export readable data", new BackupExportView(_services.DatabaseBackupService, _settingsService, _services.Settings));
        }

        private void ShowSettings()
        {
            ShowView("Settings", "Application and database connection settings", new SettingsView(_settingsService, _services.Settings, async () =>
            {
                _services = new AppServices(_settingsService.Load(), _logger);
                _ = await InitializeDatabaseAsync().ConfigureAwait(true);
            }));
        }

        private void ShowDatabase()
        {
            ShowView("Database", "Connection and schema status", new DatabaseView(_services.SchemaStatusService, async () =>
            {
                _ = await InitializeDatabaseAsync().ConfigureAwait(true);
            }));
        }

        private void ShowView(string title, string subtitle, Control view)
        {
            _selectedNavigation = title;
            UpdateNavigationState();
            _titleLabel.Text = title;
            _pageSubtitleLabel.Text = subtitle;
            _contentPanel.Controls.Clear();

            Panel pageHost = new()
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = UiTheme.AppBackground,
                Padding = new Padding(0, 0, 10, 0)
            };

            view.Dock = DockStyle.None;
            view.Location = new Point(0, 0);
            view.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            view.BackColor = UiTheme.AppBackground;

            if (view is ScrollableControl scrollableControl)
            {
                scrollableControl.AutoScroll = false;
            }

            UiTheme.ApplyTo(view);
            pageHost.Controls.Add(view);
            pageHost.Resize += (_, _) => ResizeHostedView(pageHost, view);
            _contentPanel.Controls.Add(pageHost);
            ResizeHostedView(pageHost, view);
        }

        private static void ResizeHostedView(ScrollableControl pageHost, Control view)
        {
            int verticalScrollbarPadding = SystemInformation.VerticalScrollBarWidth + 8;
            int width = Math.Max(600, pageHost.ClientSize.Width - verticalScrollbarPadding);
            int preferredHeight = view is IPageLayoutControl pageLayoutControl ? pageLayoutControl.PreferredPageHeight : 1000;
            int height = Math.Max(pageHost.ClientSize.Height - 2, preferredHeight);
            view.SuspendLayout();
            view.Width = width;
            view.Height = height;
            view.ResumeLayout(true);
        }

        private void UpdateNavigationState()
        {
            foreach (KeyValuePair<string, Button> pair in _navButtons)
            {
                UiTheme.StyleNavButton(pair.Value, string.Equals(pair.Key, _selectedNavigation, StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}
