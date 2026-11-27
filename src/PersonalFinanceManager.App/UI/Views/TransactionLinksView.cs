using System.Text;
using PersonalFinanceManager.App.UI.Controls;
using PersonalFinanceManager.App.UI.Styling;
using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Application.Services.Contracts;

namespace PersonalFinanceManager.App.UI.Views
{
    public sealed class TransactionLinksView : UserControl, IPageLayoutControl
    {
        private readonly BindingSource _bindingSource = new();
        private readonly ComboBox _accountFilter = new();
        private readonly ComboBox _linkTypeFilter = new();
        private readonly NumericUpDown _maxRows = new();
        private readonly TextBox _search = new();
        private readonly TextBox _sourceDetails = new();
        private readonly TextBox _linkedDetails = new();
        private readonly ITransactionLinkWorkflowService _transactionLinkService;
        private readonly DataGridView _grid = new();
        private IReadOnlyList<Account> _accounts = Array.Empty<Account>();

        public TransactionLinksView(ITransactionLinkWorkflowService transactionLinkService)
        {
            _transactionLinkService = transactionLinkService;
            InitializeLayout();
        }

        public int PreferredPageHeight => 1100;

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            await LoadAccountsAsync().ConfigureAwait(true);
            await LoadLinkTypesAsync().ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
        }

        private void InitializeLayout()
        {
            TableLayoutPanel root = new()
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                BackColor = UiTheme.AppBackground
            };

            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
            Controls.Add(root);

