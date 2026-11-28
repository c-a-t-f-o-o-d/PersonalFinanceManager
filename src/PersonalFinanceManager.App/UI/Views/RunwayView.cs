using System.Drawing.Drawing2D;
using System.Globalization;
using PersonalFinanceManager.App.UI.Controls;
using PersonalFinanceManager.App.UI.Styling;
using PersonalFinanceManager.Core.Models;
using PersonalFinanceManager.Core.Planning;
using PersonalFinanceManager.Application.Services.Contracts;

namespace PersonalFinanceManager.App.UI.Views
{
    public sealed class RunwayView : UserControl, IPageLayoutControl
    {
        private readonly IRunwayWorkflowService _runwayService;
        private readonly ComboBox _scenario = new();
        private readonly NumericUpDown _monthlyIncome = new();
        private readonly NumericUpDown _projectionMonths = new();
        private readonly Label _availableBalanceMetric = new();
        private readonly Label _monthlyBillsMetric = new();
        private readonly Label _monthlyBurnMetric = new();
        private readonly Label _depletionMetric = new();
        private readonly Label _noteLabel = new();
        private readonly TextBox _assumptions = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
        private readonly RunwayChartControl _chart = new();
        private readonly DataGridView _projectionGrid = new();
        private readonly DataGridView _billGrid = new();
        private readonly BindingSource _projectionSource = new();
        private readonly BindingSource _billSource = new();

        public RunwayView(IRunwayWorkflowService runwayService)
        {
            _runwayService = runwayService;
            InitializeLayout();
        }

        public int PreferredPageHeight => 1100;

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            await RefreshAsync().ConfigureAwait(true);
        }

        private void InitializeLayout()
        {
            BackColor = UiTheme.AppBackground;

            TableLayoutPanel root = new()
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                BackColor = UiTheme.AppBackground
            };

            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
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

            TableLayoutPanel overview = new()
            {
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.AppBackground,
                Margin = Padding.Empty
            };

            overview.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            overview.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
            overview.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

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

            overview.Controls.Add(metricRow, 0, 0);
            metricRow.Controls.Add(BuildMetricCard("Available Balance", _availableBalanceMetric), 0, 0);
            metricRow.Controls.Add(BuildMetricCard("Required Bills / Month", _monthlyBillsMetric), 1, 0);
            metricRow.Controls.Add(BuildMetricCard("Net Monthly Burn", _monthlyBurnMetric), 2, 0);
            metricRow.Controls.Add(BuildMetricCard("Estimated Depletion", _depletionMetric), 3, 0);
            PageLayout.ResponsiveMetrics(overview, 0, metricRow, 2, 850);

