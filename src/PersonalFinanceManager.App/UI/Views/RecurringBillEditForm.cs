using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Core.Planning;

namespace PersonalFinanceManager.App.UI.Views;

public sealed class RecurringBillEditForm : Form
{
    private readonly IReadOnlyList<Account> _accounts;
    private readonly TextBox _billName = new();
    private readonly TextBox _payee = new();
    private readonly ComboBox _account = new();
    private readonly ComboBox _category = new();
    private readonly ComboBox _categoryType = new();
    private readonly ComboBox _requirementType = new();
    private readonly NumericUpDown _typicalAmount = new();
    private readonly NumericUpDown _dueDay = new();
    private readonly DateTimePicker _nextDueDate = new();
    private readonly ComboBox _frequency = new();
    private readonly CheckBox _isRequired = new() { Text = "Required", AutoSize = true };
    private readonly CheckBox _isActive = new() { Text = "Active", AutoSize = true };
    private readonly TextBox _sourceDescription = new();
    private readonly TextBox _notes = new();

    public RecurringBillEditForm(RecurringBill bill, IReadOnlyList<Account> accounts, IReadOnlyList<string> categories, IReadOnlyList<string> categoryTypes, IReadOnlyList<string> requirementTypes)
    {
        Bill = bill;
        _accounts = accounts;
        Text = bill.RecurringBillId == 0 ? "Add Recurring Bill" : "Edit Recurring Bill";
        Width = 740;
        Height = 690;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9F);
        InitializeLayout(categories, categoryTypes, requirementTypes);
        LoadBill();
    }

    public RecurringBill Bill { get; }

    private void InitializeLayout(IReadOnlyList<string> categories, IReadOnlyList<string> categoryTypes, IReadOnlyList<string> requirementTypes)
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12)
        };

        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);

        _account.DropDownStyle = ComboBoxStyle.DropDownList;
        _account.DisplayMember = nameof(AccountOption.DisplayName);
        _account.ValueMember = nameof(AccountOption.AccountId);
        _account.Items.Add(new AccountOption(null, "No account"));
        foreach (Account account in _accounts.OrderBy(account => account.DisplayName))
        {
            _account.Items.Add(new AccountOption(account.AccountId, account.DisplayName));
        }

        ConfigureFreeTextCombo(_category, categories);
        ConfigureFreeTextCombo(_categoryType, categoryTypes);
        ConfigureFreeTextCombo(_requirementType, requirementTypes);
        _frequency.DropDownStyle = ComboBoxStyle.DropDownList;
        _frequency.Items.AddRange(new object[] { BillFrequencies.Monthly, BillFrequencies.Weekly, BillFrequencies.Biweekly, BillFrequencies.Quarterly, BillFrequencies.Semiannual, BillFrequencies.Annual });
        ConfigureMoney(_typicalAmount);
        _dueDay.Minimum = 0;
        _dueDay.Maximum = 31;
        _dueDay.Width = 90;
        _nextDueDate.Format = DateTimePickerFormat.Short;
        _nextDueDate.ShowCheckBox = true;
        _sourceDescription.Multiline = true;
        _sourceDescription.Height = 60;
        _notes.Multiline = true;
        _notes.Height = 70;
        int row = 0;
        AddRow(root, ref row, "Bill name", _billName, 34);
        AddRow(root, ref row, "Payee", _payee, 34);
        AddRow(root, ref row, "Account", _account, 34);
        AddRow(root, ref row, "Category", _category, 34);
        AddRow(root, ref row, "Category Type", _categoryType, 34);
        AddRow(root, ref row, "Need/Want/etc.", _requirementType, 34);
        AddRow(root, ref row, "Amount per occurrence", _typicalAmount, 34);
        AddRow(root, ref row, "Due day", _dueDay, 34);
        AddRow(root, ref row, "Next due date", _nextDueDate, 34);
        AddRow(root, ref row, "Frequency", _frequency, 34);
        FlowLayoutPanel flags = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        flags.Controls.Add(_isRequired);
        flags.Controls.Add(_isActive);
        AddRow(root, ref row, "Flags", flags, 34);
        AddRow(root, ref row, "Source description", _sourceDescription, 70);
        AddRow(root, ref row, "Notes", _notes, 80);

        FlowLayoutPanel buttons = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };

        Button cancel = new() { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90 };
        Button ok = new() { Text = "Save", Width = 90 };

        ok.Click += (_, _) =>
        {
            try
            {
                SaveToBill();
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                DialogResult = DialogResult.None;
                MessageBox.Show(this, ex.Message, "Recurring Bill", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };

        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        AddRow(root, ref row, string.Empty, buttons, 44);
        FinalizeFixedRows(root, row);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private static void ConfigureMoney(NumericUpDown control)
    {
        control.DecimalPlaces = 2;
        control.Maximum = 10000000;
        control.Minimum = 0;
        control.ThousandsSeparator = true;
        control.Width = 120;
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

    private static void AddRow(TableLayoutPanel root, ref int row, string labelText, Control control, int height)
    {
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, height));

        Label label = new()
        {
            Text = labelText,
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            Margin = new Padding(3, 4, 3, 4)
        };

        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(3, 4, 3, 4);
        root.Controls.Add(label, 0, row);
        root.Controls.Add(control, 1, row);
        row++;
    }

    private static void FinalizeFixedRows(TableLayoutPanel root, int rows)
    {
        root.RowCount = rows + 1;
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
    }

    private void LoadBill()
    {
        _billName.Text = Bill.BillName;
        _payee.Text = Bill.Payee;
        SelectAccount(Bill.AccountId);
        _category.Text = Bill.Category ?? string.Empty;
        _categoryType.Text = Bill.SpendingClass ?? string.Empty;
        _requirementType.Text = string.IsNullOrWhiteSpace(Bill.RequirementType) ? BillRequirementTypes.Need : Bill.RequirementType;
        _typicalAmount.Value = Math.Clamp(Bill.TypicalAmount, _typicalAmount.Minimum, _typicalAmount.Maximum);
        _dueDay.Value = Bill.DueDay ?? 0;
        _nextDueDate.Checked = Bill.NextDueDate.HasValue;
        _nextDueDate.Value = Bill.NextDueDate?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today;
        _frequency.SelectedItem = Bill.Frequency;

        _isRequired.Checked = Bill.IsRequired;
        _isActive.Checked = Bill.IsActive;
        _sourceDescription.Text = Bill.SourceDescription ?? string.Empty;
        _notes.Text = Bill.Notes ?? string.Empty;
    }

    private void SelectAccount(int? accountId)
    {
        for (int index = 0; index < _account.Items.Count; index++)
        {
            if (_account.Items[index] is AccountOption option && option.AccountId == accountId)
            {
                _account.SelectedIndex = index;
                return;
            }
        }

        if (_account.Items.Count > 0)
        {
            _account.SelectedIndex = 0;
        }
    }

    private void SaveToBill()
    {
        string billName = _billName.Text.Trim();
        string payee = _payee.Text.Trim();

        if (string.IsNullOrWhiteSpace(billName))
        {
            throw new InvalidOperationException("Bill name is required.");
        }

        if (string.IsNullOrWhiteSpace(payee))
        {
            throw new InvalidOperationException("Payee is required.");
        }

        if (_frequency.SelectedIndex < 0) throw new InvalidOperationException("Choose a bill frequency; the historical cadence could not be established automatically.");

        Bill.BillName = billName;
        Bill.Payee = payee;
        Bill.AccountId = (_account.SelectedItem as AccountOption)?.AccountId;
        Bill.Category = EmptyToNull(_category.Text);
        Bill.SpendingClass = EmptyToNull(_categoryType.Text);
        Bill.RequirementType = string.IsNullOrWhiteSpace(_requirementType.Text) ? BillRequirementTypes.Need : _requirementType.Text.Trim();
        Bill.TypicalAmount = _typicalAmount.Value;
        Bill.DueDay = _dueDay.Value == 0 ? null : decimal.ToInt32(_dueDay.Value);
        Bill.NextDueDate = _nextDueDate.Checked ? DateOnly.FromDateTime(_nextDueDate.Value.Date) : null;
        Bill.Frequency = BillSchedule.NormalizeFrequency(_frequency.SelectedItem?.ToString());
        Bill.IsRequired = _isRequired.Checked;
        Bill.IsActive = _isActive.Checked;
        Bill.SourceDescription = EmptyToNull(_sourceDescription.Text);
        Bill.Notes = EmptyToNull(_notes.Text);
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record AccountOption(int? AccountId, string DisplayName);
}
