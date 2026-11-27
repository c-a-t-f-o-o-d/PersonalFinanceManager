using System.Drawing.Drawing2D;
using System.Globalization;
using PersonalFinanceManager.App.UI.Controls;
using PersonalFinanceManager.App.UI.Styling;
using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Core.Reporting;
using PersonalFinanceManager.Application.Services.Contracts;

namespace PersonalFinanceManager.App.UI.Views
{
    public sealed class MoneyInOutView : UserControl, IPageLayoutControl
    {
        private readonly IAnalysisService _analysisService;
        private readonly ComboBox _accountFilter = new();
        private readonly DateTimePicker _fromDate = new();
        private readonly DateTimePicker _toDate = new();
        private readonly NumericUpDown _maxRows = new();
        private readonly Label _periodLabel = new();
        private readonly TextBox _reviewNotes = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
        private readonly Label _moneyInMetric = new();
        private readonly Label _moneyOutMetric = new();
        private readonly Label _netMetric = new();
        private readonly Label _averageNetMetric = new();
        private readonly MoneyInOutChartControl _chart = new();
        private readonly DataGridView _monthlyGrid = new();
        private readonly DataGridView _detailGrid = new();
        private readonly BindingSource _monthlySource = new();
        private readonly BindingSource _detailSource = new();
        private IReadOnlyList<Account> _accounts = Array.Empty<Account>();

        public MoneyInOutView(IAnalysisService analysisService)
        {
            _analysisService = analysisService;
            InitializeLayout();
        }

        public int PreferredPageHeight => 1150;

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            await LoadAccountsAsync().ConfigureAwait(true);
            await SetLast12MonthsAsync().ConfigureAwait(true);
        }