            CardPanel chartCard = new()
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                Margin = new Padding(0, 0, 0, 12)
            };

            BuildChartCard(chartCard);

            overview.Controls.Add(chartCard, 0, 1);

            CardPanel gridCard = new()
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12)
            };

            root.Controls.Add(gridCard, 0, 1);
            BuildGridTabs(gridCard, overview);
            PageLayout.AutoSizeHeaders(root, 0);
            UiTheme.ApplyTo(this);
        }

        private void BuildFilterCard(Control parent)
        {
            TableLayoutPanel layout = new()
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                BackColor = UiTheme.CardBackground
            };

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            parent.Controls.Add(layout);

            FlowLayoutPanel controls = new()
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = false,
                BackColor = UiTheme.CardBackground
            };

            layout.Controls.Add(controls, 0, 0);
            AddFilterLabel(controls, "Scenario");
            _scenario.DropDownStyle = ComboBoxStyle.DropDownList;
            _scenario.Items.AddRange(new object[] { "Strict", "Extended", "Emergency" });
            _scenario.SelectedIndex = 0;
            _scenario.Width = 132;
            controls.Controls.Add(_scenario);
            AddFilterLabel(controls, "Monthly Income");
            _monthlyIncome.Minimum = 0;
            _monthlyIncome.Maximum = 1000000;
            _monthlyIncome.DecimalPlaces = 2;
            _monthlyIncome.Increment = 100;
            _monthlyIncome.Width = 124;
            controls.Controls.Add(_monthlyIncome);
            AddFilterLabel(controls, "Projection Months");
            _projectionMonths.Minimum = 1;
            _projectionMonths.Maximum = 120;
            _projectionMonths.Value = 24;
            _projectionMonths.Width = 72;
            controls.Controls.Add(_projectionMonths);
            Button refresh = new() { Text = "Refresh", Width = 94, Height = 30 };
            UiTheme.StylePrimaryButton(refresh);
            refresh.Click += async (_, _) => await RefreshAsync().ConfigureAwait(true);
            controls.Controls.Add(refresh);
            _noteLabel.Dock = DockStyle.Fill;
            _noteLabel.Tag = "SecondaryText";
            _noteLabel.TextAlign = ContentAlignment.MiddleLeft;
            _noteLabel.Text = "Runway uses current account balances and active required recurring bills. Future employment income defaults to zero unless entered above.";
            layout.Controls.Add(_noteLabel, 0, 1);
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
                Text = "Projected Balance",
                Dock = DockStyle.Fill,
                Font = UiTheme.SectionTitleFont
            };

            layout.Controls.Add(title, 0, 0);

            Label subtitle = new()
            {
                Text = "Estimated balance by month after required recurring expenses and optional income assumption",
                Dock = DockStyle.Fill,
                Tag = "SecondaryText"
            };

            layout.Controls.Add(subtitle, 0, 1);
            _chart.Dock = DockStyle.Fill;
            _chart.Margin = new Padding(0, 8, 0, 0);
            layout.Controls.Add(_chart, 0, 2);
        }

        private void BuildGridTabs(Control parent, TableLayoutPanel overview)
        {
            TabControl tabs = new() { Dock = DockStyle.Fill };
            parent.Controls.Add(tabs);
            TabPage overviewTab = new("Overview");
            PageLayout.ScrollableChart(overviewTab, overview, () => (int)overview.RowStyles[0].Height + overview.LogicalToDeviceUnits(320));
            tabs.TabPages.Add(overviewTab);

            TabPage projectionTab = new("Projection");
            tabs.TabPages.Add(projectionTab);
            ConfigureProjectionGrid();
            projectionTab.Controls.Add(_projectionGrid);
            TabPage billTab = new("Included Required Bills");
            tabs.TabPages.Add(billTab);
            ConfigureBillGrid();
            billTab.Controls.Add(_billGrid);
            TabPage assumptionsTab = new("Assumptions and missing inputs");
            assumptionsTab.Controls.Add(_assumptions);
            tabs.TabPages.Add(assumptionsTab);
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

        private void ConfigureProjectionGrid()
        {
            ConfigureGrid(_projectionGrid, _projectionSource);
            _projectionGrid.Columns.Add(MakeTextColumn("PeriodLabel", "Period ending", 120));
            _projectionGrid.Columns.Add(MakeTextColumn("StartingBalance", "Starting", 130, "C2"));
            _projectionGrid.Columns.Add(MakeTextColumn("MoneyIn", "Income", 130, "C2"));
            _projectionGrid.Columns.Add(MakeTextColumn("RequiredExpenses", "Required Bills", 140, "C2"));
            _projectionGrid.Columns.Add(MakeTextColumn("EndingBalance", "Ending", 130, "C2"));
        }

        private void ConfigureBillGrid()
        {
            ConfigureGrid(_billGrid, _billSource);
            _billGrid.Columns.Add(MakeTextColumn("BillName", "Bill", 220));
            _billGrid.Columns.Add(MakeTextColumn("Payee", "Payee", 220));
            _billGrid.Columns.Add(MakeTextColumn("RequirementType", "Type", 110));
            _billGrid.Columns.Add(MakeTextColumn("Frequency", "Frequency", 100));
            _billGrid.Columns.Add(MakeTextColumn("TypicalAmount", "Amount", 120, "C2"));
            _billGrid.Columns.Add(MakeTextColumn("MonthlyEquivalent", "Monthly Eq.", 120, "C2"));
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

        private async Task RefreshAsync()
        {
            IReadOnlyList<Account> accounts = await _runwayService.GetAccountsAsync().ConfigureAwait(true);
            IReadOnlyList<RecurringBill> bills = await _runwayService.GetBillsAsync().ConfigureAwait(true);

            RunwayAnalysisResult result = new RunwayCalculator().Calculate(
                accounts,
                bills,
                Convert.ToString(_scenario.SelectedItem) ?? "Strict",
                _monthlyIncome.Value,
                decimal.ToInt32(_projectionMonths.Value),
                DateOnly.FromDateTime(DateTime.Today));

            _availableBalanceMetric.Text = result.HasKnownBalance ? result.StartingAvailableBalance.ToString("C2", CultureInfo.CurrentCulture) : "Unknown";
            _monthlyBillsMetric.Text = result.MonthlyRequiredExpenses.ToString("C2", CultureInfo.CurrentCulture);
            _monthlyBurnMetric.Text = result.MonthlyNetBurn.ToString("C2", CultureInfo.CurrentCulture);
            UiTheme.SetTextRole(_monthlyBurnMetric, result.MonthlyNetBurn > 0M ? "DangerText" : "SuccessText");
            _depletionMetric.Text = !result.HasCompleteInputs ? "Incomplete inputs" : result.EstimatedDepletionDate.HasValue ? result.EstimatedDepletionDate.Value.ToString("MMM d, yyyy", CultureInfo.CurrentCulture) : "Beyond range";
            _projectionSource.DataSource = result.ProjectionPoints;
            _billSource.DataSource = result.IncludedBills;
            _chart.SetData(result.ProjectionPoints);
            _noteLabel.Text = $"Projection starts {result.AsOfDate:MMM d, yyyy}. {result.IncludedBills.Count} required bill(s). Review the Assumptions and missing inputs tab before relying on this estimate.";
            _assumptions.Lines = result.Assumptions.ToArray();
        }

        private static void AddFilterLabel(FlowLayoutPanel controls, string text)
        {
            controls.Controls.Add(new Label
            {
                Text = text,
                AutoSize = true,
                Margin = new Padding(5, 8, 5, 0),
                TextAlign = ContentAlignment.MiddleLeft
            });
        }

        private sealed class RunwayChartControl : Control
        {
            private IReadOnlyList<RunwayProjectionPoint> _items = Array.Empty<RunwayProjectionPoint>();

            public RunwayChartControl()
            {
                DoubleBuffered = true;
                BackColor = UiTheme.CardBackground;
            }

            public void SetData(IReadOnlyList<RunwayProjectionPoint> items)
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

                decimal max = Math.Max(1M, _items.Max(item => Math.Max(item.StartingBalance, item.EndingBalance)));
                decimal min = Math.Min(0M, _items.Min(item => item.EndingBalance));
                Rectangle plot = new(82, 16, Math.Max(80, Width - 112), Math.Max(80, Height - 70));
                DrawGrid(e.Graphics, plot, min, max);
                DrawLine(e.Graphics, plot, min, max);
            }

            private void DrawLine(Graphics graphics, Rectangle plot, decimal min, decimal max)
            {
                if (_items.Count == 1)
                {
                    return;
                }

                PointF[] points = new PointF[_items.Count];
                decimal range = max - min;

                for (int index = 0; index < _items.Count; index++)
                {
                    decimal value = _items[index].EndingBalance;
                    float x = plot.Left + plot.Width * index / Math.Max(1F, _items.Count - 1F);
                    float y = plot.Bottom - (float)((value - min) / range) * plot.Height;
                    points[index] = new PointF(x, y);
                }

                using Pen linePen = new(UiTheme.Accent, 3F);
                graphics.DrawLines(linePen, points);
                using Brush pointBrush = new SolidBrush(UiTheme.AccentDark);

                foreach (PointF point in points)
                {
                    graphics.FillEllipse(pointBrush, point.X - 3F, point.Y - 3F, 6F, 6F);
                }
            }

            private static void DrawGrid(Graphics graphics, Rectangle plot, decimal min, decimal max)
            {
                using Pen axisPen = new(UiTheme.TextMuted);
                using Pen gridPen = new(UiTheme.GridLine) { DashStyle = DashStyle.Dash };
                using Font labelFont = new("Segoe UI", 8F, FontStyle.Regular);
                using Brush labelBrush = new SolidBrush(UiTheme.TextSecondary);
                using StringFormat right = new() { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

                for (int step = 0; step <= 5; step++)
                {
                    decimal value = min + (max - min) / 5M * step;
                    int y = plot.Bottom - (int)Math.Round(plot.Height * step / 5D, MidpointRounding.AwayFromZero);
                    graphics.DrawLine(step == 0 ? axisPen : gridPen, plot.Left, y, plot.Right, y);
                    graphics.DrawString(value.ToString("C0", CultureInfo.CurrentCulture), labelFont, labelBrush, new Rectangle(0, y - 10, plot.Left - 8, 20), right);
                }

                graphics.DrawLine(axisPen, plot.Left, plot.Top, plot.Left, plot.Bottom);
            }
        }

        private static void DrawEmpty(Graphics graphics, Rectangle bounds)
        {
            using Font font = new("Segoe UI", 10F, FontStyle.Regular);
            using Brush brush = new SolidBrush(UiTheme.TextSecondary);
            using StringFormat format = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            graphics.DrawString("No runway data. Add current balances and active required bills.", font, brush, bounds, format);
        }
    }
}
