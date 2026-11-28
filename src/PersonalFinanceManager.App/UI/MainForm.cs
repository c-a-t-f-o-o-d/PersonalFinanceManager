using PersonalFinanceManager.App.UI.Controls;
using PersonalFinanceManager.App.UI.Styling;
using PersonalFinanceManager.App.UI.Views;
using PersonalFinanceManagerIdentity = PersonalFinanceManager.Core.Application.ApplicationIdentity;
using PersonalFinanceManager.Data.Configuration;
using PersonalFinanceManager.Data.Logging;

namespace PersonalFinanceManager.App.UI
{
    public sealed class MainForm : ThemedForm
    {
        private readonly Dictionary<string, SidebarIconButton> _navButtons = new(StringComparer.OrdinalIgnoreCase);
        private readonly FileLogger _logger;
        private readonly Label _pageSubtitleLabel = new();
        private readonly Label _statusDetailLabel = new();
        private readonly Label _statusLabel = new();
        private readonly Label _titleLabel = new();
        private readonly SidebarIconButton _themeToggle = new();
        private readonly ToolTip _themeToolTip = new() { ShowAlways = true };
        private readonly Panel _contentPanel = new();
        private readonly SettingsService _settingsService;
        private AppServices _services;
        private string _selectedNavigation = "Setup";
        private bool _changingAppearance;

        public MainForm(SettingsService settingsService, FileLogger logger)
        {
            _settingsService = settingsService;
            _logger = logger;
            _services = new AppServices(_settingsService.Load(), _logger);
            UiTheme.SetDarkMode(_services.Settings.UseDarkMode);
            InitializeLayout();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _themeToolTip.Dispose();
            }

            base.Dispose(disposing);
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

        protected override void OnThemeChanged(EventArgs e)
        {
            base.OnThemeChanged(e);
            _services.Settings.UseDarkMode = UiTheme.IsDarkMode;
            UpdateNavigationState();
            UpdateThemeToggle();
        }

        private void InitializeLayout()
        {
            Text = PersonalFinanceManagerIdentity.ProductName;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            MinimumSize = new Size(1000, 700);
            Width = 1600;
            Height = 980;
            Font = UiTheme.BodyFont;
            BackColor = UiTheme.AppBackground;

            TableLayoutPanel root = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty,
                BackColor = UiTheme.AppBackground
            };

            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);
            root.Controls.Add(BuildSidebar(), 0, 0);

