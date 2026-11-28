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
