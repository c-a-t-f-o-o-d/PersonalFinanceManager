using System.Globalization;
using PersonalFinanceManager.Core.Models;

namespace PersonalFinanceManager.App.UI.Views
{
    public sealed class RuleEditForm : Form
    {
        private readonly TextBox _ruleName = new();
        private readonly TextBox _priority = new();
        private readonly TextBox _description = new();
        private readonly TextBox _type = new();
        private readonly ComboBox _descriptionMode = new();
        private readonly ComboBox _typeMode = new();
        private readonly TextBox _category = new();
        private readonly TextBox _spendingClass = new();
        private readonly CheckBox _isIncome = new() { Text = "Income" };
        private readonly CheckBox _isTransfer = new() { Text = "Transfer" };
        private readonly ComboBox _incomeOwner = new();
        private readonly CheckBox _isActive = new() { Text = "Active" };
        private readonly TextBox _notes = new();

        public RuleEditForm(FinancialRule rule)
        {
            Rule = rule;
            InitializeLayout();
            LoadRule();
        }

        public FinancialRule Rule { get; }

        private void InitializeLayout()
        {
            Text = "Rule";
            Width = 720;
            Height = 560;
            StartPosition = FormStartPosition.CenterParent;

            TableLayoutPanel root = new()
            {
                Dock = DockStyle.Fill,
                RowCount = 2
            };

            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            Controls.Add(root);

            TableLayoutPanel fields = new()
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                ColumnCount = 2,
                Padding = new Padding(12)
            };

            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.Controls.Add(fields, 0, 0);
            _descriptionMode.Items.AddRange(new object[] { RuleMatchModes.Exact, RuleMatchModes.Contains, RuleMatchModes.StartsWith });
            _typeMode.Items.AddRange(new object[] { RuleMatchModes.Exact, RuleMatchModes.Contains, RuleMatchModes.StartsWith });
            int row = 0;
            AddRow(fields, ref row, "Rule Name", _ruleName);
            AddRow(fields, ref row, "Priority", _priority);
            AddRow(fields, ref row, "Description", _description);
            AddRow(fields, ref row, "Description Matching", _descriptionMode);
            AddRow(fields, ref row, "Type / Direction", _type);
            AddRow(fields, ref row, "Type Matching", _typeMode);
            AddRow(fields, ref row, "Category", _category);
            AddRow(fields, ref row, "Category Type", _spendingClass);
            AddRow(fields, ref row, "Income", _isIncome);
            AddRow(fields, ref row, "Transfer", _isTransfer);
            _incomeOwner.DropDownStyle = ComboBoxStyle.DropDown;
            _incomeOwner.Items.AddRange(OwnerValues.All);
            AddRow(fields, ref row, "Income Owner", _incomeOwner);
            AddRow(fields, ref row, "Active", _isActive);
            _notes.Multiline = true;
            _notes.Height = 80;
            AddRow(fields, ref row, "Notes", _notes, 90);
            FinalizeFixedRows(fields, row);

            FlowLayoutPanel buttons = new()
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };

            root.Controls.Add(buttons, 0, 1);
            Button save = new() { Text = "Save", DialogResult = DialogResult.OK, Width = 100 };
            save.Click += (_, _) => SaveRule();
            buttons.Controls.Add(save);
            Button cancel = new() { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 100 };
            buttons.Controls.Add(cancel);
            AcceptButton = save;
            CancelButton = cancel;
        }

        private static void AddRow(TableLayoutPanel fields, ref int row, string labelText, Control control, int height = 36)
        {
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, height));

            Label label = new()
            {
                Text = labelText,
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Margin = new Padding(3, 4, 3, 4)
            };

            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(3, 4, 3, 4);
            fields.Controls.Add(label, 0, row);
            fields.Controls.Add(control, 1, row);
            row++;
        }

        private static void FinalizeFixedRows(TableLayoutPanel fields, int rows)
        {
            fields.RowCount = rows + 1;
            fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        }

        private void LoadRule()
        {
            _ruleName.Text = Rule.RuleName;
            _priority.Text = Rule.Priority.ToString(CultureInfo.InvariantCulture);
            _description.Text = Rule.MatchDescription;
            _type.Text = Rule.MatchType ?? string.Empty;
            _descriptionMode.Text = string.IsNullOrWhiteSpace(Rule.DescriptionMatchMode) ? RuleMatchModes.Exact : Rule.DescriptionMatchMode;
            _typeMode.Text = string.IsNullOrWhiteSpace(Rule.TypeMatchMode) ? RuleMatchModes.Exact : Rule.TypeMatchMode;
            _category.Text = Rule.Category ?? string.Empty;
            _spendingClass.Text = Rule.SpendingClass ?? string.Empty;
            _isIncome.Checked = Rule.IsIncome ?? false;
            _isTransfer.Checked = Rule.IsTransfer ?? false;
            _incomeOwner.Text = Rule.IncomeOwner ?? string.Empty;
            _isActive.Checked = Rule.IsActive;
            _notes.Text = Rule.Notes ?? string.Empty;
        }

        private void SaveRule()
        {
            Rule.RuleName = string.IsNullOrWhiteSpace(_ruleName.Text) ? _description.Text.Trim() : _ruleName.Text.Trim();
            Rule.Priority = int.TryParse(_priority.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int priority) ? priority : 100;
            Rule.MatchDescription = _description.Text.Trim();
            Rule.MatchType = NullIfWhite(_type.Text);
            Rule.DescriptionMatchMode = string.IsNullOrWhiteSpace(_descriptionMode.Text) ? RuleMatchModes.Exact : _descriptionMode.Text.Trim();
            Rule.TypeMatchMode = string.IsNullOrWhiteSpace(_typeMode.Text) ? RuleMatchModes.Exact : _typeMode.Text.Trim();
            Rule.Category = NullIfWhite(_category.Text);
            Rule.SpendingClass = NullIfWhite(_spendingClass.Text);
            Rule.IsIncome = _isIncome.Checked;
            Rule.IsTransfer = _isTransfer.Checked;
            Rule.IncomeOwner = OwnerValues.Normalize(_incomeOwner.Text);
            Rule.IsActive = _isActive.Checked;
            Rule.Notes = NullIfWhite(_notes.Text);
        }

        private static string? NullIfWhite(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
