using PersonalFinanceManager.App.UI.Styling;
using System.Globalization;
using PersonalFinanceManager.Core.Models;

namespace PersonalFinanceManager.App.UI.Views
{
    public sealed class TransactionEditForm : ThemedForm
    {
        private readonly ComboBox _account = new();
        private readonly DateTimePicker _date = new();
        private readonly DateTimePicker _time = new();
        private readonly TextBox _amount = new();
        private readonly TextBox _type = new();
        private readonly TextBox _description = new();
        private readonly ComboBox _category = new();
        private readonly ComboBox _categoryType = new();
        private readonly TextBox _merchant = new();
        private readonly CheckBox _isIncome = new();
        private readonly CheckBox _isTransfer = new();
        private readonly CheckBox _isManual = new();
        private readonly CheckBox _preserveClassification = new() { Text = "Keep these choices when rules are reapplied", AutoSize = true };
        private readonly ComboBox _incomeOwner = new();
        private readonly TextBox _notes = new();
        private readonly IReadOnlyList<Account> _accounts;
        private readonly IReadOnlyList<string> _categories;
        private readonly IReadOnlyList<string> _categoryTypes;

        public TransactionEditForm(ImportedTransaction transaction, IReadOnlyList<Account> accounts) : this(transaction, accounts, Array.Empty<string>(), Array.Empty<string>()) { }

        public TransactionEditForm(ImportedTransaction transaction, IReadOnlyList<Account> accounts, IReadOnlyList<string> categories, IReadOnlyList<string> categoryTypes)
        {
            Transaction = transaction;
            _accounts = accounts;
            _categories = categories;
            _categoryTypes = categoryTypes;
            InitializeLayout();
            LoadValues();
        }

        public ImportedTransaction Transaction { get; }

        private void InitializeLayout()
        {
            Text = Transaction.ImportedTransactionId == 0 ? "Add Transaction" : "Edit Transaction";
            StartPosition = FormStartPosition.CenterParent;
            Width = 720;
            Height = 720;
            Font = new Font("Segoe UI", 9F);
            MinimizeBox = false;
            MaximizeBox = false;

            TableLayoutPanel root = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Padding = new Padding(12),
                RowCount = 17
            };

            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(root);
            _account.DropDownStyle = ComboBoxStyle.DropDownList;
            _account.DataSource = _accounts.ToList();
            _account.DisplayMember = nameof(Account.DisplayName);
            _account.ValueMember = nameof(Account.AccountId);
            _date.Format = DateTimePickerFormat.Short;
            _time.Format = DateTimePickerFormat.Time;
            _time.ShowUpDown = true;
            _time.ShowCheckBox = true;
            ConfigureFreeTextCombo(_category, _categories);
            ConfigureFreeTextCombo(_categoryType, _categoryTypes);
            _incomeOwner.DropDownStyle = ComboBoxStyle.DropDown;
            _incomeOwner.Items.AddRange(OwnerValues.All);
            _description.Multiline = true;
            _description.Height = 70;
            _notes.Multiline = true;
            _notes.Height = 70;
            AddRow(root, 0, "Account", _account);
            AddRow(root, 1, "Date", _date);
            AddRow(root, 2, "Time", _time);
            AddRow(root, 3, "Amount", _amount);
            AddRow(root, 4, "Type", _type);
            AddRow(root, 5, "Description", _description, 82);
            AddRow(root, 6, "Category", _category);
            AddRow(root, 7, "Category Type", _categoryType);
            AddRow(root, 8, "Merchant", _merchant);
            AddRow(root, 9, "Income", _isIncome);
            AddRow(root, 10, "Income Owner", _incomeOwner);
            AddRow(root, 11, "Transfer", _isTransfer);
            AddRow(root, 12, "Manual", _isManual);
            AddRow(root, 13, "Notes", _notes, 82);
            AddRow(root, 14, "Classification", _preserveClassification);

            FlowLayoutPanel buttons = new()
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };

            Button save = new() { Text = "Save", DialogResult = DialogResult.OK, Width = 100 };
            Button cancel = new() { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 100 };

