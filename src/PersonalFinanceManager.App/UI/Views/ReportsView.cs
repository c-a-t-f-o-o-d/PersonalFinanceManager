using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text;
using PersonalFinanceManager.App.UI.Controls;
using PersonalFinanceManager.App.UI.Styling;
using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Core.Reporting;
using PersonalFinanceManager.Application.Services.Contracts;

namespace PersonalFinanceManager.App.UI.Views
{
    public sealed class ReportsView : UserControl, IPageLayoutControl
    {
        private readonly IAnalysisService _analysisService;
        private readonly ComboBox _accountFilter = new();
        private readonly ComboBox _breakdownMode = new();
        private readonly BindingSource _detailSource = new();
        private readonly DateTimePicker _fromDate = new();
        private readonly NumericUpDown _maxDetailRows = new();
        private readonly BindingSource _monthlySource = new();
        private readonly MonthlyExpenseChartControl _monthlyChart = new();
        private readonly PieExpenseChartControl _pieChart = new();
        private readonly Label _periodLabel = new();
        private readonly TextBox _reviewNotes = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
        private readonly BindingSource _summarySource = new();
        private readonly DateTimePicker _toDate = new();
        private readonly DataGridView _detailGrid = new();
        private readonly DataGridView _monthlyGrid = new();
        private readonly DataGridView _summaryGrid = new();
        private readonly Label _averageMonthMetric = new();
        private readonly Label _averageTransactionMetric = new();
        private readonly Label _dailyAverageMetric = new();
        private readonly Label _largestCategoryMetric = new();
        private readonly Label _largestMonthMetric = new();
        private readonly Label _totalExpensesMetric = new();
        private IReadOnlyList<Account> _accounts = Array.Empty<Account>();
        private IReadOnlyList<ExpenseDetailItem> _currentDetailRows = Array.Empty<ExpenseDetailItem>();

        public ReportsView(IAnalysisService analysisService)
        {
            _analysisService = analysisService;
            InitializeLayout();
        }

        public int PreferredPageHeight => 1250;

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            await LoadAccountsAsync().ConfigureAwait(true);
            await SetLast12MonthsAsync().ConfigureAwait(true);
        }

