namespace PersonalFinanceManager.App.UI.Controls
{
    internal static class PageLayout
    {
        public static void AutoSizeHeaders(TableLayoutPanel layout, params int[] rows)
        {
            if (layout.ColumnCount <= 1)
            {
                layout.ColumnCount = 1;
                layout.ColumnStyles.Clear();
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            }

            foreach (int row in rows)
            {
                layout.RowStyles[row].SizeType = SizeType.AutoSize;
                Control? header = layout.GetControlFromPosition(0, row);

                if (header is not null)
                {
                    PrepareHeader(header);
                }
            }
        }

        public static void ResponsiveMetrics(TableLayoutPanel parent, int row, TableLayoutPanel metrics, int narrowColumns, int breakpoint)
        {
            Control[] cards = metrics.Controls.Cast<Control>().OrderBy(metrics.GetColumn).ToArray();
            int fullColumns = cards.Length;
            int rowHeight = (int)parent.RowStyles[row].Height;
            bool arranging = false;

            void Arrange()
            {
                if (arranging)
                {
                    return;
                }

                int columns = metrics.ClientSize.Width < metrics.LogicalToDeviceUnits(breakpoint) ? narrowColumns : fullColumns;
                int rows = (cards.Length + columns - 1) / columns;
                int height = metrics.LogicalToDeviceUnits(rowHeight) * rows;

                if (metrics.ColumnCount == columns && metrics.RowCount == rows && parent.RowStyles[row].Height == height)
                {
                    return;
                }

                arranging = true;
                parent.SuspendLayout();
                metrics.SuspendLayout();

                try
                {
                    metrics.ColumnStyles.Clear();
                    metrics.RowStyles.Clear();
                    metrics.ColumnCount = columns;
                    metrics.RowCount = rows;

                    for (int column = 0; column < columns; column++)
                    {
                        metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / columns));
                    }

                    for (int index = 0; index < rows; index++)
                    {
                        metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / rows));
                    }

                    for (int index = 0; index < cards.Length; index++)
                    {
                        metrics.SetCellPosition(cards[index], new TableLayoutPanelCellPosition(index % columns, index / columns));
                    }

                    parent.RowStyles[row].Height = height;
                }
                finally
                {
                    metrics.ResumeLayout(true);
                    parent.ResumeLayout(true);
                    arranging = false;
                }
            }

            metrics.Resize += (_, _) => Arrange();
            metrics.DpiChangedAfterParent += (_, _) => Arrange();
            Arrange();
        }

        public static void ScrollableChart(TabPage page, Control content, int minimumHeight)
        {
            bool arranging = false;
            page.AutoScroll = true;
            content.Dock = DockStyle.None;
            content.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            content.Margin = Padding.Empty;
            page.Controls.Add(content);

            void Arrange()
            {
                if (arranging)
                {
                    return;
                }

                arranging = true;

                try
                {
                    int height = content.LogicalToDeviceUnits(minimumHeight);
                    page.AutoScrollMinSize = new Size(0, height);
                    content.Bounds = new Rectangle(page.AutoScrollPosition, new Size(Math.Max(0, page.ClientSize.Width), Math.Max(height, page.ClientSize.Height)));
                }
                finally
                {
                    arranging = false;
                }
            }

            page.Layout += (_, _) => Arrange();
            page.DpiChangedAfterParent += (_, _) => Arrange();
            Arrange();
        }

        private static void PrepareHeader(Control control)
        {
            if (control is FlowLayoutPanel flow)
            {
                flow.AutoSize = true;
                flow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                flow.WrapContents = true;
                flow.AutoScroll = false;
            }
            else if (control is TableLayoutPanel table)
            {
                table.AutoSize = true;
                table.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                table.AutoScroll = false;

                foreach (RowStyle style in table.RowStyles)
                {
                    style.SizeType = SizeType.AutoSize;
                }

                if (table.ColumnCount <= 1)
                {
                    table.ColumnCount = 1;
                    table.ColumnStyles.Clear();
                    table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                }
            }
            else if (control is Panel panel)
            {
                panel.AutoSize = true;
                panel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                panel.AutoScroll = false;
            }

            if (control is Panel header)
            {
                header.Layout += (_, _) => ConstrainWrappingLabels(header);
            }

            foreach (Control child in control.Controls)
            {
                if (child is Panel)
                {
                    PrepareHeader(child);
                }
                else if (child is Label { Dock: DockStyle.Fill } label)
                {
                    label.AutoSize = true;
                }
            }
        }

        private static void ConstrainWrappingLabels(Panel panel)
        {
            foreach (Label label in panel.Controls.OfType<Label>())
            {
                if (label.AutoSize)
                {
                    int width = Math.Max(1, panel.ClientSize.Width - panel.Padding.Horizontal - label.Margin.Horizontal);
                    Size maximum = new(width, 0);

                    if (label.MaximumSize != maximum)
                    {
                        label.MaximumSize = maximum;
                    }
                }
            }
        }
    }
}