            save.Click += (_, _) =>
            {
                if (!SaveValues())
                {
                    DialogResult = DialogResult.None;
                }
            };

            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.Controls.Add(buttons, 1, 15);
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            AcceptButton = save;
            CancelButton = cancel;
        }

        private static void ConfigureFreeTextCombo(ComboBox comboBox, IReadOnlyList<string> values)
        {
            comboBox.DropDownStyle = ComboBoxStyle.DropDown;
            comboBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            comboBox.AutoCompleteSource = AutoCompleteSource.ListItems;

            foreach (string value in values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value))
            {
                comboBox.Items.Add(value);
            }
        }

        private static void AddRow(TableLayoutPanel root, int row, string labelText, Control control, int height = 36)
        {
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, height));

            Label label = new()
            {
                Text = labelText,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Margin = new Padding(3, 4, 3, 4)
            };

            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(3, 4, 3, 4);
            root.Controls.Add(label, 0, row);
            root.Controls.Add(control, 1, row);
        }

        private void LoadValues()
        {
            if (Transaction.AccountId > 0)
            {
                _account.SelectedValue = Transaction.AccountId;
            }
            else if (_accounts.Count > 0)
            {
                _account.SelectedIndex = 0;
            }

            DateOnly transactionDate = Transaction.TransactionDate == default ? DateOnly.FromDateTime(DateTime.Today) : Transaction.TransactionDate;
            _date.Value = transactionDate.ToDateTime(TimeOnly.MinValue);

            if (Transaction.TransactionTime.HasValue)
            {
                _time.Checked = true;
                _time.Value = DateTime.Today.Add(Transaction.TransactionTime.Value.ToTimeSpan());
            }
            else
            {
                _time.Checked = false;
            }

            _amount.Text = Transaction.Amount.ToString("0.00", CultureInfo.CurrentCulture);
            _type.Text = string.IsNullOrWhiteSpace(Transaction.Type) ? "Withdrawal" : Transaction.Type;
            _description.Text = Transaction.Description;
            _category.Text = Transaction.Category ?? string.Empty;
            _categoryType.Text = Transaction.SpendingClass ?? string.Empty;
            _merchant.Text = Transaction.Merchant ?? string.Empty;
            _isIncome.Checked = Transaction.IsIncome;
            _incomeOwner.Text = Transaction.IncomeOwner ?? string.Empty;
            _isTransfer.Checked = Transaction.IsTransfer && !Transaction.LegacyLinkTreatment;
            _isManual.Checked = Transaction.ImportedTransactionId == 0 || Transaction.IsManual;
            _preserveClassification.Checked = Transaction.ClassificationIsManual;
            _notes.Text = Transaction.Notes ?? string.Empty;
        }

        private bool SaveValues()
        {
            if (_account.SelectedValue is not int accountId || accountId <= 0)
            {
                MessageBox.Show(this, "Select an account.", "Transaction", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (!decimal.TryParse(_amount.Text.Trim(), NumberStyles.Currency | NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out decimal amount))
            {
                MessageBox.Show(this, "Enter a valid amount.", "Transaction", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            string type = _type.Text.Trim();
            string description = _description.Text.Trim();

            if (string.IsNullOrWhiteSpace(type))
            {
                MessageBox.Show(this, "Enter a transaction type.", "Transaction", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                MessageBox.Show(this, "Enter a transaction description.", "Transaction", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            Transaction.AccountId = accountId;
            Transaction.TransactionDate = DateOnly.FromDateTime(_date.Value.Date);
            Transaction.TransactionTime = _time.Checked ? TimeOnly.FromDateTime(_time.Value) : null;
            Transaction.Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
            Transaction.Type = type;
            Transaction.Description = description;
            Transaction.Category = EmptyToNull(_category.Text);
            Transaction.SpendingClass = EmptyToNull(_categoryType.Text);
            Transaction.Merchant = EmptyToNull(_merchant.Text);
            Transaction.IsIncome = _isIncome.Checked;
            Transaction.IncomeOwner = OwnerValues.Normalize(_incomeOwner.Text);
            Transaction.IsTransfer = _isTransfer.Checked;
            Transaction.IsManual = _isManual.Checked;
            Transaction.ClassificationIsManual = _preserveClassification.Checked;
            Transaction.Notes = EmptyToNull(_notes.Text);
            return true;
        }

        private static string? EmptyToNull(string value)
        {
            string trimmed = value.Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }
    }
}