        private void InitializeLayout()
        {
            BackColor = UiTheme.AppBackground;
            Padding = new Padding(0);

            TableLayoutPanel root = new()
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                BackColor = UiTheme.AppBackground
            };

            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
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
                ColumnCount = 6,
                BackColor = UiTheme.AppBackground,
                Margin = new Padding(0, 0, 0, 12)
            };

            for (int index = 0; index < 6; index++)
            {
                metricRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 6F));
            }

            root.Controls.Add(metricRow, 0, 1);
            metricRow.Controls.Add(BuildMetricCard("Net Expenses", _totalExpensesMetric), 0, 0);
            metricRow.Controls.Add(BuildMetricCard("Avg / Month", _averageMonthMetric), 1, 0);
            metricRow.Controls.Add(BuildMetricCard("Avg / Day", _dailyAverageMetric), 2, 0);
            metricRow.Controls.Add(BuildMetricCard("Avg / Txn", _averageTransactionMetric), 3, 0);
            metricRow.Controls.Add(BuildMetricCard("Top Category", _largestCategoryMetric), 4, 0);
            metricRow.Controls.Add(BuildMetricCard("Top Month", _largestMonthMetric), 5, 0);

            TableLayoutPanel chartRow = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                BackColor = UiTheme.AppBackground,
                Margin = new Padding(0, 0, 0, 12)
            };

            chartRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
            chartRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
            CardPanel pieCard = BuildChartCard("Expense Breakdown", "Where money is going for the selected period", _pieChart);
            chartRow.Controls.Add(pieCard, 0, 0);
            CardPanel monthlyCard = BuildChartCard("Monthly Expense Totals", "Spending trend by calendar month", _monthlyChart);
            chartRow.Controls.Add(monthlyCard, 1, 0);

            CardPanel gridCard = new()
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                Margin = new Padding(0)
            };

            root.Controls.Add(gridCard, 0, 2);
            BuildGridTabs(gridCard, chartRow);
            PageLayout.AutoSizeHeaders(root, 0);
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
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
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
            _accountFilter.Width = 240;
            filters.Controls.Add(_accountFilter);
            AddFilterLabel(filters, "From");
            ConfigureDatePicker(_fromDate);
            filters.Controls.Add(_fromDate);
            AddFilterLabel(filters, "To");
            ConfigureDatePicker(_toDate);
            filters.Controls.Add(_toDate);
            AddFilterLabel(filters, "Breakdown");
            _breakdownMode.DropDownStyle = ComboBoxStyle.DropDownList;
            _breakdownMode.Items.AddRange(new object[] { ExpenseBreakdownModes.Category, ExpenseBreakdownModes.CategoryType });
            _breakdownMode.SelectedIndex = 0;
            _breakdownMode.Width = 132;
            filters.Controls.Add(_breakdownMode);
            AddFilterLabel(filters, "Max Detail");
            _maxDetailRows.Minimum = 50;
            _maxDetailRows.Maximum = 10000;
            _maxDetailRows.Value = 1000;
            _maxDetailRows.Width = 82;
            filters.Controls.Add(_maxDetailRows);
            Button refresh = new() { Text = "Refresh", Width = 92, Height = 30 };
            UiTheme.StylePrimaryButton(refresh);
            refresh.Click += async (_, _) => await RefreshReportsAsync().ConfigureAwait(true);
            filters.Controls.Add(refresh);

            FlowLayoutPanel quickRanges = new()
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = false,
                BackColor = UiTheme.CardBackground
            };

            layout.Controls.Add(quickRanges, 0, 1);
            AddRangeButton(quickRanges, "Current Month", SetCurrentMonthAsync, 122);
            AddRangeButton(quickRanges, "Current Year", SetCurrentYearAsync, 112);
            AddRangeButton(quickRanges, "Last 3 Months", SetLastThreeMonthsAsync, 124);
            AddRangeButton(quickRanges, "Last 12 Months", SetLast12MonthsAsync, 130);
            AddRangeButton(quickRanges, "All", SetAllAsync, 70);
            Button export = new() { Text = "Export Detail CSV", Width = 150, Height = 30 };
            UiTheme.StyleSecondaryButton(export);
            export.Click += (_, _) => ExportDetailsCsv();
            quickRanges.Controls.Add(export);
            _periodLabel.Dock = DockStyle.Fill;
            _periodLabel.Tag = "SecondaryText";
            _periodLabel.TextAlign = ContentAlignment.MiddleRight;
            _periodLabel.Padding = new Padding(0, 4, 2, 0);
            layout.Controls.Add(_periodLabel, 0, 2);
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
            valueLabel.Font = new Font("Segoe UI Semibold", 12.5F, FontStyle.Regular);
            valueLabel.TextAlign = ContentAlignment.MiddleLeft;
            valueLabel.AutoEllipsis = true;
            layout.Controls.Add(valueLabel, 0, 1);
            return card;
        }

        private static CardPanel BuildChartCard(string title, string subtitle, Control content)
        {
            CardPanel card = new()
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 12, 0),
                Padding = new Padding(16, 12, 16, 16)
            };

            TableLayoutPanel layout = new()
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                BackColor = UiTheme.CardBackground
            };

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            card.Controls.Add(layout);

            Label titleLabel = new()
            {
                Text = title,
                Dock = DockStyle.Fill,
                Font = UiTheme.SectionTitleFont,
                TextAlign = ContentAlignment.MiddleLeft
            };

            layout.Controls.Add(titleLabel, 0, 0);

            Label subtitleLabel = new()
            {
                Text = subtitle,
                Dock = DockStyle.Fill,
                Font = UiTheme.SmallFont,
                Tag = "SecondaryText",
                TextAlign = ContentAlignment.MiddleLeft
            };

            layout.Controls.Add(subtitleLabel, 0, 1);
            content.Dock = DockStyle.Fill;
            content.Margin = new Padding(0, 8, 0, 0);
            layout.Controls.Add(content, 0, 2);
            return card;
        }

        private void BuildGridTabs(Control parent, Control overview)
        {
            TabControl tabs = new()
            {
                Dock = DockStyle.Fill
            };

            parent.Controls.Add(tabs);
            TabPage overviewTab = new("Charts");
            overviewTab.Controls.Add(overview);
            tabs.TabPages.Add(overviewTab);

            TabPage summaryTab = new("Expense Summary");
            tabs.TabPages.Add(summaryTab);
            ConfigureSummaryGrid();
            summaryTab.Controls.Add(_summaryGrid);
            TabPage monthlyTab = new("Monthly Totals");
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

        private void ConfigureSummaryGrid()
        {
            ConfigureGrid(_summaryGrid, _summarySource);
            _summaryGrid.Columns.Add(MakeTextColumn("GroupName", "Group", 260));
            _summaryGrid.Columns.Add(MakeTextColumn("TotalAmount", "Total", 130, "C2"));
            _summaryGrid.Columns.Add(MakeTextColumn("TransactionCount", "Count", 90));
            _summaryGrid.Columns.Add(MakeTextColumn("PercentOfTotal", "% of Total", 100, "N2"));
        }

        private void ConfigureMonthlyGrid()
        {
            ConfigureGrid(_monthlyGrid, _monthlySource);
            _monthlyGrid.Columns.Add(MakeTextColumn("MonthLabel", "Month", 120));
            _monthlyGrid.Columns.Add(MakeTextColumn("TotalAmount", "Total", 130, "C2"));
            _monthlyGrid.Columns.Add(MakeTextColumn("TransactionCount", "Count", 90));
        }

        private void ConfigureDetailGrid()
        {
            ConfigureGrid(_detailGrid, _detailSource);
            _detailGrid.Columns.Add(MakeTextColumn("TransactionDate", "Date", 95));
            _detailGrid.Columns.Add(MakeTextColumn("AccountNickname", "Account", 170));
            _detailGrid.Columns.Add(MakeTextColumn("Amount", "Expense", 110, "C2"));
            _detailGrid.Columns.Add(MakeTextColumn("Type", "Type", 110));
            _detailGrid.Columns.Add(MakeTextColumn("Description", "Description", 420));
            _detailGrid.Columns.Add(MakeTextColumn("Category", "Category", 170));
            _detailGrid.Columns.Add(MakeTextColumn("SpendingClass", "Category Type", 170));
            _detailGrid.Columns.Add(MakeTextColumn("Merchant", "Merchant", 200));
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
            List<AccountOption> options = new() { new AccountOption(null, "All Accounts") };
            options.AddRange(_accounts.Select(account => new AccountOption(account.AccountId, account.DisplayName)));
            _accountFilter.DataSource = options;
            _accountFilter.DisplayMember = nameof(AccountOption.DisplayName);
            _accountFilter.ValueMember = nameof(AccountOption.AccountId);
        }

        private async Task SetCurrentMonthAsync()
        {
            DateTime today = DateTime.Today;
            SetDateRange(new DateTime(today.Year, today.Month, 1), today);
            await RefreshReportsAsync().ConfigureAwait(true);
        }

        private async Task SetCurrentYearAsync()
        {
            DateTime today = DateTime.Today;
            SetDateRange(new DateTime(today.Year, 1, 1), today);
            await RefreshReportsAsync().ConfigureAwait(true);
        }

        private async Task SetLastThreeMonthsAsync()
        {
            DateTime today = DateTime.Today;
            SetDateRange(new DateTime(today.Year, today.Month, 1).AddMonths(-2), today);
            await RefreshReportsAsync().ConfigureAwait(true);
        }

        private async Task SetLast12MonthsAsync()
        {
            DateTime today = DateTime.Today;
            SetDateRange(new DateTime(today.Year, today.Month, 1).AddMonths(-11), today);
            await RefreshReportsAsync().ConfigureAwait(true);
        }

        private async Task SetAllAsync()
        {
            _fromDate.Checked = false;
            _toDate.Checked = false;
            await RefreshReportsAsync().ConfigureAwait(true);
        }

        private void SetDateRange(DateTime fromDate, DateTime toDate)
        {
            _fromDate.Checked = true;
            _fromDate.Value = fromDate;
            _toDate.Checked = true;
            _toDate.Value = toDate;
        }

        private async Task RefreshReportsAsync()
        {
            ExpenseReportFilter filter = BuildFilter();

            if (filter.FromDate.HasValue && filter.ToDate.HasValue && filter.FromDate > filter.ToDate)
            {
                MessageBox.Show(this, "The start date must be on or before the end date.", "Expense Analysis", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ExpenseReportResult report = await _analysisService.GetExpenseReportAsync(filter).ConfigureAwait(true);
            ExpenseMetricSummary metrics = report.Metrics;
            IReadOnlyList<ExpenseSummaryItem> summary = report.Summary;
            IReadOnlyList<MonthlyExpenseTotalItem> monthly = report.Monthly;
            IReadOnlyList<ExpenseDetailItem> details = report.Details;
            _currentDetailRows = details;
            _summarySource.DataSource = summary;
            _monthlySource.DataSource = monthly;
            _detailSource.DataSource = details;
            UpdateMetrics(metrics, summary, monthly, report.Period);
            _reviewNotes.Lines = report.Warnings.Append(report.Period.CoverageNote).ToArray();
            UiTheme.SetTextRole(_periodLabel, report.Warnings.Count > 0 ? "WarningText" : "SecondaryText");
            if (report.Warnings.Count > 0)
                _periodLabel.Text = "Transaction relationships require review; totals include unresolved entries. See Review notes.";
            _pieChart.SetData(summary);
            _monthlyChart.SetData(monthly);
        }

        private ExpenseReportFilter BuildFilter()
        {
            AccountOption? option = _accountFilter.SelectedItem as AccountOption;

            return new ExpenseReportFilter
            {
                AccountId = option?.AccountId,
                FromDate = _fromDate.Checked ? DateOnly.FromDateTime(_fromDate.Value.Date) : null,
                ToDate = _toDate.Checked ? DateOnly.FromDateTime(_toDate.Value.Date) : null,
                BreakdownMode = _breakdownMode.SelectedItem?.ToString() ?? ExpenseBreakdownModes.Category,
                MaxDetailRows = decimal.ToInt32(_maxDetailRows.Value)
            };
        }

        private void UpdateMetrics(ExpenseMetricSummary metrics, IReadOnlyList<ExpenseSummaryItem> summary, IReadOnlyList<MonthlyExpenseTotalItem> monthly, ReportPeriod period)
        {
            decimal total = metrics.TotalExpenses;
            decimal months = period.MonthEquivalent;
            int days = period.DayCount;
            ExpenseSummaryItem? topCategory = summary.OrderByDescending(item => item.TotalAmount).FirstOrDefault();
            MonthlyExpenseTotalItem? topMonth = monthly.OrderByDescending(item => item.TotalAmount).FirstOrDefault();
            _totalExpensesMetric.Text = period.HasObservedTransactions ? total.ToString("C2", CultureInfo.CurrentCulture) : "Unknown";
            _averageMonthMetric.Text = period.HasObservedTransactions && months > 0M ? (total / months).ToString("C2", CultureInfo.CurrentCulture) : "Unknown";
            _dailyAverageMetric.Text = period.HasObservedTransactions && days > 0 ? (total / days).ToString("C2", CultureInfo.CurrentCulture) : "Unknown";
            _averageTransactionMetric.Text = metrics.TransactionCount == 0 ? "$0.00" : (total / metrics.TransactionCount).ToString("C2", CultureInfo.CurrentCulture);
            _largestCategoryMetric.Text = topCategory is null ? "None" : $"{topCategory.GroupName} {topCategory.TotalAmount:C0}";
            _largestMonthMetric.Text = topMonth is null ? "None" : $"{topMonth.MonthLabel} {topMonth.TotalAmount:C0}";

            string rangeText = period.FromDate.HasValue && period.ToDate.HasValue
                ? period.FromDate.Value.ToString("MMM d, yyyy", CultureInfo.CurrentCulture) + " - " + period.ToDate.Value.ToString("MMM d, yyyy", CultureInfo.CurrentCulture)
                : "No expense rows in selected period";

            _periodLabel.Text = $"{rangeText}     Total: {total:C2}     Groups: {summary.Count}     Rows: {metrics.TransactionCount}     Import coverage unverified";
        }

        private void ExportDetailsCsv()
        {
            if (_currentDetailRows.Count == 0)
            {
                MessageBox.Show(this, "There are no detail rows to export.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using SaveFileDialog dialog = new()
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = "personal-finance-manager-expense-detail.csv"
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            StringBuilder builder = new();
            builder.AppendLine("Date,Account,Amount,Type,Description,Category,Category Type,Merchant");

            foreach (ExpenseDetailItem row in _currentDetailRows)
            {
                builder.Append(Csv(row.TransactionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))).Append(',')
                    .Append(Csv(row.AccountNickname)).Append(',')
                    .Append(Csv(row.Amount.ToString("0.00", CultureInfo.InvariantCulture))).Append(',')
                    .Append(Csv(row.Type)).Append(',')
                    .Append(Csv(row.Description)).Append(',')
                    .Append(Csv(row.Category)).Append(',')
                    .Append(Csv(row.SpendingClass)).Append(',')
                    .Append(Csv(row.Merchant)).AppendLine();
            }

            File.WriteAllText(dialog.FileName, builder.ToString(), Encoding.UTF8);
            MessageBox.Show(this, "Export complete.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static string Csv(string? value)
        {
            string text = value ?? string.Empty;
            return '"' + text.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
        }

        private sealed record AccountOption(int? AccountId, string DisplayName);

        private sealed class PieExpenseChartControl : Control
        {
            private const int _maxLegendSlices = 20;

            public static readonly Color[] _palette =
            {
                Color.FromArgb(37, 99, 235),
                Color.FromArgb(220, 38, 38),
                Color.FromArgb(22, 163, 74),
                Color.FromArgb(245, 158, 11),
                Color.FromArgb(124, 58, 237),
                Color.FromArgb(8, 145, 178),
                Color.FromArgb(249, 115, 22),
                Color.FromArgb(14, 116, 144),
                Color.FromArgb(225, 29, 72),
                Color.FromArgb(161, 98, 7),
                Color.FromArgb(75, 85, 99),
                Color.FromArgb(13, 148, 136),
                Color.FromArgb(147, 51, 234),
                Color.FromArgb(202, 138, 4),
                Color.FromArgb(219, 39, 119),
                Color.FromArgb(5, 150, 105),
                Color.FromArgb(100, 116, 139)
            };

            private IReadOnlyList<ExpenseSummaryItem> _items = Array.Empty<ExpenseSummaryItem>();

            public PieExpenseChartControl()
            {
                DoubleBuffered = true;
                BackColor = UiTheme.CardBackground;
            }

            public void SetData(IReadOnlyList<ExpenseSummaryItem> items)
            {
                _items = BuildDisplayItems(items);
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                decimal total = _items.Sum(item => item.TotalAmount);

                if (_items.Count == 0 || total <= 0M)
                {
                    DrawEmpty(e.Graphics, ClientRectangle);
                    return;
                }

                int legendWidth = Math.Min(430, Math.Max(340, (int)(Width * 0.38)));
                int availablePieWidth = Math.Max(280, Width - legendWidth - 34);
                int pieDiameter = Math.Min(Math.Max(300, Height - 10), availablePieWidth);
                pieDiameter = Math.Max(280, pieDiameter);
                Rectangle pieBounds = new(10, Math.Max(4, (Height - pieDiameter) / 2), pieDiameter, pieDiameter);
                List<SliceLabel> labels = new();
                float start = -90F;
                using Pen separator = new(UiTheme.CardBackground, 2F);

                for (int index = 0; index < _items.Count; index++)
                {
                    ExpenseSummaryItem item = _items[index];
                    float sweep = (float)(item.TotalAmount / total * 360M);
                    using SolidBrush brush = new(GetColor(index));
                    e.Graphics.FillPie(brush, pieBounds, start, sweep);
                    e.Graphics.DrawPie(separator, pieBounds, start, sweep);
                    labels.Add(new SliceLabel(start, sweep, item.PercentOfTotal));
                    start += sweep;
                }

                DrawPercentLabels(e.Graphics, pieBounds, labels);
                Rectangle legendBounds = new(pieBounds.Right + 22, 2, Math.Max(300, Width - pieBounds.Right - 28), Height - 4);
                DrawLegend(e.Graphics, legendBounds);
            }

            private static IReadOnlyList<ExpenseSummaryItem> BuildDisplayItems(IReadOnlyList<ExpenseSummaryItem> sourceItems)
            {
                decimal positiveTotal = sourceItems.Where(item => item.TotalAmount > 0M).Sum(item => item.TotalAmount);

                List<ExpenseSummaryItem> orderedItems = sourceItems
                    .Where(item => item.TotalAmount > 0M)
                    .OrderByDescending(item => item.TotalAmount)
                    .Select(item => new ExpenseSummaryItem
                    {
                        GroupName = item.GroupName,
                        TotalAmount = item.TotalAmount,
                        TransactionCount = item.TransactionCount,
                        PercentOfTotal = positiveTotal > 0M ? decimal.Round(item.TotalAmount / positiveTotal * 100M, 2, MidpointRounding.AwayFromZero) : 0M
                    })
                    .ToList();

                if (orderedItems.Count <= _maxLegendSlices)
                {
                    return orderedItems;
                }

                List<ExpenseSummaryItem> displayItems = orderedItems.Take(_maxLegendSlices).ToList();
                decimal otherAmount = orderedItems.Skip(_maxLegendSlices).Sum(item => item.TotalAmount);
                int otherCount = orderedItems.Skip(_maxLegendSlices).Sum(item => item.TransactionCount);
                decimal total = orderedItems.Sum(item => item.TotalAmount);

                if (otherAmount > 0M)
                {
                    displayItems.Add(new ExpenseSummaryItem
                    {
                        GroupName = "Other",
                        TotalAmount = otherAmount,
                        TransactionCount = otherCount,
                        PercentOfTotal = total <= 0M ? 0M : decimal.Round(otherAmount / total * 100M, 2, MidpointRounding.AwayFromZero)
                    });
                }

                return displayItems;
            }

            private void DrawLegend(Graphics graphics, Rectangle bounds)
            {
                using Font itemFont = new("Segoe UI", 9F, FontStyle.Regular);
                using Font valueFont = new("Segoe UI Semibold", 9F, FontStyle.Regular);
                using Brush primaryBrush = new SolidBrush(UiTheme.TextPrimary);
                using Brush secondaryBrush = new SolidBrush(UiTheme.TextSecondary);
                using Pen swatchBorder = new(UiTheme.CardBorder);
                int y = bounds.Top + 8;
                int amountX = Math.Max(bounds.Left + 180, bounds.Right - 155);
                int percentX = Math.Max(bounds.Left + 285, bounds.Right - 64);

                for (int index = 0; index < _items.Count && y < bounds.Bottom - 16; index++)
                {
                    ExpenseSummaryItem item = _items[index];
                    using Brush swatchBrush = new SolidBrush(GetColor(index));
                    Rectangle swatch = new(bounds.Left, y + 5, 12, 12);
                    graphics.FillRectangle(swatchBrush, swatch);
                    graphics.DrawRectangle(swatchBorder, swatch);
                    string label = TrimToWidth(graphics, item.GroupName, itemFont, Math.Max(80, amountX - bounds.Left - 26));
                    graphics.DrawString(label, itemFont, primaryBrush, bounds.Left + 20, y);
                    graphics.DrawString(item.TotalAmount.ToString("C2", CultureInfo.CurrentCulture), valueFont, primaryBrush, amountX, y);
                    graphics.DrawString(item.PercentOfTotal.ToString("N2", CultureInfo.CurrentCulture) + "%", itemFont, secondaryBrush, percentX, y);
                    y += 24;
                }

                if (_items.Count > 0)
                {
                    decimal total = _items.Sum(item => item.TotalAmount);
                    using Font totalFont = new("Segoe UI Semibold", 9F, FontStyle.Regular);
                    graphics.DrawString("Positive category balances: " + total.ToString("C2", CultureInfo.CurrentCulture), totalFont, primaryBrush, bounds.Left, Math.Max(y + 8, bounds.Bottom - 24));
                }
            }

            private static void DrawPercentLabels(Graphics graphics, Rectangle pieBounds, IReadOnlyList<SliceLabel> labels)
            {
                using Font largeFont = new("Segoe UI Semibold", 8.5F, FontStyle.Regular);
                using Font smallFont = new("Segoe UI Semibold", 7.25F, FontStyle.Regular);
                using Brush whiteBrush = new SolidBrush(Color.White);
                using Brush shadowBrush = new SolidBrush(Color.FromArgb(120, 0, 0, 0));
                using StringFormat centerFormat = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                List<RectangleF> usedBounds = new();

                foreach (SliceLabel label in labels)
                {
                    if (label.Percent <= 0M)
                    {
                        continue;
                    }

                    string text = label.Percent.ToString(label.Percent < 1M ? "0.#" : "0.##", CultureInfo.CurrentCulture) + "%";
                    Font font = label.Percent < 4M ? smallFont : largeFont;
                    RectangleF labelBounds = ChooseLabelBounds(graphics, pieBounds, label, text, font, usedBounds);
                    DrawOutlinedText(graphics, text, font, whiteBrush, shadowBrush, labelBounds, centerFormat);
                    usedBounds.Add(labelBounds);
                }
            }

            private static RectangleF ChooseLabelBounds(Graphics graphics, Rectangle pieBounds, SliceLabel label, string text, Font font, IReadOnlyList<RectangleF> usedBounds)
            {
                SizeF measured = graphics.MeasureString(text, font);
                float width = Math.Max(32F, measured.Width + 4F);
                float height = Math.Max(14F, measured.Height + 2F);
                double middle = Math.PI * (label.StartAngle + label.SweepAngle / 2F) / 180D;
                float pieRadius = Math.Min(pieBounds.Width, pieBounds.Height) / 2F;
                float[] radiusFactors = label.Percent < 2M ? new[] { 0.82F, 0.72F, 0.62F, 0.90F, 0.52F } : new[] { 0.58F, 0.68F, 0.78F, 0.48F };
                PointF center = new(pieBounds.Left + pieBounds.Width / 2F, pieBounds.Top + pieBounds.Height / 2F);
                RectangleF best = RectangleF.Empty;

                foreach (float factor in radiusFactors)
                {
                    PointF labelCenter = new(center.X + (float)(Math.Cos(middle) * pieRadius * factor), center.Y + (float)(Math.Sin(middle) * pieRadius * factor));
                    RectangleF candidate = new(labelCenter.X - width / 2F, labelCenter.Y - height / 2F, width, height);

                    if (IsMostlyInsideCircle(candidate, center, pieRadius) && !usedBounds.Any(existing => existing.IntersectsWith(candidate)))
                    {
                        return candidate;
                    }

                    best = candidate;
                }

                return best == RectangleF.Empty ? new RectangleF(center.X - width / 2F, center.Y - height / 2F, width, height) : best;
            }

            private static bool IsMostlyInsideCircle(RectangleF rectangle, PointF center, float radius)
            {
                PointF[] points =
                {
                    new(rectangle.Left, rectangle.Top),
                    new(rectangle.Right, rectangle.Top),
                    new(rectangle.Left, rectangle.Bottom),
                    new(rectangle.Right, rectangle.Bottom),
                    new(rectangle.Left + rectangle.Width / 2F, rectangle.Top + rectangle.Height / 2F)
                };

                return points.Count(point => Distance(point, center) <= radius + 2F) >= 3;
            }

            private static float Distance(PointF left, PointF right)
            {
                float dx = left.X - right.X;
                float dy = left.Y - right.Y;
                return (float)Math.Sqrt(dx * dx + dy * dy);
            }

            private static void DrawOutlinedText(Graphics graphics, string text, Font font, Brush textBrush, Brush shadowBrush, RectangleF bounds, StringFormat format)
            {
                RectangleF shadowBounds = new(bounds.Left + 1F, bounds.Top + 1F, bounds.Width, bounds.Height);
                graphics.DrawString(text, font, shadowBrush, shadowBounds, format);
                graphics.DrawString(text, font, textBrush, bounds, format);
            }

            private static Color GetColor(int index)
            {
                return _palette[index % _palette.Length];
            }

            private readonly record struct SliceLabel(float StartAngle, float SweepAngle, decimal Percent);
        }

        private sealed class MonthlyExpenseChartControl : Control
        {
            private IReadOnlyList<MonthlyExpenseTotalItem> _items = Array.Empty<MonthlyExpenseTotalItem>();

            public MonthlyExpenseChartControl()
            {
                DoubleBuffered = true;
                BackColor = UiTheme.CardBackground;
            }

            public void SetData(IReadOnlyList<MonthlyExpenseTotalItem> items)
            {
                _items = items;
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                if (_items.Count == 0 || _items.All(item => item.TotalAmount == 0M))
                {
                    DrawEmpty(e.Graphics, ClientRectangle);
                    return;
                }

                Rectangle plot = new(74, 12, Math.Max(60, Width - 94), Math.Max(80, Height - 64));
                decimal max = Math.Max(1M, RoundAxisMaximum(_items.Max(item => item.TotalAmount)));
                decimal min = Math.Min(0M, _items.Min(item => item.TotalAmount));
                DrawChartGrid(e.Graphics, plot, min, max);
                DrawBars(e.Graphics, plot, min, max);
            }

            private void DrawBars(Graphics graphics, Rectangle plot, decimal min, decimal max)
            {
                int gap = _items.Count > 18 ? 6 : 12;
                int barWidth = Math.Max(8, Math.Min(46, (plot.Width - gap * (_items.Count + 1)) / Math.Max(1, _items.Count)));
                int usedWidth = _items.Count * barWidth + (_items.Count + 1) * gap;
                int startX = plot.Left + Math.Max(0, (plot.Width - usedWidth) / 2) + gap;
                using StringFormat center = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };

                for (int index = 0; index < _items.Count; index++)
                {
                    MonthlyExpenseTotalItem item = _items[index];
                    int zeroY = plot.Bottom - (int)Math.Round((double)(-min / (max - min)) * plot.Height, MidpointRounding.AwayFromZero);
                    int valueY = plot.Bottom - (int)Math.Round((double)((item.TotalAmount - min) / (max - min)) * plot.Height, MidpointRounding.AwayFromZero);
                    int x = startX + index * (barWidth + gap);
                    Rectangle bar = new(x, Math.Min(zeroY, valueY), barWidth, Math.Max(1, Math.Abs(zeroY - valueY)));
                    Color baseColor = PieExpenseChartControl._palette[index % PieExpenseChartControl._palette.Length];
                    using LinearGradientBrush brush = new(bar, Lighten(baseColor, 0.14F), baseColor, LinearGradientMode.Vertical);
                    graphics.FillRectangle(brush, bar);
                    using Pen outline = new(UiTheme.CardBorder);
                    graphics.DrawRectangle(outline, bar);
                    string label = item.MonthStart.ToString("MMM", CultureInfo.CurrentCulture) + Environment.NewLine + item.MonthStart.ToString("yyyy", CultureInfo.CurrentCulture);
                    Rectangle labelBounds = new(x - 22, plot.Bottom + 6, barWidth + 44, 38);
                    using Brush monthLabelBrush = new SolidBrush(UiTheme.TextPrimary);
                    graphics.DrawString(label, UiTheme.SmallFont, monthLabelBrush, labelBounds, center);
                }
            }

            private static void DrawChartGrid(Graphics graphics, Rectangle plot, decimal min, decimal max)
            {
                using Pen axisPen = new(UiTheme.TextMuted);
                using Pen gridPen = new(UiTheme.GridLine) { DashStyle = DashStyle.Dash };
                using Font gridLabelFont = new("Segoe UI", 8F, FontStyle.Regular);
                using Brush gridLabelBrush = new SolidBrush(UiTheme.TextSecondary);
                using StringFormat right = new() { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

                for (int step = 0; step <= 5; step++)
                {
                    decimal value = min + (max - min) / 5M * step;
                    int y = plot.Bottom - (int)Math.Round(plot.Height * step / 5D, MidpointRounding.AwayFromZero);
                    graphics.DrawLine(step == 0 ? axisPen : gridPen, plot.Left, y, plot.Right, y);
                    Rectangle labelBounds = new(0, y - 10, plot.Left - 8, 20);
                    graphics.DrawString(value.ToString("C0", CultureInfo.CurrentCulture), gridLabelFont, gridLabelBrush, labelBounds, right);
                }

                graphics.DrawLine(axisPen, plot.Left, plot.Top, plot.Left, plot.Bottom);
            }
        }

        private static void DrawEmpty(Graphics graphics, Rectangle bounds)
        {
            using Font font = new("Segoe UI", 10F, FontStyle.Regular);
            using Brush brush = new SolidBrush(UiTheme.TextSecondary);
            using StringFormat format = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            graphics.DrawString("No expense data for the selected period", font, brush, bounds, format);
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

        private static string TrimToWidth(Graphics graphics, string text, Font font, int maxWidth)
        {
            if (graphics.MeasureString(text, font).Width <= maxWidth)
            {
                return text;
            }

            const string ellipsis = "...";

            for (int length = text.Length - 1; length > 0; length--)
            {
                string candidate = text[..length] + ellipsis;

                if (graphics.MeasureString(candidate, font).Width <= maxWidth)
                {
                    return candidate;
                }
            }

            return ellipsis;
        }
    }
}