        private void InitializeLayout()
        {
            BackColor = UiTheme.AppBackground;

            TableLayoutPanel root = new()
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                BackColor = UiTheme.AppBackground
            };

            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 148));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            Controls.Add(root);

            CardPanel filterCard = new()
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 12),
                Padding = new Padding(16, 12, 16, 10)
            };

            root.Controls.Add(filterCard, 0, 0);
            BuildFilterCard(filterCard);

            TableLayoutPanel metricRow = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                BackColor = UiTheme.AppBackground,
                Margin = new Padding(0, 0, 0, 12)
            };

            for (int index = 0; index < 4; index++)
            {
                metricRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            }

            root.Controls.Add(metricRow, 0, 1);
            metricRow.Controls.Add(BuildMetricCard("Money In", _moneyInMetric), 0, 0);
            metricRow.Controls.Add(BuildMetricCard("Money Out", _moneyOutMetric), 1, 0);
            metricRow.Controls.Add(BuildMetricCard("Net Cash Flow", _netMetric), 2, 0);
            metricRow.Controls.Add(BuildMetricCard("Avg Net / Month", _averageNetMetric), 3, 0);

            CardPanel chartCard = new()
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 12),
                Padding = new Padding(16)
            };

            root.Controls.Add(chartCard, 0, 2);
            BuildChartCard(chartCard);

            CardPanel gridCard = new()
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12)
            };

            root.Controls.Add(gridCard, 0, 3);
            BuildGridTabs(gridCard);
            UiTheme.ApplyTo(this);
        }

        private void BuildFilterCard(Control parent)
        {
            TableLayoutPanel layout = new()
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                BackColor = UiTheme.CardBackground
            };

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            parent.Controls.Add(layout);

            FlowLayoutPanel filters = new()
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = false,
                BackColor = UiTheme.CardBackground
            };

            layout.Controls.Add(filters, 0, 0);
            AddFilterLabel(filters, "Account");
            _accountFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            _accountFilter.Width = 260;
            filters.Controls.Add(_accountFilter);
            AddFilterLabel(filters, "From");
            ConfigureDatePicker(_fromDate);
            filters.Controls.Add(_fromDate);
            AddFilterLabel(filters, "To");
            ConfigureDatePicker(_toDate);
            filters.Controls.Add(_toDate);
            AddFilterLabel(filters, "Max Rows");
            _maxRows.Minimum = 50;
            _maxRows.Maximum = 10000;
            _maxRows.Value = 1000;
            _maxRows.Width = 86;
            filters.Controls.Add(_maxRows);
            Button refresh = new() { Text = "Refresh", Width = 94, Height = 30 };
            UiTheme.StylePrimaryButton(refresh);
            refresh.Click += async (_, _) => await RefreshAsync().ConfigureAwait(true);
            filters.Controls.Add(refresh);

            FlowLayoutPanel ranges = new()
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = false,
                BackColor = UiTheme.CardBackground
            };

            layout.Controls.Add(ranges, 0, 1);
            AddRangeButton(ranges, "Current Month", SetCurrentMonthAsync, 122);
            AddRangeButton(ranges, "Current Year", SetCurrentYearAsync, 112);
            AddRangeButton(ranges, "Last 3 Months", SetLastThreeMonthsAsync, 124);
            AddRangeButton(ranges, "Last 12 Months", SetLast12MonthsAsync, 130);
            AddRangeButton(ranges, "All", SetAllAsync, 70);
            _periodLabel.Dock = DockStyle.Fill;
            _periodLabel.Tag = "SecondaryText";
            _periodLabel.TextAlign = ContentAlignment.MiddleRight;
            _periodLabel.Padding = new Padding(0, 4, 0, 0);
            layout.Controls.Add(_periodLabel, 0, 2);
        }

        private void BuildChartCard(Control parent)
        {
            TableLayoutPanel layout = new()
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                BackColor = UiTheme.CardBackground
            };

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            parent.Controls.Add(layout);

            Label title = new()
            {
                Text = "Money In / Money Out by Month",
                Dock = DockStyle.Fill,
                Font = UiTheme.SectionTitleFont
            };

            layout.Controls.Add(title, 0, 0);

            Label subtitle = new()
            {
                Text = "Cash-account receipts and payments; credit-card purchases affect spending when purchased and cash when paid",
                Dock = DockStyle.Fill,
                Tag = "SecondaryText"
            };

            layout.Controls.Add(subtitle, 0, 1);
            _chart.Dock = DockStyle.Fill;
            _chart.Margin = new Padding(0, 8, 0, 0);
            layout.Controls.Add(_chart, 0, 2);
        }

        private void BuildGridTabs(Control parent)
        {
            TabControl tabs = new() { Dock = DockStyle.Fill };
            parent.Controls.Add(tabs);
            TabPage monthlyTab = new("Monthly Summary");
            tabs.TabPages.Add(monthlyTab);
            ConfigureMonthlyGrid();
            monthlyTab.Controls.Add(_monthlyGrid);
            TabPage detailTab = new("Detailed Transactions");
            tabs.TabPages.Add(detailTab);
            ConfigureDetailGrid();
            detailTab.Controls.Add(_detailGrid);
            TabPage reviewTab = new("Review notes");
            reviewTab.Controls.Add(_reviewNotes);
            tabs.TabPages.Add(reviewTab);
        }

        private static CardPanel BuildMetricCard(string title, Label valueLabel)
        {
            CardPanel card = new()
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 10, 0),
                Padding = new Padding(14, 10, 14, 10)
            };

            TableLayoutPanel layout = new()
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                BackColor = UiTheme.CardBackground
            };

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            card.Controls.Add(layout);

            Label titleLabel = new()
            {
                Text = title,
                Dock = DockStyle.Fill,
                Font = UiTheme.SmallFont,
                Tag = "SecondaryText"
            };

            layout.Controls.Add(titleLabel, 0, 0);
            valueLabel.Dock = DockStyle.Fill;
            valueLabel.Font = new Font("Segoe UI Semibold", 13F, FontStyle.Regular);
            valueLabel.TextAlign = ContentAlignment.MiddleLeft;
            valueLabel.AutoEllipsis = true;
            layout.Controls.Add(valueLabel, 0, 1);
            return card;
        }

        private void ConfigureMonthlyGrid()
        {
            ConfigureGrid(_monthlyGrid, _monthlySource);
            _monthlyGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _monthlyGrid.Columns.Add(MakeTextColumn("MonthLabel", "Month", 120));
            _monthlyGrid.Columns.Add(MakeTextColumn("MoneyIn", "Money In", 130, "C2"));
            _monthlyGrid.Columns.Add(MakeTextColumn("MoneyOut", "Money Out", 130, "C2"));
            _monthlyGrid.Columns.Add(MakeTextColumn("NetCashFlow", "Net", 130, "C2"));
            _monthlyGrid.Columns.Add(MakeTextColumn("TransactionCount", "Rows", 90));
        }

        private void ConfigureDetailGrid()
        {
            ConfigureGrid(_detailGrid, _detailSource);
            _detailGrid.Columns.Add(MakeTextColumn("TransactionDate", "Date", 95));
            _detailGrid.Columns.Add(MakeTextColumn("AccountNickname", "Account", 170));
            _detailGrid.Columns.Add(MakeTextColumn("Direction", "Direction", 80));
            _detailGrid.Columns.Add(MakeTextColumn("Amount", "Amount", 120, "C2"));
            _detailGrid.Columns.Add(MakeTextColumn("Type", "Type", 110));
            _detailGrid.Columns.Add(MakeTextColumn("Description", "Description", 420));
            _detailGrid.Columns.Add(MakeTextColumn("Category", "Category", 170));
            _detailGrid.Columns.Add(MakeTextColumn("SpendingClass", "Category Type", 170));
        }

        private static void ConfigureGrid(DataGridView grid, BindingSource source)
        {
            grid.Dock = DockStyle.Fill;
            grid.AutoGenerateColumns = false;
            grid.AllowUserToAddRows = false;
            grid.ReadOnly = true;
            grid.DataSource = source;
            UiTheme.StyleGrid(grid);
        }

        private static DataGridViewTextBoxColumn MakeTextColumn(string propertyName, string headerText, int width, string? format = null)
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
            _accounts = await _analysisService.GetAccountsAsync().ConfigureAwait(true);
            List<AccountOption> options = new() { new AccountOption(null, "All Cash Accounts") };
            options.AddRange(_accounts.Where(account => AccountClassification.IsCash(account.AccountType)).Select(account => new AccountOption(account.AccountId, account.DisplayName)));
            _accountFilter.DataSource = options;
            _accountFilter.DisplayMember = nameof(AccountOption.DisplayName);
            _accountFilter.ValueMember = nameof(AccountOption.AccountId);
        }

        private async Task SetCurrentMonthAsync()
        {
            DateTime today = DateTime.Today;
            SetDateRange(new DateTime(today.Year, today.Month, 1), today);
            await RefreshAsync().ConfigureAwait(true);
        }

        private async Task SetCurrentYearAsync()
        {
            DateTime today = DateTime.Today;
            SetDateRange(new DateTime(today.Year, 1, 1), today);
            await RefreshAsync().ConfigureAwait(true);
        }

        private async Task SetLastThreeMonthsAsync()
        {
            DateTime today = DateTime.Today;
            SetDateRange(new DateTime(today.Year, today.Month, 1).AddMonths(-2), today);
            await RefreshAsync().ConfigureAwait(true);
        }

        private async Task SetLast12MonthsAsync()
        {
            DateTime today = DateTime.Today;
            SetDateRange(new DateTime(today.Year, today.Month, 1).AddMonths(-11), today);
            await RefreshAsync().ConfigureAwait(true);
        }

        private async Task SetAllAsync()
        {
            _fromDate.Checked = false;
            _toDate.Checked = false;
            await RefreshAsync().ConfigureAwait(true);
        }

        private void SetDateRange(DateTime fromDate, DateTime toDate)
        {
            _fromDate.Checked = true;
            _fromDate.Value = fromDate;
            _toDate.Checked = true;
            _toDate.Value = toDate;
        }

        private async Task RefreshAsync()
        {
            ExpenseReportFilter filter = BuildFilter();

            if (filter.FromDate.HasValue && filter.ToDate.HasValue && filter.FromDate > filter.ToDate)
            {
                MessageBox.Show(this, "The start date must be on or before the end date.", "Cash Flow", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            CashFlowReportResult report = await _analysisService.GetCashFlowReportAsync(filter).ConfigureAwait(true);
            MoneyInOutSummary summary = report.Summary;
            IReadOnlyList<MoneyInOutMonthlyItem> monthly = report.Monthly;
            IReadOnlyList<MoneyInOutDetailItem> details = report.Details;
            _monthlySource.DataSource = monthly;
            _detailSource.DataSource = details;
            _chart.SetData(monthly);
            _moneyInMetric.Text = summary.TotalMoneyIn.ToString("C2", CultureInfo.CurrentCulture);
            _moneyOutMetric.Text = summary.TotalMoneyOut.ToString("C2", CultureInfo.CurrentCulture);
            _netMetric.Text = summary.NetCashFlow.ToString("C2", CultureInfo.CurrentCulture);
            _netMetric.ForeColor = summary.NetCashFlow >= 0M ? UiTheme.Success : Color.FromArgb(220, 38, 38);
            decimal averageNet = report.Period.MonthEquivalent > 0M ? summary.NetCashFlow / report.Period.MonthEquivalent : 0M;
            _averageNetMetric.Text = report.Period.HasObservedTransactions ? averageNet.ToString("C2", CultureInfo.CurrentCulture) : "Unknown";
            _averageNetMetric.ForeColor = averageNet >= 0M ? UiTheme.Success : Color.FromArgb(220, 38, 38);

            _periodLabel.Text = report.Period.HasObservedTransactions
                ? $"{report.Period.FromDate:MMM d, yyyy} - {report.Period.ToDate:MMM d, yyyy} | {report.Period.MonthCount} calendar months | Import coverage unverified"
                : report.Period.CoverageNote;

            _reviewNotes.Lines = report.Warnings.Append(report.Period.CoverageNote).ToArray();
            _periodLabel.ForeColor = report.Warnings.Count > 0 ? Color.DarkOrange : UiTheme.TextSecondary;

            if (report.Warnings.Count > 0)
            {
                _periodLabel.Text = "Transaction relationships require review; totals include unresolved entries. See Review notes.";
            }
        }

        private ExpenseReportFilter BuildFilter()
        {
            AccountOption? option = _accountFilter.SelectedItem as AccountOption;

            return new ExpenseReportFilter
            {
                AccountId = option?.AccountId,
                FromDate = _fromDate.Checked ? DateOnly.FromDateTime(_fromDate.Value.Date) : null,
                ToDate = _toDate.Checked ? DateOnly.FromDateTime(_toDate.Value.Date) : null,
                MaxDetailRows = decimal.ToInt32(_maxRows.Value)
            };
        }

        private static void AddFilterLabel(FlowLayoutPanel filters, string text)
        {
            filters.Controls.Add(new Label
            {
                Text = text,
                AutoSize = true,
                Margin = new Padding(5, 8, 5, 0),
                TextAlign = ContentAlignment.MiddleLeft
            });
        }

        private static void ConfigureDatePicker(DateTimePicker picker)
        {
            picker.Width = 122;
            picker.Format = DateTimePickerFormat.Short;
            picker.ShowCheckBox = true;
            picker.Checked = false;
            picker.Height = 28;
        }

        private static void AddRangeButton(FlowLayoutPanel panel, string text, Func<Task> handler, int width)
        {
            Button button = new() { Text = text, Width = width, Height = 30 };
            UiTheme.StyleSecondaryButton(button);
            button.Click += async (_, _) => await handler().ConfigureAwait(true);
            panel.Controls.Add(button);
        }

        private sealed record AccountOption(int? AccountId, string DisplayName);

        private sealed class MoneyInOutChartControl : Control
        {
            private IReadOnlyList<MoneyInOutMonthlyItem> _items = Array.Empty<MoneyInOutMonthlyItem>();

            public MoneyInOutChartControl()
            {
                DoubleBuffered = true;
                BackColor = UiTheme.CardBackground;
            }

            public void SetData(IReadOnlyList<MoneyInOutMonthlyItem> items)
            {
                _items = items;
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                if (_items.Count == 0)
                {
                    DrawEmpty(e.Graphics, ClientRectangle);
                    return;
                }

                decimal max = Math.Max(_items.Max(item => item.MoneyIn), _items.Max(item => item.MoneyOut));

                if (max <= 0M)
                {
                    DrawEmpty(e.Graphics, ClientRectangle);
                    return;
                }

                Rectangle plot = new(76, 20, Math.Max(80, Width - 110), Math.Max(80, Height - 76));
                max = RoundAxisMaximum(max);
                DrawGrid(e.Graphics, plot, max);
                DrawBars(e.Graphics, plot, max);
                DrawLegend(e.Graphics, new Rectangle(plot.Left, 0, plot.Width, 18));
            }

            private void DrawBars(Graphics graphics, Rectangle plot, decimal max)
            {
                int groupGap = _items.Count > 18 ? 8 : 14;
                int barWidth = Math.Max(5, Math.Min(28, (plot.Width - groupGap * (_items.Count + 1)) / Math.Max(1, _items.Count * 2)));
                int usedWidth = _items.Count * (barWidth * 2 + 3) + (_items.Count + 1) * groupGap;
                int x = plot.Left + Math.Max(0, (plot.Width - usedWidth) / 2) + groupGap;
                using StringFormat center = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };

                for (int index = 0; index < _items.Count; index++)
                {
                    MoneyInOutMonthlyItem item = _items[index];
                    DrawSingleBar(graphics, plot, x, barWidth, item.MoneyIn, max, Color.FromArgb(22, 163, 74));
                    DrawSingleBar(graphics, plot, x + barWidth + 3, barWidth, item.MoneyOut, max, Color.FromArgb(220, 38, 38));
                    string label = item.MonthStart.ToString("MMM", CultureInfo.CurrentCulture) + Environment.NewLine + item.MonthStart.ToString("yyyy", CultureInfo.CurrentCulture);
                    Rectangle labelBounds = new(x - 28, plot.Bottom + 6, barWidth * 2 + 60, 38);
                    using Brush labelBrush = new SolidBrush(UiTheme.TextPrimary);
                    graphics.DrawString(label, UiTheme.SmallFont, labelBrush, labelBounds, center);
                    x += barWidth * 2 + 3 + groupGap;
                }
            }

            private static void DrawSingleBar(Graphics graphics, Rectangle plot, int x, int width, decimal value, decimal max, Color color)
            {
                int height = (int)Math.Round((double)(value / max) * (plot.Height - 4), MidpointRounding.AwayFromZero);
                Rectangle bar = new(x, plot.Bottom - height, width, Math.Max(1, height));
                using LinearGradientBrush brush = new(bar, Lighten(color, 0.18F), color, LinearGradientMode.Vertical);
                graphics.FillRectangle(brush, bar);
                using Pen outline = new(Color.FromArgb(190, 199, 214));
                graphics.DrawRectangle(outline, bar);
            }

            private static void DrawGrid(Graphics graphics, Rectangle plot, decimal max)
            {
                using Pen axisPen = new(Color.FromArgb(194, 203, 217));
                using Pen gridPen = new(Color.FromArgb(229, 234, 242)) { DashStyle = DashStyle.Dash };
                using Font labelFont = new("Segoe UI", 8F, FontStyle.Regular);
                using Brush labelBrush = new SolidBrush(UiTheme.TextSecondary);
                using StringFormat right = new() { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

                for (int step = 0; step <= 5; step++)
                {
                    decimal value = max / 5M * step;
                    int y = plot.Bottom - (int)Math.Round(plot.Height * step / 5D, MidpointRounding.AwayFromZero);
                    graphics.DrawLine(step == 0 ? axisPen : gridPen, plot.Left, y, plot.Right, y);
                    Rectangle labelBounds = new(0, y - 10, plot.Left - 8, 20);
                    graphics.DrawString(value.ToString("C0", CultureInfo.CurrentCulture), labelFont, labelBrush, labelBounds, right);
                }

                graphics.DrawLine(axisPen, plot.Left, plot.Top, plot.Left, plot.Bottom);
            }

            private static void DrawLegend(Graphics graphics, Rectangle bounds)
            {
                using Brush inBrush = new SolidBrush(Color.FromArgb(22, 163, 74));
                using Brush outBrush = new SolidBrush(Color.FromArgb(220, 38, 38));
                using Brush textBrush = new SolidBrush(UiTheme.TextPrimary);
                graphics.FillRectangle(inBrush, bounds.Left, bounds.Top + 5, 12, 12);
                graphics.DrawString("Money In", UiTheme.SmallFont, textBrush, bounds.Left + 18, bounds.Top + 2);
                graphics.FillRectangle(outBrush, bounds.Left + 110, bounds.Top + 5, 12, 12);
                graphics.DrawString("Money Out", UiTheme.SmallFont, textBrush, bounds.Left + 128, bounds.Top + 2);
            }
        }

        private static void DrawEmpty(Graphics graphics, Rectangle bounds)
        {
            using Font font = new("Segoe UI", 10F, FontStyle.Regular);
            using Brush brush = new SolidBrush(UiTheme.TextSecondary);
            using StringFormat format = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            graphics.DrawString("No money in/out data for the selected period", font, brush, bounds, format);
        }

        private static decimal RoundAxisMaximum(decimal value)
        {
            if (value <= 0M)
            {
                return 1M;
            }

            decimal rounded = Math.Ceiling(value / 5000M) * 5000M;
            return rounded <= 0M ? value : rounded;
        }

        private static Color Lighten(Color color, float amount)
        {
            int red = color.R + (int)((255 - color.R) * amount);
            int green = color.G + (int)((255 - color.G) * amount);
            int blue = color.B + (int)((255 - color.B) * amount);
            return Color.FromArgb(Math.Min(255, red), Math.Min(255, green), Math.Min(255, blue));
        }
    }
}
