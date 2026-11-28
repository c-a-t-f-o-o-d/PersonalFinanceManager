using PersonalFinanceManager.App.UI.Styling;
using PersonalFinanceManager.Core.Models;

namespace PersonalFinanceManager.App.UI.Views;

public sealed class RecurringBillAssociationForm : ThemedForm
{
    private readonly ComboBox _bills = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };

    public RecurringBillAssociationForm(RecurringBillCandidate candidate, IReadOnlyList<RecurringBill> bills)
    {
        Text = "Attach a payment description";
        ClientSize = new Size(660, 260);
        MinimumSize = new Size(560, 300);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 9F);
        TableLayoutPanel root = new() { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 4 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        Controls.Add(root);
        root.Controls.Add(new Label
        {
            Text = "Choose the existing bill for this payment. A former or new servicer belongs to the same obligation. Review the added alias before saving.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        _bills.DisplayMember = nameof(BillOption.DisplayName);

        foreach (RecurringBill bill in bills.Where(bill => bill.IsActive && bill.AccountId == candidate.AccountId).OrderBy(bill => bill.BillName))
        {
            _bills.Items.Add(new BillOption(bill.RecurringBillId, $"{bill.BillName} — {bill.Payee} ({bill.TypicalAmount:C2}, {bill.Frequency})"));
        }

        if (_bills.Items.Count > 0)
        {
            _bills.SelectedIndex = 0;
        }

        root.Controls.Add(_bills, 0, 1);
        root.Controls.Add(new Label
        {
            Text = $"Account: {candidate.AccountNickname}\r\nDescription: {candidate.SourceDescription}\r\nThe bill must have the same account, category, and category type. Its existing amount and schedule are retained.",
            Dock = DockStyle.Fill,
            AutoEllipsis = true
        }, 0, 2);
        FlowLayoutPanel buttons = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        Button cancel = new() { Text = "Cancel", Width = 90, DialogResult = DialogResult.Cancel };
        Button review = new() { Text = "Review alias", Width = 110, Enabled = _bills.Items.Count > 0, DialogResult = DialogResult.OK };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(review);
        root.Controls.Add(buttons, 0, 3);
        AcceptButton = review;
        CancelButton = cancel;
    }

    public int? SelectedBillId => (_bills.SelectedItem as BillOption)?.RecurringBillId;

    private sealed record BillOption(int RecurringBillId, string DisplayName);
}
