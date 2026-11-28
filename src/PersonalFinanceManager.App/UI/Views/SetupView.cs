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
        private readonly List<CardPanel> _cards = new();
        private CardPanel? _hero;
        private bool _arrangingCards;

        public SetupView(AppSettings settings, Action openSettings, Action openDatabase, Func<string, Task> configureConnectionAsync, Func<Task> loadDemoDataAsync)
        {
            _settings = settings;
            _openSettings = openSettings;
            _openDatabase = openDatabase;
            _configureConnectionAsync = configureConnectionAsync;
            _loadDemoDataAsync = loadDemoDataAsync;
            InitializeLayout();
        }

        public int PreferredPageHeight => 0;
        public PageScrollMode ScrollMode => PageScrollMode.Page;

        public override Size GetPreferredSize(Size proposedSize)
        {
            int width = Math.Max(1, proposedSize.Width > 0 ? proposedSize.Width : ClientSize.Width);
            return new Size(width, ArrangeCards(width, false));
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);

            if (_arrangingCards || _hero is null)
            {
                return;
            }

            _arrangingCards = true;

            try
            {
                ArrangeCards(ClientSize.Width, true);
            }
            finally
            {
                _arrangingCards = false;
            }
        }

        private void InitializeLayout()
        {
            Padding = new Padding(6);
            _hero = CreateCard();
            _hero.Name = "SetupHero";
            Controls.Add(_hero);
            _hero.Controls.Add(CreateHeaderLabel($"Welcome to {PersonalFinanceManagerIdentity.ProductName}"));

            _hero.Controls.Add(CreateBodyLabel(
                $"{PersonalFinanceManagerIdentity.ProductName} needs a SQL Server database connection before account, import, reporting, and backup features are available. The connection string is stored only in your local Windows profile and is protected with DPAPI."));

            Label status = CreateBodyLabel(BuildStatusText());
            UiTheme.SetTextRole(status, _settings.HasUsableConnectionString() ? "SuccessText" : "DangerText");
            _hero.Controls.Add(status);

            AddCard(CreateConnectionCard(
                "Option 1: Local development",
                "Use SQL Server LocalDB or SQL Server Express on the same machine. This is the easiest option for contributors and reviewers who just want to run the project locally.",
                AppSettings.LocalDbConnectionString,
                "Use LocalDB Now",
                async () => await _configureConnectionAsync(AppSettings.LocalDbConnectionString).ConfigureAwait(true)));

            AddCard(CreateConnectionCard(
                "Option 2: Shared network SQL Server",
                "Use an existing SQL Server instance on your network. This is useful when you want multiple machines to connect to the same " + PersonalFinanceManagerIdentity.ProductName + " database.",
                AppSettings.NetworkSqlServerTemplateConnectionString,
                "Open Settings",
                () =>
                {
                    _openSettings();
                    return Task.CompletedTask;
                }));

            CardPanel workflow = CreateCard();
            AddCard(workflow);
            workflow.Controls.Add(CreateHeaderLabel("Recommended first-run workflow"));

            workflow.Controls.Add(CreateNumberedText(new[]
            {
                "Use LocalDB Now for the fastest local setup, or open Settings for network SQL Server.",
                "Replace placeholder values if you use network SQL Server.",
                "Save and test the connection.",
                "Open Database to confirm migrations are current.",
                "Create accounts, then import transactions."
            }));

            Button settings = CreatePrimaryButton("Open Settings", 140);
            settings.Click += (_, _) => _openSettings();
            Button database = CreateSecondaryButton("Open Database", 140);
            database.Click += (_, _) => _openDatabase();
            workflow.Controls.Add(CreateActions(settings, database));
            CardPanel privacy = CreateCard();
            AddCard(privacy);
            privacy.Controls.Add(CreateHeaderLabel("Public repository safety"));

            privacy.Controls.Add(CreateNumberedText(new[]
            {
                "Do not commit appsettings.Local.json.",
                "Do not commit real bank exports or statements.",
                "Do not commit SQL backups, MDF, or LDF files.",
                "Use sanitized sample data for screenshots or demos.",
                "Keep personal categorization rules out of source."
            }));

            CardPanel demo = CreateCard();
            AddCard(demo);
            demo.Controls.Add(CreateHeaderLabel("Optional demo data"));
            demo.Controls.Add(CreateBodyLabel("Load sanitized sample accounts, rules, transactions, bills, and links into an empty database. This is useful for screenshots, code review, and first-time evaluation without importing real bank files."));
            Button demoButton = CreatePrimaryButton("Load Demo Data", 160);
            demoButton.Click += async (_, _) => await _loadDemoDataAsync().ConfigureAwait(true);
            demo.Controls.Add(CreateActions(demoButton));
            CardPanel nextSteps = CreateCard();
            AddCard(nextSteps);
            nextSteps.Controls.Add(CreateHeaderLabel("After connecting"));
            nextSteps.Controls.Add(CreateBodyLabel("Create an account for each bank, card, or wallet you import. Review the starter categories and inactive rule examples before importing statements. Use Duplicate Review and Transaction Links to resolve repeated transactions and movements between your accounts."));
            PerformLayout();
        }

        private void AddCard(CardPanel card)
        {
            _cards.Add(card);
            Controls.Add(card);
        }

        private int ArrangeCards(int width, bool apply)
        {
            if (_hero is null)
            {
                return Padding.Vertical;
            }

            int availableWidth = Math.Max(1, width - Padding.Horizontal);
            int top = Padding.Top;
            int heroWidth = Math.Max(1, availableWidth - _hero.Margin.Horizontal);
            int heroHeight = MeasureCard(_hero, heroWidth, apply);

            if (apply)
            {
                _hero.Bounds = new Rectangle(Padding.Left + _hero.Margin.Left, top + _hero.Margin.Top, heroWidth, heroHeight);
            }

            top += heroHeight + _hero.Margin.Vertical;
            int columns = width >= LogicalToDeviceUnits(1000) ? 2 : 1;
            int columnWidth = availableWidth / columns;

            for (int first = 0; first < _cards.Count; first += columns)
            {
                int rowHeight = 0;

                for (int column = 0; column < columns && first + column < _cards.Count; column++)
                {
                    CardPanel card = _cards[first + column];
                    int cardWidth = Math.Max(1, columnWidth - card.Margin.Horizontal);
                    rowHeight = Math.Max(rowHeight, MeasureCard(card, cardWidth, apply) + card.Margin.Vertical);
                }

                for (int column = 0; apply && column < columns && first + column < _cards.Count; column++)
                {
                    CardPanel card = _cards[first + column];
                    card.Bounds = new Rectangle(Padding.Left + column * columnWidth + card.Margin.Left, top + card.Margin.Top,
                        Math.Max(1, columnWidth - card.Margin.Horizontal), Math.Max(1, rowHeight - card.Margin.Vertical));
                }

                top += rowHeight;
            }

            return top + Padding.Bottom;
        }

        private int MeasureCard(CardPanel card, int width, bool apply)
        {
            int contentWidth = Math.Max(1, width - card.Padding.Horizontal);
            int top = card.Padding.Top;
            int spacing = LogicalToDeviceUnits(10);

            foreach (Control child in card.Controls)
            {
                int height;

                if (child is TextBox)
                {
                    int textHeight = TextRenderer.MeasureText(child.Text, child.Font,
                        new Size(Math.Max(1, contentWidth - LogicalToDeviceUnits(8)), int.MaxValue),
                        TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix).Height;
                    height = Math.Max(LogicalToDeviceUnits(72), textHeight + LogicalToDeviceUnits(10));
                }
                else
                {
                    height = child.GetPreferredSize(new Size(contentWidth, 0)).Height;
                }

                if (apply)
                {
                    child.Bounds = new Rectangle(card.Padding.Left, top, contentWidth, height);
                }

                top += height + spacing;
            }

            return top + card.Padding.Bottom - (card.Controls.Count > 0 ? spacing : 0);
        }

        private static CardPanel CreateCard()
        {
            return new CardPanel
            {
                Padding = new Padding(18),
                Margin = new Padding(8)
            };
        }

        private CardPanel CreateConnectionCard(string title, string description, string connectionTemplate, string buttonText, Func<Task> buttonActionAsync)
        {
            CardPanel card = CreateCard();
            card.Controls.Add(CreateHeaderLabel(title));
            card.Controls.Add(CreateBodyLabel(description));

            TextBox connection = new()
            {
                Multiline = true,
                ReadOnly = true,
                WordWrap = true,
                Font = UiTheme.BodyFont,
                BorderStyle = BorderStyle.FixedSingle,
                Text = connectionTemplate
            };

            card.Controls.Add(connection);
            Button button = CreateSecondaryButton(buttonText, 160);
            button.Click += async (_, _) => await buttonActionAsync().ConfigureAwait(true);
            card.Controls.Add(CreateActions(button));
            return card;
        }

        private static Label CreateHeaderLabel(string text)
        {
            return new Label
            {
                Text = text,
                Font = UiTheme.SectionTitleFont,
                ForeColor = UiTheme.TextPrimary,
                UseMnemonic = false
            };
        }

        private static Label CreateBodyLabel(string text)
        {
            return new Label
            {
                Text = text,
                Font = UiTheme.BodyFont,
                ForeColor = UiTheme.TextSecondary,
                UseMnemonic = false
            };
        }

        private static Label CreateNumberedText(IReadOnlyList<string> lines)
        {
            return CreateBodyLabel(string.Join(Environment.NewLine, lines.Select((line, index) => $"{index + 1}. {line}")));
        }

        private static FlowLayoutPanel CreateActions(params Button[] buttons)
        {
            FlowLayoutPanel actions = new()
            {
                WrapContents = true,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            actions.Controls.AddRange(buttons);
            return actions;
        }

        private static Button CreatePrimaryButton(string text, int width)
        {
            Button button = new()
            {
                Text = text,
                Size = new Size(width, 34),
                Margin = new Padding(0, 0, 10, 0)
            };

            UiTheme.StylePrimaryButton(button);
            return button;
        }

        private static Button CreateSecondaryButton(string text, int width)
        {
            Button button = new()
            {
                Text = text,
                Size = new Size(width, 34),
                Margin = new Padding(0, 0, 10, 0)
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
