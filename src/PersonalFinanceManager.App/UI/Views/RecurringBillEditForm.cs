using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Core.Planning;
using PersonalFinanceManager.App.UI.Styling;

namespace PersonalFinanceManager.App.UI.Views;

public sealed class RecurringBillEditForm : ThemedForm
{
    private readonly IReadOnlyList<Account> _accounts;
    private readonly string _previousPayee;
    private readonly string? _previousDescription;
    private readonly IReadOnlyList<string> _previousDescriptionAliases;
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
    private readonly TextBox _payeeAliases = new();
    private readonly TextBox _sourceDescriptionAliases = new();
    private readonly CheckBox _preserveFormerNames = new() { Text = "Keep former names when replacing them", Checked = true, AutoSize = true };
    private readonly CheckBox _reviewDescriptionConversion = new() { Text = "I included the former exact statement descriptions", AutoSize = true };
    private readonly TextBox _notes = new();

    public RecurringBillEditForm(RecurringBill bill, IReadOnlyList<Account> accounts, IReadOnlyList<string> categories, IReadOnlyList<string> categoryTypes, IReadOnlyList<string> requirementTypes, bool showMatching = false)
    {
        Bill = bill;
        _accounts = accounts;
        _previousPayee = bill.Payee;
        _previousDescription = bill.SourceDescription;
        _previousDescriptionAliases = bill.SourceDescriptionAliases.ToArray();
        Text = bill.RecurringBillId == 0 ? "Add Recurring Bill" : "Edit Recurring Bill";
        Width = 800;
        Height = 720;
        MinimumSize = new Size(720, 640);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9F);
        InitializeLayout(categories, categoryTypes, requirementTypes, showMatching);
        LoadBill();
    }

    public RecurringBill Bill { get; }

    private void InitializeLayout(IReadOnlyList<string> categories, IReadOnlyList<string> categoryTypes, IReadOnlyList<string> requirementTypes, bool showMatching)
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12)
        };

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        Controls.Add(root);
        root.Controls.Add(new Label
        {
            Text = "One bill, one planned payment\r\nUse a stable name such as Home mortgage. Keep former and current payment descriptions together on the Matching tab when a servicer changes.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        TabControl tabs = new() { Dock = DockStyle.Fill };
        TabPage detailsPage = new("Bill details") { AutoScroll = true };
        TabPage matchingPage = new("Matching") { AutoScroll = true };
        tabs.TabPages.Add(detailsPage);
        tabs.TabPages.Add(matchingPage);
        tabs.SelectedTab = showMatching ? matchingPage : detailsPage;
        root.Controls.Add(tabs, 0, 1);
        TableLayoutPanel details = CreateFieldsPanel(detailsPage);
        TableLayoutPanel matching = CreateFieldsPanel(matchingPage);
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
        _sourceDescription.ScrollBars = ScrollBars.Vertical;
        _sourceDescription.MaxLength = 1000;
        _sourceDescription.Name = "StatementDescriptionTextBox";
        _payee.Name = "CurrentPayeeTextBox";
        _billName.Name = "BillNameTextBox";
        _payeeAliases.Name = "PayeeAliasesTextBox";
        _sourceDescriptionAliases.Name = "StatementDescriptionAliasesTextBox";
        _preserveFormerNames.Name = "PreserveFormerNamesCheckBox";
        _reviewDescriptionConversion.Name = "ReviewDescriptionConversionCheckBox";
        ConfigureAliasBox(_payeeAliases);
        ConfigureAliasBox(_sourceDescriptionAliases);
        _billName.MaxLength = 200;
        _payee.MaxLength = 300;
        _notes.Multiline = true;
        _notes.ScrollBars = ScrollBars.Vertical;
        _notes.MaxLength = 1000;
        int row = 0;
        AddRow(details, ref row, "Bill name", _billName, 34);
        AddRow(details, ref row, "Current payee", _payee, 34);
        AddRow(details, ref row, "Account", _account, 34);
        AddRow(details, ref row, "Category", _category, 34);
        AddRow(details, ref row, "Category type", _categoryType, 34);
        AddRow(details, ref row, "Planning purpose", _requirementType, 34);
        AddRow(details, ref row, "Amount per occurrence", _typicalAmount, 34);
        AddRow(details, ref row, "Due day (0 = unset)", _dueDay, 34);
        AddRow(details, ref row, "Next due date", _nextDueDate, 34);
        AddRow(details, ref row, "Frequency", _frequency, 34);
        FlowLayoutPanel flags = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        flags.Controls.Add(_isRequired);
        flags.Controls.Add(_isActive);
        AddRow(details, ref row, "Flags", flags, 34);
        AddRow(details, ref row, "Notes", _notes, 80);
        FinalizeFixedRows(details, row);

        int matchingRow = 0;

        if (Bill.RecurringBillId > 0)
        {
            AddRow(matching, ref matchingRow, "When changing names", _preserveFormerNames, 34);

            if (string.IsNullOrWhiteSpace(_previousDescription) && _previousDescriptionAliases.All(string.IsNullOrWhiteSpace))
            {
                AddRow(matching, ref matchingRow, "Conversion review", _reviewDescriptionConversion, 34);
            }
        }

        AddRow(matching, ref matchingRow, "Statement description", _sourceDescription, 64);
        AddRow(matching, ref matchingRow, "Additional descriptions\r\n(one per line)", _sourceDescriptionAliases, 142);
        AddRow(matching, ref matchingRow, "Former / other payees\r\n(one per line)", _payeeAliases, 100);
        Label matchingHelp = new()
        {
            Text = "Copy complete descriptions from imported transactions. Matching ignores letter case and surrounding spaces, and stays within the bill's account, category, and category type.\r\n\r\nWhen any statement description is entered, only those exact descriptions identify historical payments. Payee names are used only when all description fields are empty.\r\n\r\nAliases preserve history; they do not create another scheduled payment or change imported transactions. Review each alias before adding it.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft
        };
        matching.Controls.Add(matchingHelp, 0, matchingRow);
        matching.SetColumnSpan(matchingHelp, 2);
        matching.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        FinalizeFixedRows(matching, matchingRow + 1);

        FlowLayoutPanel buttons = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };

        Button cancel = new() { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90 };
        Button ok = new() { Text = "Save", Name = "SaveRecurringBillButton", Width = 90 };

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
        root.Controls.Add(buttons, 0, 2);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private static TableLayoutPanel CreateFieldsPanel(TabPage page)
    {
        TableLayoutPanel panel = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(8) };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        page.Controls.Add(panel);
        return panel;
    }

    private static void ConfigureAliasBox(TextBox textBox)
    {
        textBox.Multiline = true;
        textBox.AcceptsReturn = true;
        textBox.ScrollBars = ScrollBars.Vertical;
        textBox.MaxLength = 51000;
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
        _payeeAliases.Lines = Bill.PayeeAliases.ToArray();
        _sourceDescriptionAliases.Lines = Bill.SourceDescriptionAliases.ToArray();
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

        if (_frequency.SelectedIndex < 0)
        {
            throw new InvalidOperationException("Choose a bill frequency; the historical cadence could not be established automatically.");
        }

        RecurringBill matching = new()
        {
            Payee = payee,
            SourceDescription = EmptyToNull(_sourceDescription.Text),
            PayeeAliases = _payeeAliases.Lines,
            SourceDescriptionAliases = _sourceDescriptionAliases.Lines
        };

        if (Bill.RecurringBillId > 0 && _preserveFormerNames.Checked)
        {
            RecurringBillMatcher.PreserveFormerNames(matching, _previousPayee, _previousDescription,
                _previousDescriptionAliases, _reviewDescriptionConversion.Checked);
        }

        RecurringBillMatcher.NormalizeAliases(matching);
        Bill.BillName = billName;
        Bill.Payee = matching.Payee;
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
        Bill.SourceDescription = matching.SourceDescription;
        Bill.PayeeAliases = matching.PayeeAliases;
        Bill.SourceDescriptionAliases = matching.SourceDescriptionAliases;
        Bill.Notes = EmptyToNull(_notes.Text);
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record AccountOption(int? AccountId, string DisplayName);
}
