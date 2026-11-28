using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using PersonalFinanceManager.App.UI.Styling;
using static PersonalFinanceManager.UiTests.UiTestSupport;
using WinFormsApplication = System.Windows.Forms.Application;

namespace PersonalFinanceManager.UiTests
{
    [TestClass]
    public sealed class DatePickerThemeTests
    {
        private const uint _getMonthCalendar = 0x1008;
        private const uint _closeMonthCalendar = 0x100D;

        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        public void NativeDateFieldAndReopenedCalendarRenderInTheCurrentTheme()
        {
            RunOnUiThread(() =>
            {
                UiTheme.SetDarkMode(false);
                using ThemedForm host = new() { Text = "Date filter preview", ClientSize = new Size(600, 320) };
                DateTimePicker picker = new()
                {
                    Format = DateTimePickerFormat.Short,
                    ShowCheckBox = true,
                    Checked = true,
                    Location = new Point(24, 40),
                    Width = 180,
                    MinDate = new DateTime(2020, 1, 1),
                    MaxDate = new DateTime(2030, 12, 31),
                    Value = new DateTime(2026, 11, 27)
                };
                host.Controls.Add(picker);
                host.Show();
                WinFormsApplication.DoEvents();
                IntPtr originalHandle = picker.Handle;
                DateTime originalValue = picker.Value;
                int changes = 0;
                int opened = 0;
                int closed = 0;
                picker.ValueChanged += (_, _) => changes++;
                picker.DropDown += (_, _) => opened++;
                picker.CloseUp += (_, _) => closed++;
                UiTheme.SetDarkMode(true);
                WinFormsApplication.DoEvents();
                using (Bitmap field = RenderWindow(picker.Handle))
                {
                    SaveImage(field, "date-field-dark", TestContext);
                    AssertDarkSurface(field);
                    AssertFieldHasGlyphs(field);
                }

                for (int opening = 0; opening < 2; opening++)
                {
                    picker.Focus();
                    picker.AccessibilityObject.DoDefaultAction();
                    WinFormsApplication.DoEvents();
                    IntPtr calendar = SendMessage(picker.Handle, _getMonthCalendar, IntPtr.Zero, IntPtr.Zero);
                    Assert.AreNotEqual(IntPtr.Zero, calendar, "The actual native calendar must be open.");
                    Assert.IsTrue(IsWindowVisible(calendar));
                    Assert.AreEqual(IntPtr.Zero, GetWindowTheme(calendar), "The popup must accept its custom calendar colors.");
                    using (Bitmap popup = RenderWindow(calendar))
                    {
                        SaveImage(popup, "date-calendar-dark-" + opening, TestContext);
                        AssertDarkSurface(popup);
                    }

                    UiTheme.SetDarkMode(false);
                    WinFormsApplication.DoEvents();
                    Assert.AreEqual(calendar, SendMessage(picker.Handle, _getMonthCalendar, IntPtr.Zero, IntPtr.Zero));
                    using (Bitmap popup = RenderWindow(calendar))
                    {
                        SaveImage(popup, "date-calendar-light", TestContext);
                        AssertLightSurface(popup);
                    }

                    UiTheme.SetDarkMode(true);
                    WinFormsApplication.DoEvents();
                    _ = SendMessage(picker.Handle, _closeMonthCalendar, IntPtr.Zero, IntPtr.Zero);
                    WinFormsApplication.DoEvents();
                    Assert.AreEqual(IntPtr.Zero, SendMessage(picker.Handle, _getMonthCalendar, IntPtr.Zero, IntPtr.Zero));
                }

                Assert.AreEqual(originalHandle, picker.Handle);
                Assert.AreEqual(originalValue, picker.Value);
                Assert.IsTrue(picker.Checked);
                Assert.AreEqual(0, changes, "Appearance and opening the calendar must not change the filter value.");
                Assert.AreEqual(2, opened);
                Assert.AreEqual(2, closed);
                picker.Checked = false;
                UiTheme.SetDarkMode(false);
                UiTheme.SetDarkMode(true);
                Assert.IsFalse(picker.Checked, "Theme changes must retain an optional filter's unchecked state.");
                Capture(host, "date-controls-dark", TestContext);
                host.Close();
            });
        }

