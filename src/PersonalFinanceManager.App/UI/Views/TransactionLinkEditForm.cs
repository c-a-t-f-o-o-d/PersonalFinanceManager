using System.Globalization;
using PersonalFinanceManager.App.UI.Styling;
using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Application.Services.Contracts;

namespace PersonalFinanceManager.App.UI.Views
{
    public sealed class TransactionLinkEditForm : Form
    {
        private readonly ITransactionLinkWorkflowService _transactionLinkService;
        private readonly TextBox _sourceId = new();
        private readonly TextBox _linkedId = new();
        private readonly ComboBox _linkType = new();
        private readonly NumericUpDown _allocationAmount = new();
        private readonly NumericUpDown _confidence = new();
        private readonly TextBox _notes = new();
        private readonly ComboBox _searchAccount = new();
        private readonly TextBox _searchText = new();
        private readonly NumericUpDown _maxRows = new();
        private readonly BindingSource _transactionSource = new();
        private readonly DataGridView _transactionGrid = new();
        private IReadOnlyList<Account> _accounts = Array.Empty<Account>();

        public TransactionLinkEditForm(ITransactionLinkWorkflowService transactionLinkService, TransactionLinkEditModel model)
        {
            _transactionLinkService = transactionLinkService;
            LinkModel = model;
            InitializeLayout();
            LoadModel();
        }

        public TransactionLinkEditModel LinkModel { get; }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            await LoadAccountsAsync().ConfigureAwait(true);
            await SearchTransactionsAsync().ConfigureAwait(true);
        }

        private void InitializeLayout()
        {
            Text = LinkModel.TransactionLinkId == 0 ? "Add Transaction Link" : "Edit Transaction Link";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1180, 760);
            MinimumSize = new Size(980, 620);
            Font = UiTheme.BodyFont;
            BackColor = UiTheme.AppBackground;

