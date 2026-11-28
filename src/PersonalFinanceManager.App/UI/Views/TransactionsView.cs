using PersonalFinanceManager.App.UI.Controls;
using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Application.Services.Contracts;

namespace PersonalFinanceManager.App.UI.Views
{
    public sealed class TransactionsView : UserControl, IPageLayoutControl
    {
        private readonly ITransactionWorkflowService _transactionWorkflowService;
        private readonly BindingSource _bindingSource = new();
        private readonly ComboBox _accountFilter = new();
        private readonly DateTimePicker _fromDate = new();
        private readonly DateTimePicker _toDate = new();
        private readonly TextBox _search = new();
        private readonly ComboBox _category = new();
        private readonly ComboBox _categoryType = new();
        private readonly CheckBox _uncategorizedOnly = new() { Text = "Uncategorized only", AutoSize = true };
        private readonly ComboBox _incomeFilter = new();
        private readonly ComboBox _transferFilter = new();
        private readonly ComboBox _manualFilter = new();
        private readonly ComboBox _incomeOwnerFilter = new();
        private readonly NumericUpDown _maxRows = new();
        private readonly ComboBox _quickCategory = new();
        private readonly ComboBox _quickCategoryType = new();
        private readonly ComboBox _quickRuleMode = new();
        private readonly DataGridView _grid = new();
        private IReadOnlyList<Account> _accounts = Array.Empty<Account>();
        private IReadOnlyList<string> _categories = Array.Empty<string>();
        private IReadOnlyList<string> _categoryTypes = Array.Empty<string>();

        public TransactionsView(ITransactionWorkflowService transactionWorkflowService)
        {
            _transactionWorkflowService = transactionWorkflowService ?? throw new ArgumentNullException(nameof(transactionWorkflowService));
            InitializeLayout();
        }

        public int PreferredPageHeight => 1050;

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            await LoadAccountsAsync().ConfigureAwait(true);
            await LoadCategoryOptionsAsync().ConfigureAwait(true);
            await LoadTransactionsAsync().ConfigureAwait(true);
        }