        [TestMethod]
        public void DarkDateAndTimeFieldsRetainNativeKeyboardEditing()
        {
            RunOnUiThread(() =>
            {
                UiTheme.SetDarkMode(true);
                using ThemedForm host = new() { ClientSize = new Size(600, 240) };
                DateTimePicker date = new() { Format = DateTimePickerFormat.Short, Location = new Point(24, 40), Width = 180, Value = new DateTime(2026, 11, 27) };
                DateTimePicker time = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm:ss", ShowUpDown = true, Location = new Point(24, 90), Width = 180, Value = new DateTime(2026, 11, 27, 10, 30, 0) };
                host.Controls.AddRange([date, time]);
                host.Show();
                WinFormsApplication.DoEvents();

                foreach (DateTimePicker picker in new[] { date, time })
                {
                    DateTime previous = picker.Value;
                    IntPtr handle = picker.Handle;
                    picker.Focus();
                    _ = SendMessage(handle, 0x0100, new IntPtr((int)Keys.Up), IntPtr.Zero);
                    _ = SendMessage(handle, 0x0101, new IntPtr((int)Keys.Up), IntPtr.Zero);
                    WinFormsApplication.DoEvents();
                    Assert.AreNotEqual(previous, picker.Value, "The native selected date/time segment must remain editable.");
                    Assert.AreEqual(handle, picker.Handle);
                    using Bitmap field = RenderWindow(handle);
                    SaveImage(field, picker == date ? "date-keyboard-dark" : "time-keyboard-dark", TestContext);
                    AssertDarkSurface(field);
                    AssertFieldHasGlyphs(field);
                }

                host.Close();
            });
        }

        private static Bitmap RenderWindow(IntPtr window)
        {
            Assert.IsTrue(GetWindowRect(window, out NativeRectangle bounds));
            Bitmap image = new(bounds.Right - bounds.Left, bounds.Bottom - bounds.Top, PixelFormat.Format24bppRgb);
            using Graphics graphics = Graphics.FromImage(image);
            graphics.Clear(Color.Magenta);
            IntPtr device = graphics.GetHdc();

            try
            {
                Assert.IsTrue(PrintWindow(window, device, 0), "The native control must render into its preview image.");
            }
            finally
            {
                graphics.ReleaseHdc(device);
            }

            return image;
        }

        private static void AssertDarkSurface(Bitmap image)
        {
            int dark = 0;
            int total = 0;

            for (int y = 3; y < image.Height - 3; y++)
            {
                for (int x = 3; x < image.Width - 3; x++)
                {
                    Color color = image.GetPixel(x, y);
                    total++;

                    if (color.R < 120 && color.G < 120 && color.B < 120 && Math.Abs(color.R - color.B) < 5 && Math.Abs(color.G - color.B) < 5)
                    {
                        dark++;
                    }
                }
            }

            Assert.IsGreaterThan(0.65D, (double)dark / total, "The rendered control must have a dark neutral background, not a white native field or popup.");
        }

        private static void AssertLightSurface(Bitmap image)
        {
            int light = 0;
            int total = 0;

            for (int y = 3; y < image.Height - 3; y++)
            {
                for (int x = 3; x < image.Width - 3; x++)
                {
                    Color color = image.GetPixel(x, y);
                    total++;

                    if (color.R > 180 && color.G > 180 && color.B > 180)
                    {
                        light++;
                    }
                }
            }

            Assert.IsGreaterThan(0.65D, (double)light / total, "The open popup must return to a light background when the theme changes.");
        }

        private static void AssertFieldHasGlyphs(Bitmap image)
        {
            int glyphPixels = 0;

            for (int y = Math.Max(3, image.Height / 4); y < image.Height * 3 / 4; y++)
            {
                for (int x = 28; x < image.Width - 28; x++)
                {
                    Color pixel = image.GetPixel(x, y);

                    if (pixel.R > 180 && pixel.G > 180 && pixel.B > 180)
                    {
                        glyphPixels++;
                    }
                }
            }

            Assert.IsGreaterThan(12, glyphPixels, "The dark field must render readable date/time glyphs, not only a dark rectangle.");
        }

        [DllImport("user32.dll", EntryPoint = "SendMessageW", ExactSpelling = true)]
        private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr window, out NativeRectangle rectangle);

        [DllImport("user32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PrintWindow(IntPtr window, IntPtr device, uint flags);

        [DllImport("user32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("uxtheme.dll", ExactSpelling = true)]
        private static extern IntPtr GetWindowTheme(IntPtr window);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRectangle
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