            TableLayoutPanel root = new()
            {
                Dock = DockStyle.Fill,
                RowCount = 5,
                Padding = new Padding(14),
                BackColor = UiTheme.AppBackground
            };

            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 122));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
            Controls.Add(root);

            TableLayoutPanel fields = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 3
            };

            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            root.Controls.Add(fields, 0, 0);
            AddField(fields, "Source Txn ID", _sourceId, 0, 0);
            AddField(fields, "Linked Txn ID", _linkedId, 2, 0);
            AddField(fields, "Link Type", _linkType, 0, 1);
            _linkType.DropDownStyle = ComboBoxStyle.DropDownList;
            _linkType.Items.AddRange(TransactionLinkTypes.All.Cast<object>().ToArray());
            AddField(fields, "Confidence", _confidence, 2, 1);
            _confidence.Minimum = 0;
            _confidence.Maximum = 100;
            _confidence.Value = 100;
            AddField(fields, "Allocation", _allocationAmount, 0, 2);
            _allocationAmount.DecimalPlaces = 2;
            _allocationAmount.Maximum = 100000000;
            _allocationAmount.Width = 120;
            fields.Controls.Add(MakeLabel("Notes"), 2, 2);
            _notes.Multiline = true;
            _notes.MaxLength = 2000;
            _notes.ScrollBars = ScrollBars.Vertical;
            _notes.Dock = DockStyle.Fill;
            fields.Controls.Add(_notes, 3, 2);

            FlowLayoutPanel searchRow = new()
            {
                Dock = DockStyle.Fill
            };

            root.Controls.Add(searchRow, 0, 1);
            searchRow.Controls.Add(MakeInlineLabel("Account", 58));
            _searchAccount.DropDownStyle = ComboBoxStyle.DropDownList;
            _searchAccount.Width = 250;
            searchRow.Controls.Add(_searchAccount);
            searchRow.Controls.Add(MakeInlineLabel("Search", 55));
            _searchText.Width = 320;
            searchRow.Controls.Add(_searchText);
            searchRow.Controls.Add(MakeInlineLabel("Max", 35));
            _maxRows.Minimum = 50;
            _maxRows.Maximum = 5000;
            _maxRows.Value = 500;
            _maxRows.Width = 80;
            searchRow.Controls.Add(_maxRows);
            Button search = new() { Text = "Search", Width = 90 };
            search.Click += async (_, _) => await SearchTransactionsAsync().ConfigureAwait(true);
            searchRow.Controls.Add(search);
            Button setSource = new() { Text = "Set as Source", Width = 120 };
            setSource.Click += (_, _) => SetSelectedTransactionId(_sourceId);
            searchRow.Controls.Add(setSource);
            Button setLinked = new() { Text = "Set as Linked", Width = 120 };
            setLinked.Click += (_, _) => SetSelectedTransactionId(_linkedId);
            searchRow.Controls.Add(setLinked);
            _transactionGrid.Dock = DockStyle.Fill;
            _transactionGrid.AutoGenerateColumns = false;
            _transactionGrid.AllowUserToAddRows = false;
            _transactionGrid.ReadOnly = true;
            _transactionGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _transactionGrid.MultiSelect = false;
            _transactionGrid.DataSource = _transactionSource;
            _transactionGrid.CellDoubleClick += (_, _) => SetSelectedTransactionId(_sourceId);
            _transactionGrid.Columns.Add(MakeColumn("ImportedTransactionId", "Txn ID", 80));
            _transactionGrid.Columns.Add(MakeColumn("TransactionDate", "Date", 95));
            _transactionGrid.Columns.Add(MakeColumn("AccountNickname", "Account", 145));
            _transactionGrid.Columns.Add(MakeColumn("Amount", "Amount", 95, "C2"));
            _transactionGrid.Columns.Add(MakeColumn("Type", "Type", 100));
            _transactionGrid.Columns.Add(MakeColumn("Description", "Description", 360));
            _transactionGrid.Columns.Add(MakeColumn("Category", "Category", 130));
            _transactionGrid.Columns.Add(MakeColumn("CategoryType", "Category Type", 130));
            _transactionGrid.Columns.Add(MakeColumn("FundingSource", "Funding Source", 170));
            root.Controls.Add(_transactionGrid, 0, 2);

            FlowLayoutPanel buttons = new()
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };

            root.Controls.Add(buttons, 0, 3);
            Button cancel = new() { Text = "Cancel", Width = 100 };
            cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(cancel);
            Button save = new() { Text = "Save", Width = 100, Tag = "PrimaryButton" };
            save.Click += (_, _) => Save();
            buttons.Controls.Add(save);

            Label guidance = new()
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                Text = "PurchaseMirror: original wallet purchase → same signed purchase on the funding account. " +
                    "PurchaseFundingAllocation: wallet purchase → combined bank/card debit; enter the portion funded and repeat for each purchase. " +
                    "CreditCardPaymentFunding: positive credit/Smart Connect payment → equal negative bank payment; it may settle many purchases. " +
                    "BankTransfer: equal opposite amounts between owned cash accounts. Dates may differ; verify the statements before saving."
            };

            root.Controls.Add(guidance, 0, 4);
            UiTheme.ApplyTo(this);
        }

        private static void AddField(TableLayoutPanel fields, string label, Control control, int column, int row)
        {
            fields.Controls.Add(MakeLabel(label), column, row);
            control.Dock = DockStyle.Fill;
            fields.Controls.Add(control, column + 1, row);
        }

        private static Label MakeLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static Label MakeInlineLabel(string text, int width)
        {
            return new Label
            {
                Text = text,
                Width = width,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(4, 8, 2, 2)
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

        private void LoadModel()
        {
            _sourceId.Text = LinkModel.SourceTransactionId > 0 ? LinkModel.SourceTransactionId.ToString(CultureInfo.InvariantCulture) : string.Empty;
            _linkedId.Text = LinkModel.LinkedTransactionId > 0 ? LinkModel.LinkedTransactionId.ToString(CultureInfo.InvariantCulture) : string.Empty;
            _linkType.Text = string.IsNullOrWhiteSpace(LinkModel.LinkType) ? TransactionLinkTypes.PurchaseMirror : TransactionLinkTypes.Normalize(LinkModel.LinkType);
            _allocationAmount.Value = LinkModel.AllocationAmount.HasValue ? Math.Clamp(LinkModel.AllocationAmount.Value, 0M, _allocationAmount.Maximum) : 0M;
            _confidence.Value = Math.Clamp(LinkModel.ConfidenceScore, 0, 100);
            _notes.Text = LinkModel.Notes ?? string.Empty;
        }

        private async Task LoadAccountsAsync()
        {
            _accounts = await _transactionLinkService.GetAccountsAsync().ConfigureAwait(true);
            List<AccountOption> options = new() { new AccountOption(null, "All Accounts") };
            options.AddRange(_accounts.Select(account => new AccountOption(account.AccountId, account.DisplayName)));
            _searchAccount.DataSource = options;
            _searchAccount.DisplayMember = nameof(AccountOption.DisplayName);
            _searchAccount.ValueMember = nameof(AccountOption.AccountId);
        }

        private async Task SearchTransactionsAsync()
        {
            AccountOption? option = _searchAccount.SelectedItem as AccountOption;
            IReadOnlyList<TransactionLinkTransactionOption> transactions = await _transactionLinkService.GetTransactionOptionsAsync(option?.AccountId, _searchText.Text, decimal.ToInt32(_maxRows.Value)).ConfigureAwait(true);
            _transactionSource.DataSource = transactions;
        }

        private void SetSelectedTransactionId(TextBox target)
        {
            if (_transactionSource.Current is not TransactionLinkTransactionOption transaction)
            {
                return;
            }

            target.Text = transaction.ImportedTransactionId.ToString(CultureInfo.InvariantCulture);
        }

        private void Save()
        {
            if (!long.TryParse(_sourceId.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long sourceId))
            {
                MessageBox.Show(this, "Enter a valid source transaction ID.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!long.TryParse(_linkedId.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long linkedId))
            {
                MessageBox.Show(this, "Enter a valid linked transaction ID.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            LinkModel.SourceTransactionId = sourceId;
            LinkModel.LinkedTransactionId = linkedId;
            LinkModel.LinkType = _linkType.Text.Trim();
            LinkModel.AllocationAmount = _allocationAmount.Value > 0 ? _allocationAmount.Value : null;
            LinkModel.ConfidenceScore = decimal.ToInt32(_confidence.Value);
            LinkModel.Notes = string.IsNullOrWhiteSpace(_notes.Text) ? null : _notes.Text.Trim();
            DialogResult = DialogResult.OK;
        }

        private sealed record AccountOption(int? AccountId, string DisplayName);
    }
}
