using PersonalFinanceManager.App.UI.Controls;
using PersonalFinanceManager.App.UI.Styling;
using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Application.Services.Contracts;

namespace PersonalFinanceManager.App.UI.Views
{
    public sealed class DataHealthView : AsyncPageControl, IPageLayoutControl
    {
        private readonly BindingSource _bindingSource = new();
        private readonly DataGridView _grid = new();
        private readonly Label _summary = new();
        private readonly IDataHealthWorkflowService _dataHealthService;

        public DataHealthView(IDataHealthWorkflowService dataHealthService)
        {
            _dataHealthService = dataHealthService;
            InitializeLayout();
        }

        public int PreferredPageHeight => 940;

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (!TryBeginInitialLoad())
            {
                return;
            }

            await RefreshAsync().ConfigureAwait(true);
        }

        private void InitializeLayout()
        {
            BackColor = UiTheme.AppBackground;

            TableLayoutPanel root = new()
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                BackColor = UiTheme.AppBackground
            };

            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            FlowLayoutPanel commandRow = new()
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                Padding = new Padding(0, 8, 0, 8)
            };

            root.Controls.Add(commandRow, 0, 0);
            Button refresh = new() { Text = "Refresh", Width = 100, Height = 34 };
            UiTheme.StylePrimaryButton(refresh);
            refresh.Click += async (_, _) => await RefreshAsync().ConfigureAwait(true);
            commandRow.Controls.Add(refresh);
            Button viewDetails = new() { Text = "View Details", Width = 120, Height = 34 };
            UiTheme.StyleSecondaryButton(viewDetails);
            viewDetails.Click += async (_, _) => await ViewDetailsAsync().ConfigureAwait(true);
            commandRow.Controls.Add(viewDetails);
            Button syncOwners = new() { Text = "Sync Owners From Accounts", Width = 210, Height = 34 };
            UiTheme.StyleSecondaryButton(syncOwners);
            syncOwners.Click += async (_, _) => await SyncOwnersFromAccountsAsync().ConfigureAwait(true);
            commandRow.Controls.Add(syncOwners);
            _summary.AutoSize = true;
            _summary.Margin = new Padding(14, 10, 4, 4);
            _summary.Tag = "SecondaryText";
            commandRow.Controls.Add(_summary);

            CardPanel explanation = new()
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(14, 10, 14, 10),
                Margin = new Padding(0, 0, 0, 10)
            };

            root.Controls.Add(explanation, 0, 1);

            explanation.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Data Health checks for issues that can make calculations hard to trust. Select a row and click View Details to see the underlying accounts, transactions, or bills without changing any data. Sync Owners From Accounts updates transaction owner fields from account ownership when the account owner is correct.",
                Tag = "SecondaryText",
                AutoEllipsis = false
            });

            CardPanel gridCard = new()
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12)
            };

            root.Controls.Add(gridCard, 0, 2);
            _grid.Dock = DockStyle.Fill;
            _grid.AutoGenerateColumns = false;
            _grid.AllowUserToAddRows = false;
            _grid.ReadOnly = true;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.DataSource = _bindingSource;
            _grid.CellFormatting += Grid_CellFormatting;
            _grid.CellDoubleClick += async (_, _) => await ViewDetailsAsync().ConfigureAwait(true);
            _grid.Columns.Add(MakeTextColumn(nameof(DataHealthCheckResult.Severity), "Severity", 90));
            _grid.Columns.Add(MakeTextColumn(nameof(DataHealthCheckResult.Area), "Area", 120));
            _grid.Columns.Add(MakeTextColumn(nameof(DataHealthCheckResult.Count), "Count", 80));
            _grid.Columns.Add(MakeTextColumn(nameof(DataHealthCheckResult.Issue), "Issue", 280));
            _grid.Columns.Add(MakeFillColumn(nameof(DataHealthCheckResult.Recommendation), "Recommendation", 420));
            gridCard.Controls.Add(_grid);
            UiTheme.StyleGrid(_grid);
            PageLayout.AutoSizeHeaders(root, 0, 1);
            UiTheme.ApplyTo(this);
        }

        private Task RefreshAsync()
        {
            return LoadLatestAsync("checks", "Loading data health checks...", token => _dataHealthService.GetChecksAsync(token), ApplyChecks);
        }

        private void ApplyChecks(IReadOnlyList<DataHealthCheckResult> results)
        {
            _bindingSource.DataSource = results;
            int warnings = 0;

            foreach (DataHealthCheckResult result in results)
            {
                if (result.Count > 0 && string.Equals(result.Severity, "Warning", StringComparison.OrdinalIgnoreCase))
                {
                    warnings++;
                }
            }

            _summary.Text = $"{warnings} warning check(s) currently have non-zero counts.";
        }

        private async Task ViewDetailsAsync()
        {
            if (_bindingSource.Current is not DataHealthCheckResult selected)
            {
                return;
            }

            IReadOnlyList<DataHealthDetailRow>? rows = null;
            await LoadLatestAsync("details", "Loading check details...",
                token => _dataHealthService.GetDetailsAsync(selected.IssueCode, token), result => rows = result).ConfigureAwait(true);

            if (!IsPageActive || rows is null)
            {
                return;
            }

            using DataHealthDetailForm form = new(selected, rows);
            form.ShowDialog(this);
        }

        private async Task SyncOwnersFromAccountsAsync()
        {
            DialogResult result = MessageBox.Show(this,
                "This will update transaction owner fields from each transaction account's Account Owner value. Use this when your account ownership values are correct. Continue?",
                "Sync transaction owners",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
            {
                return;
            }

            int affected = await _dataHealthService.SyncTransactionOwnersFromAccountsAsync().ConfigureAwait(true);

            if (!IsPageActive)
            {
                return;
            }

            MessageBox.Show(this, $"Updated {affected} owner field value(s).", "Sync complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await RefreshAsync().ConfigureAwait(true);
        }

        private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || _bindingSource[e.RowIndex] is not DataHealthCheckResult result)
            {
                return;
            }

            if (result.Count == 0)
            {
                e.CellStyle.ForeColor = UiTheme.TextMuted;
                return;
            }

            if (string.Equals(result.Severity, "Warning", StringComparison.OrdinalIgnoreCase))
            {
                e.CellStyle.ForeColor = UiTheme.Danger;
            }
            else
            {
                e.CellStyle.ForeColor = UiTheme.Accent;
            }
        }

        private static DataGridViewTextBoxColumn MakeTextColumn(string propertyName, string headerText, int width)
        {
            return new DataGridViewTextBoxColumn
            {
                DataPropertyName = propertyName,
                HeaderText = headerText,
                Width = width
            };
        }

        private static DataGridViewTextBoxColumn MakeFillColumn(string propertyName, string headerText, int minimumWidth)
        {
            return new DataGridViewTextBoxColumn
            {
                DataPropertyName = propertyName,
                HeaderText = headerText,
                MinimumWidth = minimumWidth,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            };
        }

        private sealed class DataHealthDetailForm : Form
        {
            private readonly BindingSource _source = new();
            private readonly DataGridView _grid = new();

            public DataHealthDetailForm(DataHealthCheckResult check, IReadOnlyList<DataHealthDetailRow> rows)
            {
                Text = $"Data Health Details - {check.Issue}";
                StartPosition = FormStartPosition.CenterParent;
                Width = 1320;
                Height = 720;
                Font = UiTheme.BodyFont;
                BackColor = UiTheme.AppBackground;
                InitializeLayout(check, rows);
            }

            private void InitializeLayout(DataHealthCheckResult check, IReadOnlyList<DataHealthDetailRow> rows)
            {
                TableLayoutPanel root = new()
                {
                    Dock = DockStyle.Fill,
                    RowCount = 3,
                    Padding = new Padding(12)
                };

                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                Controls.Add(root);

                Label title = new()
                {
                    Text = check.Issue,
                    Dock = DockStyle.Fill,
                    Font = new Font(UiTheme.BodyFont, FontStyle.Bold)
                };

                root.Controls.Add(title, 0, 0);

                Label recommendation = new()
                {
                    Text = $"{rows.Count} row(s). {check.Recommendation}",
                    Dock = DockStyle.Fill,
                    Tag = "SecondaryText"
                };

                root.Controls.Add(recommendation, 0, 1);
                _source.DataSource = rows;
                _grid.Dock = DockStyle.Fill;
                _grid.AutoGenerateColumns = false;
                _grid.AllowUserToAddRows = false;
                _grid.ReadOnly = true;
                _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                _grid.DataSource = _source;
                AddColumn("GroupKey", "Group", 180);
                AddColumn("Source", "Source", 120);
                AddColumn("TransactionId", "Txn ID", 80);
                AddColumn("Account", "Account", 150);
                AddColumn("AccountType", "Account Type", 120);
                AddColumn("AccountOwner", "Account Owner", 120);
                AddColumn("Date", "Date", 95);
                AddColumn("Amount", "Amount", 100, "C2");
                AddColumn("Type", "Type", 100);
                AddColumn("Description", "Description", 300);
                AddColumn("Category", "Category", 140);
                AddColumn("CategoryType", "Category Type", 150);
                AddColumn("IncomeOwner", "Owner", 120);
                AddColumn("Memo", "Memo", 260);
                AddColumn("Details", "Details", 340);
                root.Controls.Add(_grid, 0, 2);
                UiTheme.StyleGrid(_grid);
                UiTheme.ApplyTo(this);
            }

            private void AddColumn(string propertyName, string headerText, int width, string? format = null)
            {
                DataGridViewTextBoxColumn column = new()
                {
                    DataPropertyName = propertyName,
                    HeaderText = headerText,
                    Width = width
                };

                if (!string.IsNullOrWhiteSpace(format))
                {
                    column.DefaultCellStyle.Format = format;
                }

                _grid.Columns.Add(column);
            }
        }
    }
}
