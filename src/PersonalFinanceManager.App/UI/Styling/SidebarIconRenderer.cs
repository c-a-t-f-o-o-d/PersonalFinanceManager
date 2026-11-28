using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace PersonalFinanceManager.App.UI.Styling
{
    public enum SidebarIcon
    {
        Accounts,
        Transactions,
        Imports,
        Rules,
        DuplicateReview,
        TransactionLinks,
        DataHealth,
        ExpenseAnalysis,
        MoneyInOut,
        SalaryNeeded,
        Runway,
        Bills,
        Setup,
        BackupExport,
        Settings,
        Database,
        Sun,
        Moon
    }

    internal static class SidebarIconRenderer
    {
        public static Bitmap Render(SidebarIcon icon, int size, int textGap, int dpi, Color highContrastColor)
        {
            Bitmap image = new(size + textGap, size, PixelFormat.Format32bppPArgb);
            image.SetResolution(dpi, dpi);
            using Graphics graphics = Graphics.FromImage(image);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.ScaleTransform(size / 24F, size / 24F);
            using Pen pen = new(GetColor(icon, highContrastColor), 1.8F)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };

            switch (icon)
            {
                case SidebarIcon.Accounts:
                    graphics.DrawPolygon(pen, [new PointF(3, 8), new PointF(12, 3), new PointF(21, 8)]);
                    graphics.DrawLine(pen, 3, 21, 21, 21);
                    graphics.DrawLine(pen, 4, 18, 20, 18);

                    foreach (int x in new[] { 6, 12, 18 })
                    {
                        graphics.DrawLine(pen, x, 11, x, 18);
                    }

                    break;
                case SidebarIcon.Transactions:
                    graphics.DrawLine(pen, 4, 7, 20, 7);
                    graphics.DrawLines(pen, [new PointF(16, 3), new PointF(20, 7), new PointF(16, 11)]);
                    graphics.DrawLine(pen, 20, 17, 4, 17);
                    graphics.DrawLines(pen, [new PointF(8, 13), new PointF(4, 17), new PointF(8, 21)]);
                    break;
                case SidebarIcon.Imports:
                    graphics.DrawLine(pen, 12, 3, 12, 15);
                    graphics.DrawLines(pen, [new PointF(7, 10), new PointF(12, 15), new PointF(17, 10)]);
                    graphics.DrawLines(pen, [new PointF(3, 15), new PointF(3, 21), new PointF(21, 21), new PointF(21, 15)]);
                    break;
                case SidebarIcon.Rules:
                    foreach (int y in new[] { 5, 12, 19 })
                    {
                        graphics.DrawLines(pen, [new PointF(3, y), new PointF(5, y + 2), new PointF(8, y - 2)]);
                        graphics.DrawLine(pen, 12, y, 21, y);
                    }

                    break;
                case SidebarIcon.DuplicateReview:
                    graphics.DrawLines(pen, [new PointF(5, 17), new PointF(3, 17), new PointF(3, 3), new PointF(16, 3), new PointF(16, 5)]);
                    DrawRoundedRectangle(graphics, pen, new RectangleF(7, 7, 14, 14), 2);
                    graphics.DrawLine(pen, 11, 12, 17, 12);
                    graphics.DrawLine(pen, 11, 16, 15, 16);
                    break;
                case SidebarIcon.TransactionLinks:
                    GraphicsState linkState = graphics.Save();
                    graphics.TranslateTransform(12, 12);
                    graphics.RotateTransform(-35);
                    DrawRoundedRectangle(graphics, pen, new RectangleF(-10, -4, 12, 8), 4);
                    DrawRoundedRectangle(graphics, pen, new RectangleF(-2, -4, 12, 8), 4);
                    graphics.DrawLine(pen, -3, 0, 3, 0);
                    graphics.Restore(linkState);
                    break;
                case SidebarIcon.DataHealth:
                    using (GraphicsPath shield = new())
                    {
                        shield.AddLines([new PointF(12, 3), new PointF(20, 6), new PointF(19, 14)]);
                        shield.AddBezier(19, 14, 18, 18, 15, 20, 12, 21);
                        shield.AddBezier(12, 21, 9, 20, 6, 18, 5, 14);
                        shield.AddLine(5, 14, 4, 6);
                        shield.CloseFigure();
                        graphics.DrawPath(pen, shield);
                    }

                    graphics.DrawLines(pen, [new PointF(8, 12), new PointF(11, 15), new PointF(16, 9)]);
                    break;
                case SidebarIcon.ExpenseAnalysis:
                    graphics.DrawEllipse(pen, 3, 3, 18, 18);
                    graphics.DrawLines(pen, [new PointF(12, 3), new PointF(12, 12), new PointF(20, 16)]);
                    break;
                case SidebarIcon.MoneyInOut:
                    graphics.DrawLine(pen, 7, 21, 7, 3);
                    graphics.DrawLines(pen, [new PointF(3, 7), new PointF(7, 3), new PointF(11, 7)]);
                    graphics.DrawLine(pen, 17, 3, 17, 21);
                    graphics.DrawLines(pen, [new PointF(13, 17), new PointF(17, 21), new PointF(21, 17)]);
                    break;
                case SidebarIcon.SalaryNeeded:
                    DrawRoundedRectangle(graphics, pen, new RectangleF(3, 8, 18, 13), 2);
                    graphics.DrawLines(pen, [new PointF(8, 8), new PointF(8, 3), new PointF(16, 3), new PointF(16, 8)]);
                    graphics.DrawLine(pen, 3, 13, 9, 13);
                    graphics.DrawLine(pen, 15, 13, 21, 13);
                    graphics.DrawRectangle(pen, 9, 12, 6, 4);
                    break;
                case SidebarIcon.Runway:
                    graphics.DrawPolygon(pen, [new PointF(5, 3), new PointF(19, 3), new PointF(19, 7), new PointF(14, 12),
                        new PointF(19, 17), new PointF(19, 21), new PointF(5, 21), new PointF(5, 17), new PointF(10, 12), new PointF(5, 7)]);
                    graphics.DrawLine(pen, 8, 6, 16, 6);
                    graphics.DrawLine(pen, 8, 18, 16, 18);
                    break;
                case SidebarIcon.Bills:
                    DrawRoundedRectangle(graphics, pen, new RectangleF(3, 5, 18, 16), 2);
                    graphics.DrawLine(pen, 7, 3, 7, 7);
                    graphics.DrawLine(pen, 17, 3, 17, 7);
                    graphics.DrawLine(pen, 3, 10, 21, 10);
                    graphics.DrawLines(pen, [new PointF(8, 16), new PointF(11, 19), new PointF(17, 13)]);
                    break;
                case SidebarIcon.Setup:
                    graphics.DrawLine(pen, 5, 3, 5, 21);
                    graphics.DrawLines(pen, [new PointF(5, 4), new PointF(20, 4), new PointF(16, 8), new PointF(20, 12), new PointF(5, 12)]);
                    break;
                case SidebarIcon.BackupExport:
                    using (GraphicsPath cloud = new())
                    {
                        cloud.AddBezier(7, 17, 1, 18, 1, 10, 5, 9);
                        cloud.AddBezier(5, 9, 4, 3, 14, 1, 17, 7);
                        cloud.AddBezier(17, 7, 24, 7, 24, 17, 18, 17);
                        graphics.DrawPath(pen, cloud);
                    }

                    graphics.DrawLine(pen, 12, 21, 12, 11);
                    graphics.DrawLines(pen, [new PointF(8, 15), new PointF(12, 11), new PointF(16, 15)]);
                    break;
                case SidebarIcon.Settings:
                    PointF[] gear = new PointF[32];

                    for (int index = 0; index < gear.Length; index++)
                    {
                        double angle = Math.PI * index / 16;
                        float radius = index % 4 is 0 or 3 ? 9.5F : 7.5F;
                        gear[index] = new PointF(12 + radius * (float)Math.Cos(angle), 12 + radius * (float)Math.Sin(angle));
                    }

                    graphics.DrawPolygon(pen, gear);
                    graphics.DrawEllipse(pen, 9, 9, 6, 6);
                    break;
                case SidebarIcon.Database:
                    graphics.DrawEllipse(pen, 3, 3, 18, 6);
                    graphics.DrawLine(pen, 3, 6, 3, 18);
                    graphics.DrawLine(pen, 21, 6, 21, 18);
                    graphics.DrawArc(pen, 3, 9, 18, 6, 0, 180);
                    graphics.DrawArc(pen, 3, 15, 18, 6, 0, 180);
                    break;
                case SidebarIcon.Sun:
                    graphics.DrawEllipse(pen, 8, 8, 8, 8);

                    for (int index = 0; index < 8; index++)
                    {
                        double angle = Math.PI * index / 4;
                        graphics.DrawLine(pen,
                            12 + 7 * (float)Math.Cos(angle), 12 + 7 * (float)Math.Sin(angle),
                            12 + 10 * (float)Math.Cos(angle), 12 + 10 * (float)Math.Sin(angle));
                    }

                    break;
                case SidebarIcon.Moon:
                    using (GraphicsPath moon = new())
                    {
                        moon.AddBezier(16.5F, 3, 10.5F, 1.5F, 4, 6, 4, 12);
                        moon.AddBezier(4, 12, 4, 18, 8.7F, 21, 13.2F, 21);
                        moon.AddBezier(13.2F, 21, 17.6F, 21, 20.3F, 18, 21, 15);
                        moon.AddBezier(21, 15, 16, 17, 11, 13, 11, 8);
                        moon.AddBezier(11, 8, 11, 5.8F, 13.2F, 3.5F, 16.5F, 3);
                        moon.CloseFigure();
                        graphics.DrawPath(pen, moon);
                    }

                    break;
            }

            return image;
        }

        private static Color GetColor(SidebarIcon icon, Color highContrastColor)
        {
            if (SystemInformation.HighContrast)
            {
                return highContrastColor;
            }

            return icon switch
            {
                SidebarIcon.Accounts or SidebarIcon.BackupExport => UiTheme.IsDarkMode ? Color.FromArgb(112, 181, 255) : Color.FromArgb(37, 99, 190),
                SidebarIcon.Transactions or SidebarIcon.SalaryNeeded => UiTheme.IsDarkMode ? Color.FromArgb(192, 155, 255) : Color.FromArgb(112, 65, 179),
                SidebarIcon.Imports or SidebarIcon.MoneyInOut => UiTheme.IsDarkMode ? Color.FromArgb(81, 212, 195) : Color.FromArgb(13, 119, 114),
                SidebarIcon.Rules or SidebarIcon.Sun => UiTheme.IsDarkMode ? Color.FromArgb(251, 201, 105) : Color.FromArgb(146, 91, 12),
                SidebarIcon.DuplicateReview or SidebarIcon.Bills => UiTheme.IsDarkMode ? Color.FromArgb(255, 170, 112) : Color.FromArgb(168, 89, 18),
                SidebarIcon.TransactionLinks or SidebarIcon.Moon => UiTheme.IsDarkMode ? Color.FromArgb(169, 172, 255) : Color.FromArgb(85, 83, 180),
                SidebarIcon.DataHealth or SidebarIcon.Setup => UiTheme.IsDarkMode ? Color.FromArgb(105, 218, 155) : Color.FromArgb(15, 122, 82),
                SidebarIcon.ExpenseAnalysis => UiTheme.IsDarkMode ? Color.FromArgb(248, 153, 185) : Color.FromArgb(180, 55, 100),
                SidebarIcon.Runway or SidebarIcon.Database => UiTheme.IsDarkMode ? Color.FromArgb(95, 207, 235) : Color.FromArgb(0, 111, 135),
                _ => UiTheme.IsDarkMode ? Color.FromArgb(192, 180, 224) : Color.FromArgb(104, 90, 151)
            };
        }

        private static void DrawRoundedRectangle(Graphics graphics, Pen pen, RectangleF bounds, float radius)
        {
            float diameter = radius * 2;
            using GraphicsPath path = new();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            graphics.DrawPath(pen, path);
        }
    }
}