            CardPanel filterCard = new()
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(14),
                Margin = new Padding(0, 0, 0, 10)
            };

            root.Controls.Add(filterCard, 0, 0);

            TableLayoutPanel filterLayout = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.CardBackground
            };

            filterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            filterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            filterCard.Controls.Add(filterLayout);

            TableLayoutPanel criteriaRow = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 10,
                RowCount = 1,
                BackColor = UiTheme.CardBackground,
                Margin = new Padding(0, 0, 0, 6)
            };

            criteriaRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66));
            criteriaRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
            criteriaRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 16));
            criteriaRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
            criteriaRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
            criteriaRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 16));
            criteriaRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
            criteriaRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            criteriaRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
            criteriaRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            filterLayout.Controls.Add(criteriaRow, 0, 0);
            criteriaRow.Controls.Add(MakeLabel("Account", 62), 0, 0);
            _accountFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            _accountFilter.Dock = DockStyle.Fill;
            criteriaRow.Controls.Add(_accountFilter, 1, 0);
            criteriaRow.Controls.Add(MakeLabel("Link Type", 78), 3, 0);
            _linkTypeFilter.DropDownStyle = ComboBoxStyle.DropDown;
            _linkTypeFilter.Dock = DockStyle.Fill;
            criteriaRow.Controls.Add(_linkTypeFilter, 4, 0);
            criteriaRow.Controls.Add(MakeLabel("Search", 56), 6, 0);
            _search.Dock = DockStyle.Fill;
            criteriaRow.Controls.Add(_search, 7, 0);
            criteriaRow.Controls.Add(MakeLabel("Max", 38), 8, 0);
            _maxRows.Minimum = 1;
            _maxRows.Maximum = 10000;
            _maxRows.Value = 1000;
            _maxRows.Dock = DockStyle.Fill;
            criteriaRow.Controls.Add(_maxRows, 9, 0);

            TableLayoutPanel actionRow = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1,
                BackColor = UiTheme.CardBackground
            };

            for (int index = 0; index < 5; index++)
            {
                actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            }

            filterLayout.Controls.Add(actionRow, 0, 1);
            Button refresh = MakeActionButton("Refresh", true);
            refresh.Click += async (_, _) => await RefreshAsync().ConfigureAwait(true);
            actionRow.Controls.Add(refresh, 0, 0);
            Button add = MakeActionButton("Add Link", false);
            add.Click += async (_, _) => await AddAsync().ConfigureAwait(true);
            actionRow.Controls.Add(add, 1, 0);
            Button edit = MakeActionButton("Edit Link", false);
            edit.Click += async (_, _) => await EditAsync().ConfigureAwait(true);
            actionRow.Controls.Add(edit, 2, 0);
            Button delete = MakeActionButton("Delete Link", false);
            delete.Click += async (_, _) => await DeleteAsync().ConfigureAwait(true);
            actionRow.Controls.Add(delete, 3, 0);
            Button repair = MakeActionButton("Repair Data Links", false);
            repair.Click += async (_, _) => await RepairDataLinksAsync().ConfigureAwait(true);
            actionRow.Controls.Add(repair, 4, 0);

            CardPanel gridCard = new()
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                Margin = new Padding(0, 0, 0, 10)
            };

            root.Controls.Add(gridCard, 0, 1);
            _grid.Dock = DockStyle.Fill;
            _grid.AutoGenerateColumns = false;
            _grid.AllowUserToAddRows = false;
            _grid.ReadOnly = true;
            _grid.MultiSelect = true;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.DataSource = _bindingSource;
            _grid.CellDoubleClick += async (_, _) => await EditAsync().ConfigureAwait(true);
            _grid.SelectionChanged += (_, _) => UpdateDetails();
            _grid.Columns.Add(MakeColumn("TransactionLinkId", "Link ID", 75));
            _grid.Columns.Add(MakeColumn("LinkType", "Type", 115));
            _grid.Columns.Add(MakeColumn("ConfidenceScore", "Confidence", 85));
            _grid.Columns.Add(MakeColumn("AllocationAmount", "Allocation", 95, "C2"));
            _grid.Columns.Add(MakeColumn("SourceAccountNickname", "Source Account", 150));
            _grid.Columns.Add(MakeColumn("SourceDate", "Source Date", 95));
            _grid.Columns.Add(MakeColumn("SourceAmount", "Source Amount", 105, "C2"));
            _grid.Columns.Add(MakeColumn("SourceDescription", "Source Description", 310));
            _grid.Columns.Add(MakeColumn("LinkedAccountNickname", "Linked Account", 150));
            _grid.Columns.Add(MakeColumn("LinkedDate", "Linked Date", 95));
            _grid.Columns.Add(MakeColumn("LinkedAmount", "Linked Amount", 105, "C2"));
            _grid.Columns.Add(MakeColumn("LinkedDescription", "Linked Description", 310));
            _grid.Columns.Add(MakeColumn("Notes", "Notes", 260));
            gridCard.Controls.Add(_grid);

            TableLayoutPanel details = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.AppBackground
            };

            details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            root.Controls.Add(details, 0, 2);
            details.Controls.Add(BuildDetailsCard("Source transaction", _sourceDetails), 0, 0);
            details.Controls.Add(BuildDetailsCard("Linked transaction", _linkedDetails), 1, 0);
            UiTheme.ApplyTo(this);
        }

        private static CardPanel BuildDetailsCard(string title, TextBox textBox)
        {
            CardPanel card = new()
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                Margin = new Padding(0, 0, 10, 0)
            };

            TableLayoutPanel layout = new()
            {
                Dock = DockStyle.Fill,
                RowCount = 2
            };

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            card.Controls.Add(layout);

            Label label = new()
            {
                Text = title,
                Font = UiTheme.SectionTitleFont,
                Dock = DockStyle.Fill
            };

            layout.Controls.Add(label, 0, 0);
            textBox.Dock = DockStyle.Fill;
            textBox.Multiline = true;
            textBox.ReadOnly = true;
            textBox.ScrollBars = ScrollBars.Vertical;
            textBox.BorderStyle = BorderStyle.None;
            layout.Controls.Add(textBox, 0, 1);
            return card;
        }

        private static Button MakeActionButton(string text, bool primary)
        {
            return new Button
            {
                Text = text,
                Dock = DockStyle.Fill,
                Height = 34,
                Margin = new Padding(4, 3, 4, 3),
                Tag = primary ? "PrimaryButton" : null
            };
        }

        private static Label MakeLabel(string text, int width)
        {
            return new Label
            {
                Text = text,
                Width = width,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(4, 9, 2, 2)
            };
        }

        private static DataGridViewTextBoxColumn MakeColumn(string propertyName, string headerText, int width, string? format = null)
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

            return column;
        }

        private async Task LoadAccountsAsync()
        {
            _accounts = await _transactionLinkService.GetAccountsAsync().ConfigureAwait(true);
            List<AccountOption> options = new() { new AccountOption(null, "All Accounts") };
            options.AddRange(_accounts.Select(account => new AccountOption(account.AccountId, account.DisplayName)));
            _accountFilter.DataSource = options;
            _accountFilter.DisplayMember = nameof(AccountOption.DisplayName);
            _accountFilter.ValueMember = nameof(AccountOption.AccountId);
        }

        private async Task LoadLinkTypesAsync()
        {
            string current = _linkTypeFilter.Text;
            IReadOnlyList<string> linkTypes = await _transactionLinkService.GetLinkTypesAsync().ConfigureAwait(true);
            _linkTypeFilter.Items.Clear();
            _linkTypeFilter.Items.Add(string.Empty);

            foreach (string linkType in linkTypes.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                _linkTypeFilter.Items.Add(linkType);
            }

            if (_linkTypeFilter.Items.Count == 1)
            {
                _linkTypeFilter.Items.Add("PayPalFunding");
                _linkTypeFilter.Items.Add("CreditCardPaymentFunding");
                _linkTypeFilter.Items.Add("CreditCardPaymentAllocation");
                _linkTypeFilter.Items.Add(TransactionLinkTypes.BankTransfer);
                _linkTypeFilter.Items.Add(TransactionLinkTypes.PurchaseMirror);
                _linkTypeFilter.Items.Add(TransactionLinkTypes.PurchaseFundingAllocation);
                _linkTypeFilter.Items.Add(TransactionLinkTypes.PurchaseRefund);
                _linkTypeFilter.Items.Add("DebtPayment");
                _linkTypeFilter.Items.Add("ManualReview");
            }

            _linkTypeFilter.Text = current;
        }

        private async Task RefreshAsync()
        {
            AccountOption? account = _accountFilter.SelectedItem as AccountOption;

            TransactionLinkFilter filter = new()
            {
                AccountId = account?.AccountId,
                LinkType = string.IsNullOrWhiteSpace(_linkTypeFilter.Text) ? null : _linkTypeFilter.Text.Trim(),
                SearchText = _search.Text,
                MaxRows = decimal.ToInt32(_maxRows.Value)
            };

            IReadOnlyList<TransactionLinkRecord> links = await _transactionLinkService.GetLinksAsync(filter).ConfigureAwait(true);
            _bindingSource.DataSource = links;
            UpdateDetails();
        }

        private async Task AddAsync()
        {
            TransactionLinkEditModel model = new();
            using TransactionLinkEditForm form = new(_transactionLinkService, model);

            if (form.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            await _transactionLinkService.SaveAsync(form.LinkModel).ConfigureAwait(true);
            await LoadLinkTypesAsync().ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
        }

        private async Task EditAsync()
        {
            if (_bindingSource.Current is not TransactionLinkRecord selected)
            {
                return;
            }

            TransactionLinkEditModel model = new()
            {
                TransactionLinkId = selected.TransactionLinkId,
                SourceTransactionId = selected.SourceTransactionId,
                LinkedTransactionId = selected.LinkedTransactionId,
                LinkType = selected.LinkType,
                AllocationAmount = selected.AllocationAmount,
                ConfidenceScore = selected.ConfidenceScore,
                Notes = selected.Notes
            };

            using TransactionLinkEditForm form = new(_transactionLinkService, model);

            if (form.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            await _transactionLinkService.SaveAsync(form.LinkModel).ConfigureAwait(true);
            await LoadLinkTypesAsync().ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
        }

        private async Task DeleteAsync()
        {
            List<TransactionLinkRecord> selected = GetSelectedLinks();

            if (selected.Count == 0)
            {
                return;
            }

            DialogResult result = MessageBox.Show(this, $"Delete {selected.Count} selected link(s)? This removes the relationship, recalculates reporting, and prevents this pair from being linked automatically again. Both transactions are kept.", "Delete Links", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
            {
                return;
            }

            foreach (TransactionLinkRecord link in selected)
            {
                await _transactionLinkService.DeleteAsync(link.TransactionLinkId).ConfigureAwait(true);
            }

            await RefreshAsync().ConfigureAwait(true);
        }

        private async Task RepairDataLinksAsync()
        {
            DataFlowRepairSummary summary = await _transactionLinkService.RepairDataFlowAsync().ConfigureAwait(true);
            MessageBox.Show(this, $"Reconciliation complete. Purchase mirrors: {summary.LinkedFundingRows}. Debt/card payment funding links: {summary.CreditCardFundingLinks}. Internal transfers: {summary.BankTransferLinks}. Only unambiguous explicit account references are linked automatically. Use Add Link for combined funding, split payments, or missing references.", "Transaction Links", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await LoadLinkTypesAsync().ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
        }

        private List<TransactionLinkRecord> GetSelectedLinks()
        {
            if (_grid.SelectedRows.Count == 0 && _bindingSource.Current is TransactionLinkRecord current)
            {
                return new List<TransactionLinkRecord> { current };
            }

            return _grid.SelectedRows.Cast<DataGridViewRow>().Select(row => row.DataBoundItem).OfType<TransactionLinkRecord>().DistinctBy(link => link.TransactionLinkId).ToList();
        }

        private void UpdateDetails()
        {
            if (_bindingSource.Current is not TransactionLinkRecord link)
            {
                _sourceDetails.Clear();
                _linkedDetails.Clear();
                return;
            }

            _sourceDetails.Text = FormatTransaction(link.SourceTransactionId, link.SourceAccountNickname, link.SourceDate, link.SourceAmount, link.SourceType, link.SourceDescription, link.SourceCategory, link.SourceCategoryType, link.SourceFundingSource, link.SourceExternalTransactionId);
            _linkedDetails.Text = FormatTransaction(link.LinkedTransactionId, link.LinkedAccountNickname, link.LinkedDate, link.LinkedAmount, link.LinkedType, link.LinkedDescription, link.LinkedCategory, link.LinkedCategoryType, link.LinkedFundingSource, link.LinkedExternalTransactionId);
        }

        private static string FormatTransaction(long transactionId, string account, DateOnly date, decimal amount, string type, string description, string? category, string? categoryType, string? fundingSource, string? externalId)
        {
            StringBuilder builder = new();
            builder.AppendLine($"Transaction ID: {transactionId}");
            builder.AppendLine($"Account: {account}");
            builder.AppendLine($"Date: {date:yyyy-MM-dd}");
            builder.AppendLine($"Amount: {amount:C2}");
            builder.AppendLine($"Type: {type}");
            builder.AppendLine($"Category: {category ?? string.Empty}");
            builder.AppendLine($"Category Type: {categoryType ?? string.Empty}");
            builder.AppendLine($"Funding Source: {fundingSource ?? string.Empty}");
            builder.AppendLine($"External ID: {externalId ?? string.Empty}");
            builder.AppendLine();
            builder.AppendLine(description);
            return builder.ToString();
        }

        private sealed record AccountOption(int? AccountId, string DisplayName);
    }
}
