using System.ComponentModel;
using System.Drawing.Drawing2D;
using PersonalFinanceManager.App.UI.Styling;

namespace PersonalFinanceManager.App.UI.Controls
{
    public sealed class CardPanel : Panel
    {
        public CardPanel()
        {
            BackColor = UiTheme.CardBackground;
            Padding = new Padding(14);
            DoubleBuffered = true;
        }

        [Category("Appearance")]
        [DefaultValue(12)]
        public int CornerRadius
        {
            get;
            set
            {
                int normalizedValue = Math.Max(0, value);

                if (field == normalizedValue)
                {
                    return;
                }

                field = normalizedValue;
                Invalidate();
            }
        } = 12;

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new(0, 0, Width - 1, Height - 1);
            int radius = Math.Min(CornerRadius, Math.Max(0, Math.Min(bounds.Width, bounds.Height) / 2));
            using GraphicsPath path = CreateRoundRectangle(bounds, radius);
            using SolidBrush background = new(UiTheme.CardBackground);
            using Pen border = new(UiTheme.CardBorder);
            e.Graphics.FillPath(background, path);
            e.Graphics.DrawPath(border, path);
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            Invalidate();
        }

        private static GraphicsPath CreateRoundRectangle(Rectangle rectangle, int radius)
        {
            GraphicsPath path = new();

            if (radius <= 0)
            {
                path.AddRectangle(rectangle);
                return path;
            }

            int diameter = radius * 2;
            path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