            TableLayoutPanel main = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                BackColor = UiTheme.AppBackground,
                Padding = new Padding(24, 22, 20, 18)
            };

            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(main, 1, 0);

            TableLayoutPanel header = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = new Padding(0, 0, 4, 16),
                BackColor = UiTheme.AppBackground
            };

            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            main.Controls.Add(header, 0, 0);
            _titleLabel.Text = "Accounts";
            _titleLabel.Font = UiTheme.TitleFont;
            _titleLabel.Dock = DockStyle.Fill;
            _titleLabel.Margin = Padding.Empty;
            header.Controls.Add(_titleLabel, 0, 0);
            _pageSubtitleLabel.Text = "Manage your financial accounts";
            _pageSubtitleLabel.Font = UiTheme.PageSubtitleFont;
            _pageSubtitleLabel.Dock = DockStyle.Fill;
            _pageSubtitleLabel.Margin = new Padding(1, 3, 12, 0);
            UiTheme.SetTextRole(_pageSubtitleLabel, "SecondaryText");
            header.Controls.Add(_pageSubtitleLabel, 0, 1);
            _contentPanel.Dock = DockStyle.Fill;
            _contentPanel.Margin = Padding.Empty;
            _contentPanel.BackColor = UiTheme.AppBackground;
            main.Controls.Add(_contentPanel, 0, 1);
        }

        private Panel BuildSidebar()
        {
            Panel sidebar = new()
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.SidebarBackground,
                Padding = new Padding(12, 22, 12, 16),
                Margin = Padding.Empty,
                Tag = "SidebarSurface"
            };

            TableLayoutPanel sidebarLayout = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = Padding.Empty,
                BackColor = UiTheme.SidebarBackground
            };

            sidebarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
            sidebar.Controls.Add(sidebarLayout);

            Panel brand = new()
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.SidebarBackground,
                Margin = Padding.Empty
            };

            sidebarLayout.Controls.Add(brand, 0, 0);

            Label logo = new()
            {
                Text = "PF",
                Font = UiTheme.SectionTitleFont,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(6, 4),
                Size = new Size(40, 40),
                AccessibleName = PersonalFinanceManagerIdentity.ProductName
            };

            UiTheme.SetTextRole(logo, "AccentText");
            brand.Controls.Add(logo);

            Label appName = new()
            {
                Text = "Personal Finance",
                Font = UiTheme.SectionTitleFont,
                Location = new Point(54, 5),
                Size = new Size(158, 24)
            };

            brand.Controls.Add(appName);

            Label tagline = new()
            {
                Text = "MANAGER",
                Font = UiTheme.SmallFont,
                Location = new Point(55, 31),
                Size = new Size(155, 20)
            };

            UiTheme.SetTextRole(tagline, "MutedText");
            brand.Controls.Add(tagline);

            FlowLayoutPanel nav = new()
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(0, 0, 0, 12),
                Margin = Padding.Empty,
                AutoScroll = true,
                BackColor = UiTheme.SidebarBackground,
                Tag = "SidebarSurface"
            };

            sidebarLayout.Controls.Add(nav, 0, 1);
            AddNavHeading(nav, "WORKSPACE");
            AddNavButton(nav, "Accounts", "Accounts", SidebarIcon.Accounts, ShowAccounts);
            AddNavButton(nav, "Transactions", "Transactions", SidebarIcon.Transactions, ShowTransactions);
            AddNavButton(nav, "Imports", "Import statements", SidebarIcon.Imports, ShowImports);
            AddNavButton(nav, "Rules", "Rules & categories", SidebarIcon.Rules, ShowRules);
            AddNavHeading(nav, "REVIEW");
            AddNavButton(nav, "Duplicate Review", "Duplicate review", SidebarIcon.DuplicateReview, ShowDuplicateReview);
            AddNavButton(nav, "Transaction Links", "Transaction links", SidebarIcon.TransactionLinks, ShowTransactionLinks);
            AddNavButton(nav, "Data Health", "Data health", SidebarIcon.DataHealth, ShowDataHealth);
            AddNavHeading(nav, "PLAN");
            AddNavButton(nav, "Expense Analysis", "Expense analysis", SidebarIcon.ExpenseAnalysis, ShowReports);
            AddNavButton(nav, "Money In/Out", "Money in / out", SidebarIcon.MoneyInOut, ShowMoneyInOut);
            AddNavButton(nav, "Salary Needed", "Salary needed", SidebarIcon.SalaryNeeded, ShowSalaryAnalysis);
            AddNavButton(nav, "Runway", "Savings runway", SidebarIcon.Runway, ShowRunway);
            AddNavButton(nav, "Bills", "Recurring bills", SidebarIcon.Bills, ShowBills);
            AddNavHeading(nav, "MANAGE");
            AddNavButton(nav, "Setup", "Getting started", SidebarIcon.Setup, ShowSetup);
            AddNavButton(nav, "Backup/Export", "Backup & export", SidebarIcon.BackupExport, ShowBackupExport);
            AddNavButton(nav, "Settings", "Settings", SidebarIcon.Settings, ShowSettings);
            AddNavButton(nav, "Database", "Database", SidebarIcon.Database, ShowDatabase);

            _themeToggle.Name = "ThemeToggle";
            _themeToggle.Size = new Size(38, 38);
            _themeToggle.Margin = new Padding(4, 8, 0, 6);
            _themeToggle.Padding = Padding.Empty;
            _themeToggle.ImageAlign = ContentAlignment.MiddleCenter;
            _themeToggle.TextImageRelation = TextImageRelation.Overlay;
            _themeToggle.Click += (_, _) => ToggleAppearance();
            UiTheme.StyleSecondaryButton(_themeToggle);
            UpdateThemeToggle();
            nav.Controls.Add(_themeToggle);
            bool resizingNavigation = false;

            nav.Layout += (_, _) =>
            {
                if (resizingNavigation)
                {
                    return;
                }

                resizingNavigation = true;

                try
                {
                    int width = Math.Max(1, nav.ClientSize.Width - nav.Padding.Horizontal);

                    foreach (Control child in nav.Controls)
                    {
                        if (ReferenceEquals(child, _themeToggle))
                        {
                            int size = _themeToggle.LogicalToDeviceUnits(38);
                            child.Size = new Size(size, size);
                        }
                        else
                        {
                            child.Width = Math.Max(1, width - child.Margin.Horizontal);
                        }
                    }
                }
                finally
                {
                    resizingNavigation = false;
                }
            };

            CardPanel statusCard = new()
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                Margin = new Padding(0, 8, 0, 0)
            };

            sidebarLayout.Controls.Add(statusCard, 0, 2);
            _statusLabel.Text = "Starting";
            _statusLabel.Font = UiTheme.ButtonFont;
            _statusLabel.Dock = DockStyle.Top;
            _statusLabel.Height = 23;
            _statusDetailLabel.Text = "Initializing";
            _statusDetailLabel.Font = UiTheme.SmallFont;
            _statusDetailLabel.Dock = DockStyle.Fill;
            UiTheme.SetTextRole(_statusDetailLabel, "SecondaryText");
            statusCard.Controls.Add(_statusDetailLabel);
            statusCard.Controls.Add(_statusLabel);
            return sidebar;
        }

        private static void AddNavHeading(FlowLayoutPanel nav, string text)
        {
            Label heading = new()
            {
                Text = text,
                Font = UiTheme.SmallFont,
                TextAlign = ContentAlignment.BottomLeft,
                Width = 198,
                Height = 28,
                Padding = new Padding(14, 0, 0, 4),
                Margin = new Padding(0, 6, 0, 4)
            };

            UiTheme.SetTextRole(heading, "MutedText");
            nav.Controls.Add(heading);
        }

        private void AddNavButton(FlowLayoutPanel nav, string key, string text, SidebarIcon icon, Action action)
        {
            SidebarIconButton button = new()
            {
                Text = text,
                Icon = icon,
                Name = "Navigation" + icon,
                Width = 198,
                Height = 34,
                Margin = new Padding(0, 0, 0, 2),
                UseMnemonic = false,
                AccessibleName = text
            };

            UiTheme.StyleNavButton(button, false);
            button.Padding = new Padding(button.LogicalToDeviceUnits(12), 0, button.LogicalToDeviceUnits(4), 0);
            button.Click += (_, _) => action();
            _navButtons[key] = button;
            nav.Controls.Add(button);
        }

        private void SetStatus(string title, string detail, bool connected)
        {
            _statusLabel.Text = "●  " + title;
            UiTheme.SetTextRole(_statusLabel, connected ? "SuccessText" : "WarningText");
            _statusDetailLabel.Text = detail;
        }

        private void ToggleAppearance()
        {
            if (_changingAppearance)
            {
                return;
            }

            _changingAppearance = true;

            try
            {
                bool useDarkMode = !UiTheme.IsDarkMode;
                _settingsService.SaveAppearancePreference(useDarkMode);
                _services.Settings.UseDarkMode = useDarkMode;
                UiTheme.SetDarkMode(useDarkMode);
                UpdateThemeToggle();
            }
            catch (Exception ex)
            {
                _logger.Error("Settings", "Could not save the appearance preference.", ex);
                MessageBox.Show(this, "The appearance preference could not be saved. " + ex.Message, "Appearance", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _changingAppearance = false;
            }
        }

        private void UpdateThemeToggle()
        {
            string action = UiTheme.IsDarkMode ? "Switch to light mode" : "Switch to dark mode";
            string description = UiTheme.IsDarkMode ? "Dark mode is active. Switch to light mode." : "Light mode is active. Switch to dark mode.";
            _themeToggle.Text = string.Empty;
            _themeToggle.Icon = UiTheme.IsDarkMode ? SidebarIcon.Moon : SidebarIcon.Sun;
            _themeToggle.AccessibleName = action;
            _themeToggle.AccessibleDescription = description;
            _themeToolTip.SetToolTip(_themeToggle, description);
            _themeToggle.RefreshIcon();
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
                MaskAccountNumbers = _services.Settings.MaskAccountNumbers,
                UseDarkMode = UiTheme.IsDarkMode
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
                Core.Models.DemoDataLoadResult loadResult = await _services.DemoDataService.LoadDemoDataAsync().ConfigureAwait(true);
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
            ShowView("Transaction Links", "Reconcile purchases, account transfers, and repayments across your statements", new TransactionLinksView(_services.TransactionLinkWorkflowService));
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
            foreach (Control previousPage in _contentPanel.Controls.Cast<Control>().ToArray())
            {
                previousPage.Dispose();
            }

            _contentPanel.Controls.Clear();

            PageHost pageHost = new(view);
            _contentPanel.Controls.Add(pageHost);
            UiTheme.ApplyTo(pageHost);
        }

        private void UpdateNavigationState()
        {
            foreach (KeyValuePair<string, SidebarIconButton> pair in _navButtons)
            {
                SidebarIconButton button = pair.Value;
                UiTheme.StyleNavButton(button, string.Equals(pair.Key, _selectedNavigation, StringComparison.OrdinalIgnoreCase));
                button.Padding = new Padding(button.LogicalToDeviceUnits(12), 0, button.LogicalToDeviceUnits(4), 0);
                button.RefreshIcon();
            }
        }
    }
}
