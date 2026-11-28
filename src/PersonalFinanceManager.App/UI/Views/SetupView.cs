using PersonalFinanceManager.App.UI.Controls;
using PersonalFinanceManager.App.UI.Styling;
using PersonalFinanceManagerIdentity = PersonalFinanceManager.Core.Application.ApplicationIdentity;
using PersonalFinanceManager.Data.Configuration;

namespace PersonalFinanceManager.App.UI.Views
{
    public sealed class SetupView : UserControl, IPageLayoutControl
    {
        private readonly Action _openDatabase;
        private readonly Action _openSettings;
        private readonly AppSettings _settings;
        private readonly Func<string, Task> _configureConnectionAsync;
        private readonly Func<Task> _loadDemoDataAsync;

        public SetupView(AppSettings settings, Action openSettings, Action openDatabase, Func<string, Task> configureConnectionAsync, Func<Task> loadDemoDataAsync)
        {
            _settings = settings;
            _openSettings = openSettings;
            _openDatabase = openDatabase;
            _configureConnectionAsync = configureConnectionAsync;
            _loadDemoDataAsync = loadDemoDataAsync;
            InitializeLayout();
        }

        public int PreferredPageHeight => 1120;
        public PageScrollMode ScrollMode => PageScrollMode.Page;

        private void InitializeLayout()
        {
            TableLayoutPanel root = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new Padding(6)
            };

            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 290));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 260));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);
            CardPanel hero = CreateCard();
            root.Controls.Add(hero, 0, 0);
            root.SetColumnSpan(hero, 2);
            hero.Controls.Add(CreateHeaderLabel($"Welcome to {PersonalFinanceManagerIdentity.ProductName}", 18, 18, 520));

            hero.Controls.Add(CreateBodyLabel(
                $"{PersonalFinanceManagerIdentity.ProductName} needs a SQL Server database connection before account, import, reporting, and backup features are available. The connection string is stored only in your local Windows profile and is protected with DPAPI.", 18, 56, 1040, 44));

            Label status = CreateBodyLabel(BuildStatusText(), 18, 98, 1040, 24);
            UiTheme.SetTextRole(status, _settings.HasUsableConnectionString() ? "SuccessText" : "DangerText");
            hero.Controls.Add(status);

            root.Controls.Add(CreateConnectionCard(
                "Option 1: Local development",
                "Use SQL Server LocalDB or SQL Server Express on the same machine. This is the easiest option for contributors and reviewers who just want to run the project locally.",
                AppSettings.LocalDbConnectionString,
                "Use LocalDB Now",
                async () => await _configureConnectionAsync(AppSettings.LocalDbConnectionString).ConfigureAwait(true)), 0, 1);

            root.Controls.Add(CreateConnectionCard(
                "Option 2: Shared network SQL Server",
                "Use an existing SQL Server instance on your network. This is useful when you want multiple machines to connect to the same " + PersonalFinanceManagerIdentity.ProductName + " database.",
                AppSettings.NetworkSqlServerTemplateConnectionString,
                "Open Settings",
                () =>
                {
                    _openSettings();
                    return Task.CompletedTask;
                }), 1, 1);

            CardPanel workflow = CreateCard();
            root.Controls.Add(workflow, 0, 2);
            workflow.Controls.Add(CreateHeaderLabel("Recommended first-run workflow", 18, 18, 360));

            workflow.Controls.Add(CreateNumberedText(new[]
            {
                "Use LocalDB Now for the fastest local setup, or open Settings for network SQL Server.",
                "Replace placeholder values if you use network SQL Server.",
                "Save and test the connection.",
                "Open Database to confirm migrations are current.",
                "Create accounts, then import transactions."
            }, 18, 56, 480, 150));

            Button settings = CreatePrimaryButton("Open Settings", 18, 214, 140);
            settings.Click += (_, _) => _openSettings();
            workflow.Controls.Add(settings);
            Button database = CreateSecondaryButton("Open Database", 170, 214, 140);
            database.Click += (_, _) => _openDatabase();
            workflow.Controls.Add(database);
            CardPanel privacy = CreateCard();
            root.Controls.Add(privacy, 1, 2);
            privacy.Controls.Add(CreateHeaderLabel("Public repository safety", 18, 18, 360));

            privacy.Controls.Add(CreateNumberedText(new[]
            {
                "Do not commit appsettings.Local.json.",
                "Do not commit real bank exports or statements.",
                "Do not commit SQL backups, MDF, or LDF files.",
                "Use sanitized sample data for screenshots or demos.",
                "Keep personal categorization rules out of source."
            }, 18, 56, 480, 150));

            CardPanel demo = CreateCard();
            root.Controls.Add(demo, 0, 3);
            demo.Controls.Add(CreateHeaderLabel("Optional demo data", 18, 18, 360));
            demo.Controls.Add(CreateBodyLabel("Load sanitized sample accounts, rules, transactions, bills, and links into an empty database. This is useful for screenshots, code review, and first-time evaluation without importing real bank files.", 18, 56, 480, 80));
            Button demoButton = CreatePrimaryButton("Load Demo Data", 18, 154, 160);
            demoButton.Click += async (_, _) => await _loadDemoDataAsync().ConfigureAwait(true);
            demo.Controls.Add(demoButton);
            CardPanel nextSteps = CreateCard();
            root.Controls.Add(nextSteps, 1, 3);
            nextSteps.Controls.Add(CreateHeaderLabel("Contributor setup notes", 18, 18, 520));
            nextSteps.Controls.Add(CreateBodyLabel("This project currently targets SQL Server because the application was originally built for local/self-hosted use. A future public release should add a simpler non-technical installation option, improve sample/demo data, and then perform a final README rewrite after the architecture cleanup is complete.", 18, 56, 480, 100));
        }

        private static CardPanel CreateCard()
        {
            return new CardPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                Margin = new Padding(8)
            };
        }

        private CardPanel CreateConnectionCard(string title, string description, string connectionTemplate, string buttonText, Func<Task> buttonActionAsync)
        {
            CardPanel card = CreateCard();
            card.Controls.Add(CreateHeaderLabel(title, 18, 18, 420));
            card.Controls.Add(CreateBodyLabel(description, 18, 54, 470, 52));

            TextBox connection = new()
            {
                Location = new Point(18, 118),
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                Size = new Size(470, 88),
                Text = connectionTemplate
            };

            card.Controls.Add(connection);
            Button button = CreateSecondaryButton(buttonText, 18, 222, 160);
            button.Click += async (_, _) => await buttonActionAsync().ConfigureAwait(true);
            card.Controls.Add(button);
            return card;
        }

        private static Label CreateHeaderLabel(string text, int x, int y, int width)
        {
            return new Label
            {
                Text = text,
                Font = UiTheme.SectionTitleFont,
                ForeColor = UiTheme.TextPrimary,
                Location = new Point(x, y),
                Size = new Size(width, 28),
                AutoEllipsis = true
            };
        }

        private static Label CreateBodyLabel(string text, int x, int y, int width, int height)
        {
            return new Label
            {
                Text = text,
                Font = UiTheme.BodyFont,
                ForeColor = UiTheme.TextSecondary,
                Location = new Point(x, y),
                Size = new Size(width, height),
                AutoEllipsis = false
            };
        }

        private static Label CreateNumberedText(IReadOnlyList<string> lines, int x, int y, int width, int height)
        {
            return CreateBodyLabel(string.Join(Environment.NewLine, lines.Select((line, index) => $"{index + 1}. {line}")), x, y, width, height);
        }

        private static Button CreatePrimaryButton(string text, int x, int y, int width)
        {
            Button button = new()
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 34)
            };

            UiTheme.StylePrimaryButton(button);
            return button;
        }

        private static Button CreateSecondaryButton(string text, int x, int y, int width)
        {
            Button button = new()
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 34)
            };

            UiTheme.StyleSecondaryButton(button);
            return button;
        }

        private string BuildStatusText()
        {
            if (!_settings.HasUsableConnectionString())
            {
                return "Status: setup is required before database-backed features can be used.";
            }

            return "Status: a database connection is configured. Open Database to verify schema state.";
        }
    }
}