        private void InitializeLayout()
        {
            TableLayoutPanel root = new()
            {
                Dock = DockStyle.Fill,
                RowCount = 5
            };

            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);
            FlowLayoutPanel commandRow = new() { Dock = DockStyle.Fill };
            root.Controls.Add(commandRow, 0, 0);
            Button add = new() { Text = "Add", Width = 90 };
            add.Click += async (_, _) => await AddAsync().ConfigureAwait(true);
            commandRow.Controls.Add(add);
            Button edit = new() { Text = "Edit", Width = 90 };
            edit.Click += async (_, _) => await EditAsync().ConfigureAwait(true);
            commandRow.Controls.Add(edit);
            Button delete = new() { Text = "Delete", Width = 90 };
            delete.Click += async (_, _) => await DeleteAsync().ConfigureAwait(true);
            commandRow.Controls.Add(delete);
            Button refresh = new() { Text = "Refresh", Width = 90 };
            refresh.Click += async (_, _) => await RefreshAsync().ConfigureAwait(true);
            commandRow.Controls.Add(refresh);
            Button clearFilters = new() { Text = "Clear Filters", Width = 110 };
            clearFilters.Click += async (_, _) => await ClearFiltersAsync().ConfigureAwait(true);
            commandRow.Controls.Add(clearFilters);
            Button showUncategorized = new() { Text = "Show Uncategorized", Width = 145 };
            showUncategorized.Click += async (_, _) => await ShowUncategorizedAsync().ConfigureAwait(true);
            commandRow.Controls.Add(showUncategorized);
            FlowLayoutPanel filterRow1 = new() { Dock = DockStyle.Fill };
            root.Controls.Add(filterRow1, 0, 1);
            filterRow1.Controls.Add(MakeLabel("Account", 60));
            _accountFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            _accountFilter.Width = 260;
            filterRow1.Controls.Add(_accountFilter);
            filterRow1.Controls.Add(MakeLabel("From", 42));
            ConfigureDatePicker(_fromDate);
            filterRow1.Controls.Add(_fromDate);
            filterRow1.Controls.Add(MakeLabel("To", 28));
            ConfigureDatePicker(_toDate);
            filterRow1.Controls.Add(_toDate);
            filterRow1.Controls.Add(MakeLabel("Search", 55));
            _search.Width = 220;
            filterRow1.Controls.Add(_search);
            FlowLayoutPanel filterRow2 = new() { Dock = DockStyle.Fill };
            root.Controls.Add(filterRow2, 0, 2);
            filterRow2.Controls.Add(MakeLabel("Category", 70));
            ConfigureFreeTextCombo(_category);
            _category.Width = 160;
            filterRow2.Controls.Add(_category);
            filterRow2.Controls.Add(MakeLabel("Category Type", 105));
            ConfigureFreeTextCombo(_categoryType);
            _categoryType.Width = 160;
            filterRow2.Controls.Add(_categoryType);
            _uncategorizedOnly.Margin = new Padding(8, 7, 8, 3);
            filterRow2.Controls.Add(_uncategorizedOnly);
            AddBooleanFilter(filterRow2, "Income", _incomeFilter);
            AddBooleanFilter(filterRow2, "Transfer", _transferFilter);
            AddBooleanFilter(filterRow2, "Manual", _manualFilter);
            filterRow2.Controls.Add(MakeLabel("Owner", 54));
            _incomeOwnerFilter.Width = 110;
            _incomeOwnerFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            _incomeOwnerFilter.Items.AddRange(new object[] { "All", OwnerValues.Unassigned, OwnerValues.Me, OwnerValues.Spouse, OwnerValues.Joint, OwnerValues.Household });
            _incomeOwnerFilter.SelectedIndex = 0;
            filterRow2.Controls.Add(_incomeOwnerFilter);
            filterRow2.Controls.Add(MakeLabel("Max", 40));
            _maxRows.Minimum = 1;
            _maxRows.Maximum = 10000;
            _maxRows.Value = 1000;
            _maxRows.Width = 80;
            filterRow2.Controls.Add(_maxRows);
            Button apply = new() { Text = "Apply", Width = 90 };
            apply.Click += async (_, _) => await LoadTransactionsAsync().ConfigureAwait(true);
            filterRow2.Controls.Add(apply);
            FlowLayoutPanel quickRow = new() { Dock = DockStyle.Fill };
            root.Controls.Add(quickRow, 0, 3);
            quickRow.Controls.Add(MakeLabel("Quick Category", 105));
            ConfigureFreeTextCombo(_quickCategory);
            _quickCategory.Width = 180;
            quickRow.Controls.Add(_quickCategory);
            quickRow.Controls.Add(MakeLabel("Category Type", 105));
            ConfigureFreeTextCombo(_quickCategoryType);
            _quickCategoryType.Width = 180;
            quickRow.Controls.Add(_quickCategoryType);
            quickRow.Controls.Add(MakeLabel("Rule Match", 80));
            _quickRuleMode.DropDownStyle = ComboBoxStyle.DropDownList;
            _quickRuleMode.Items.AddRange(new object[] { RuleMatchModes.Exact, RuleMatchModes.StartsWith, RuleMatchModes.Contains });
            _quickRuleMode.SelectedIndex = 0;
            _quickRuleMode.Width = 110;
            quickRow.Controls.Add(_quickRuleMode);
            Button applyCategory = new() { Text = "Apply to Selected", Width = 135 };
            applyCategory.Click += async (_, _) => await ApplyQuickCategorizationAsync().ConfigureAwait(true);
            quickRow.Controls.Add(applyCategory);
            Button createRule = new() { Text = "Create Rule + Apply", Width = 155 };
            createRule.Click += async (_, _) => await CreateRuleAndApplyAsync().ConfigureAwait(true);
            quickRow.Controls.Add(createRule);
            _grid.Dock = DockStyle.Fill;
            _grid.AutoGenerateColumns = false;
            _grid.AllowUserToAddRows = false;
            _grid.ReadOnly = true;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = true;
            _grid.DataSource = _bindingSource;
            _grid.CellDoubleClick += async (_, _) => await EditAsync().ConfigureAwait(true);
            _grid.SelectionChanged += (_, _) => PopulateQuickFieldsFromSelection();
            AddColumn("TransactionDate", "Date", 95);
            AddColumn("TransactionTime", "Time", 80);
            AddColumn("AccountNickname", "Account", 150);
            AddColumn("Amount", "Amount", 100, "C2");
            AddColumn("Type", "Type", 100);
            AddColumn("Description", "Description", 320);
            AddColumn("Category", "Category", 150);
            AddColumn("SpendingClass", "Category Type", 140);
            AddColumn("Merchant", "Merchant", 160);
            AddColumn("IsIncome", "Income", 70);
            AddColumn("IncomeOwner", "Income Owner", 110);
            AddColumn("IsTransfer", "Transfer", 70);
            AddColumn("IsManual", "Manual", 70);
            AddColumn("Notes", "Notes", 220);
            root.Controls.Add(_grid, 0, 4);
            PageLayout.AutoSizeHeaders(root, 0, 1, 2, 3);
        }

        private static Label MakeLabel(string text, int width)
        {
            return new Label
            {
                Text = text,
                Width = width,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(3, 6, 3, 3)
            };
        }

        private static void ConfigureDatePicker(DateTimePicker picker)
        {
            picker.Width = 120;
            picker.Format = DateTimePickerFormat.Short;
            picker.ShowCheckBox = true;
            picker.Checked = false;
        }

        private static void ConfigureFreeTextCombo(ComboBox comboBox)
        {
            comboBox.DropDownStyle = ComboBoxStyle.DropDown;
            comboBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            comboBox.AutoCompleteSource = AutoCompleteSource.ListItems;
        }

        private static void AddBooleanFilter(FlowLayoutPanel panel, string label, ComboBox comboBox)
        {
            panel.Controls.Add(MakeLabel(label, 62));
            comboBox.Width = 80;
            comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox.Items.AddRange(new object[] { "All", "Yes", "No" });
            comboBox.SelectedIndex = 0;
            panel.Controls.Add(comboBox);
        }

        private void AddColumn(string property, string header, int width, string? format = null)
        {
            DataGridViewTextBoxColumn column = new()
            {
                DataPropertyName = property,
                HeaderText = header,
                Width = width
            };

            if (!string.IsNullOrWhiteSpace(format))
            {
                column.DefaultCellStyle.Format = format;
            }

            _grid.Columns.Add(column);
        }

        private async Task LoadAccountsAsync()
        {
            _accounts = await _transactionWorkflowService.GetAccountsAsync().ConfigureAwait(true);
            List<AccountOption> options = new() { new AccountOption(null, "All Accounts") };
            options.AddRange(_accounts.Select(account => new AccountOption(account.AccountId, account.DisplayName)));
            _accountFilter.DataSource = options;
            _accountFilter.DisplayMember = nameof(AccountOption.DisplayName);
            _accountFilter.ValueMember = nameof(AccountOption.AccountId);
        }

        private async Task LoadCategoryOptionsAsync()
        {
            _categories = await _transactionWorkflowService.GetCategoryValuesAsync().ConfigureAwait(true);
            _categoryTypes = await _transactionWorkflowService.GetCategoryTypeValuesAsync().ConfigureAwait(true);
            LoadComboValues(_category, _categories);
            LoadComboValues(_quickCategory, _categories);
            LoadComboValues(_categoryType, _categoryTypes);
            LoadComboValues(_quickCategoryType, _categoryTypes);
        }

        private static void LoadComboValues(ComboBox comboBox, IReadOnlyList<string> values)
        {
            string current = comboBox.Text;
            comboBox.Items.Clear();

            foreach (string value in values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value))
            {
                comboBox.Items.Add(value);
            }

            comboBox.Text = current;
        }

        private async Task RefreshAsync()
        {
            await LoadCategoryOptionsAsync().ConfigureAwait(true);
            await LoadTransactionsAsync().ConfigureAwait(true);
        }

        private async Task LoadTransactionsAsync()
        {
            TransactionFilter filter = BuildFilter();
            IReadOnlyList<ImportedTransaction> transactions = await _transactionWorkflowService.GetTransactionsAsync(filter).ConfigureAwait(true);
            _bindingSource.DataSource = transactions;
        }

        private TransactionFilter BuildFilter()
        {
            return new TransactionFilter
            {
                AccountId = _accountFilter.SelectedValue is int accountId ? accountId : null,
                FromDate = _fromDate.Checked ? DateOnly.FromDateTime(_fromDate.Value.Date) : null,
                ToDate = _toDate.Checked ? DateOnly.FromDateTime(_toDate.Value.Date) : null,
                SearchText = EmptyToNull(_search.Text),
                Category = EmptyToNull(_category.Text),
                SpendingClass = EmptyToNull(_categoryType.Text),
                UncategorizedOnly = _uncategorizedOnly.Checked,
                IsIncome = BooleanFilterValue(_incomeFilter),
                IsTransfer = BooleanFilterValue(_transferFilter),
                IsManual = BooleanFilterValue(_manualFilter),
                IncomeOwner = string.Equals(_incomeOwnerFilter.Text, "All", StringComparison.OrdinalIgnoreCase) ? null : EmptyToNull(_incomeOwnerFilter.Text),
                MaxRows = Convert.ToInt32(_maxRows.Value)
            };
        }

        private async Task ClearFiltersAsync()
        {
            _accountFilter.SelectedIndex = 0;
            _fromDate.Checked = false;
            _toDate.Checked = false;
            _search.Clear();
            _category.Text = string.Empty;
            _categoryType.Text = string.Empty;
            _uncategorizedOnly.Checked = false;
            _incomeFilter.SelectedIndex = 0;
            _transferFilter.SelectedIndex = 0;
            _manualFilter.SelectedIndex = 0;
            _incomeOwnerFilter.SelectedIndex = 0;
            _maxRows.Value = 1000;
            await LoadTransactionsAsync().ConfigureAwait(true);
        }

        private async Task ShowUncategorizedAsync()
        {
            _category.Text = string.Empty;
            _categoryType.Text = string.Empty;
            _uncategorizedOnly.Checked = true;
            await LoadTransactionsAsync().ConfigureAwait(true);
        }

        private async Task AddAsync()
        {
            if (_accounts.Count == 0)
            {
                MessageBox.Show(this, "Add an account before adding transactions.", "Transactions", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ImportedTransaction transaction = new()
            {
                AccountId = GetSelectedAccountId() ?? _accounts[0].AccountId,
                TransactionDate = DateOnly.FromDateTime(DateTime.Today),
                Type = "Withdrawal",
                Description = "Manual transaction",
                IsManual = true
            };

            using TransactionEditForm form = new(transaction, _accounts, _categories, _categoryTypes);

            if (form.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            await _transactionWorkflowService.SaveTransactionAsync(form.Transaction).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
        }

        private async Task EditAsync()
        {
            if (_bindingSource.Current is not ImportedTransaction selected)
            {
                return;
            }

            ImportedTransaction? full = await _transactionWorkflowService.GetTransactionAsync(selected.ImportedTransactionId).ConfigureAwait(true);

            if (full is null)
            {
                MessageBox.Show(this, "The selected transaction no longer exists.", "Transactions", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await LoadTransactionsAsync().ConfigureAwait(true);
                return;
            }

            using TransactionEditForm form = new(full, _accounts, _categories, _categoryTypes);

            if (form.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            await _transactionWorkflowService.SaveTransactionAsync(form.Transaction).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
        }

        private async Task DeleteAsync()
        {
            if (_bindingSource.Current is not ImportedTransaction selected)
            {
                return;
            }

            string message = $"Delete transaction '{selected.Description}' for {selected.Amount:C2} on {selected.TransactionDate}?";
            DialogResult confirm = MessageBox.Show(this, message, "Delete Transaction", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes)
            {
                return;
            }

            await _transactionWorkflowService.DeleteTransactionAsync(selected.ImportedTransactionId).ConfigureAwait(true);
            await LoadTransactionsAsync().ConfigureAwait(true);
        }

        private async Task ApplyQuickCategorizationAsync()
        {
            IReadOnlyList<ImportedTransaction> selected = GetSelectedTransactions();

            if (selected.Count == 0)
            {
                MessageBox.Show(this, "Select one or more transactions first.", "Categorize Transactions", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string? category = EmptyToNull(_quickCategory.Text);
            string? categoryType = EmptyToNull(_quickCategoryType.Text);

            if (category is null && categoryType is null)
            {
                MessageBox.Show(this, "Enter a category or category type.", "Categorize Transactions", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int count = await _transactionWorkflowService.UpdateCategorizationAsync(selected.Select(transaction => transaction.ImportedTransactionId), category, categoryType).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
            MessageBox.Show(this, $"Updated {count} transaction(s).", "Categorize Transactions", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async Task CreateRuleAndApplyAsync()
        {
            IReadOnlyList<ImportedTransaction> selected = GetSelectedTransactions();

            if (selected.Count == 0)
            {
                MessageBox.Show(this, "Select a transaction to use as the rule example.", "Create Rule", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ImportedTransaction example = selected[0];
            string? category = EmptyToNull(_quickCategory.Text) ?? example.Category;
            string? categoryType = EmptyToNull(_quickCategoryType.Text) ?? example.SpendingClass;

            if (category is null && categoryType is null)
            {
                MessageBox.Show(this, "Enter or select a category before creating a rule.", "Create Rule", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            FinancialRule rule = new()
            {
                RuleName = BuildRuleName(example, category, categoryType),
                Priority = 1000,
                MatchDescription = example.Description,
                MatchType = example.Type,
                DescriptionMatchMode = _quickRuleMode.SelectedItem?.ToString() ?? RuleMatchModes.Exact,
                TypeMatchMode = RuleMatchModes.Exact,
                Category = category,
                SpendingClass = categoryType,
                Merchant = example.Merchant,
                IsIncome = null,
                IsTransfer = null,
                IsActive = true,
                Notes = "Created from the Transactions quick categorize workflow."
            };

            using QuickRuleForm form = new(rule, _categories, _categoryTypes);

            if (form.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            int count = await _transactionWorkflowService.CreateRuleAndApplyAsync(form.Rule).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
            string message = form.Rule.IsActive ? $"Created rule and updated {count} matching transaction(s)." : "Saved an inactive rule. Activate it from Rules when you are ready to use it.";
            MessageBox.Show(this, message, "Create Rule", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private IReadOnlyList<ImportedTransaction> GetSelectedTransactions()
        {
            List<ImportedTransaction> selected = new();

            foreach (DataGridViewRow row in _grid.SelectedRows.Cast<DataGridViewRow>())
            {
                if (row.DataBoundItem is ImportedTransaction transaction)
                {
                    selected.Add(transaction);
                }
            }

            if (selected.Count == 0 && _bindingSource.Current is ImportedTransaction current)
            {
                selected.Add(current);
            }

            return selected;
        }

        private void PopulateQuickFieldsFromSelection()
        {
            if (_bindingSource.Current is not ImportedTransaction selected)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(selected.Category))
            {
                _quickCategory.Text = selected.Category;
            }

            if (!string.IsNullOrWhiteSpace(selected.SpendingClass))
            {
                _quickCategoryType.Text = selected.SpendingClass;
            }
        }

        private int? GetSelectedAccountId()
        {
            return _accountFilter.SelectedValue is int accountId ? accountId : null;
        }

        private static bool? BooleanFilterValue(ComboBox comboBox)
        {
            return comboBox.SelectedItem?.ToString() switch
            {
                "Yes" => true,
                "No" => false,
                _ => null
            };
        }

        private static string BuildRuleName(ImportedTransaction transaction, string? category, string? categoryType)
        {
            string target = string.Join(" / ", new[] { category, categoryType }.Where(value => !string.IsNullOrWhiteSpace(value)));
            return string.IsNullOrWhiteSpace(target) ? transaction.Description : transaction.Description + " -> " + target;
        }

        private static string? EmptyToNull(string value)
        {
            string trimmed = value.Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }

        private sealed class AccountOption
        {
            public AccountOption(int? accountId, string displayName)
            {
                AccountId = accountId;
                DisplayName = displayName;
            }

            public int? AccountId { get; }
            public string DisplayName { get; }
        }
    }
}
