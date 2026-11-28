using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using PersonalFinanceManager.App.UI.Styling;
using static PersonalFinanceManager.UiTests.UiTestSupport;
using WinFormsApplication = System.Windows.Forms.Application;

namespace PersonalFinanceManager.UiTests
{
    [TestClass]
    public sealed class DatePickerThemeTests
    {
        private const uint _getMonthCalendar = 0x1008;
        private const uint _getPickerInfo = 0x100E;
        private const uint _closeMonthCalendar = 0x100D;
        private const uint _getMinimumCalendarRectangle = 0x1009;
        private const uint _getCalendarColor = 0x100B;
        private const uint _hitTestCalendar = 0x100E;
        private const uint _getMaximumTodayWidth = 0x1015;
        private const uint _calendarDate = 0x00020001;
        private const uint _calendarWeekday = 0x00020002;
        private const uint _calendarTodayLink = 0x00030000;

        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void NativeDateFieldAndReopenedCalendarRenderInTheCurrentTheme(bool startDark)
        {
            RunOnUiThread(() =>
            {
                UiTheme.SetDarkMode(startDark);
                string capturePrefix = startDark ? "startup-" : string.Empty;
                using ThemedForm host = new()
                {
                    Text = "Date filter preview",
                    ClientSize = new Size(600, 320),
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(32, 32),
                    TopMost = true
                };
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
                host.Activate();
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
                using (Bitmap liveField = CaptureWindowFromScreen(picker.Handle))
                {
                    SaveImage(liveField, capturePrefix + "date-field-live-dark", TestContext);
                    AssertDarkSurface(liveField);
                    AssertFieldHasGlyphs(liveField);
                }

                using (Bitmap field = RenderWindow(picker.Handle))
                {
                    SaveImage(field, capturePrefix + "date-field-dark", TestContext);
                    AssertDarkSurface(field);
                    AssertFieldHasGlyphs(field);
                }

                for (int opening = 0; opening < 2; opening++)
                {
                    picker.Focus();

                    if (opening == 0)
                    {
                        picker.AccessibilityObject.DoDefaultAction();
                    }
                    else
                    {
                        ClickCalendarButton(picker);
                    }

                    WinFormsApplication.DoEvents();
                    IntPtr calendar = SendMessage(picker.Handle, _getMonthCalendar, IntPtr.Zero, IntPtr.Zero);
                    Assert.AreNotEqual(IntPtr.Zero, calendar, "The actual native calendar must be open.");
                    Assert.IsTrue(IsWindowVisible(calendar));
                    Assert.AreEqual(IntPtr.Zero, GetWindowTheme(calendar), "The popup must accept its custom calendar colors.");
                    RecordNativeWindows(picker, calendar);
                    AssertCalendarVisibleWithinAncestors(calendar);
                    using (Bitmap livePopup = CaptureWindowFromScreen(calendar))
                    {
                        SaveImage(livePopup, capturePrefix + "date-calendar-live-dark-" + opening, TestContext);
                        AssertDarkSurface(livePopup);
                        AssertCalendarLayoutAndText(calendar, livePopup, dark: true);
                    }

                    using (Bitmap popup = RenderWindow(calendar))
                    {
                        SaveImage(popup, capturePrefix + "date-calendar-dark-" + opening, TestContext);
                        AssertDarkSurface(popup);
                        AssertCalendarLayoutAndText(calendar, popup, dark: true);
                    }

                    UiTheme.SetDarkMode(false);
                    WinFormsApplication.DoEvents();
                    Assert.AreEqual(calendar, SendMessage(picker.Handle, _getMonthCalendar, IntPtr.Zero, IntPtr.Zero));
                    AssertCalendarVisibleWithinAncestors(calendar);
                    using (Bitmap livePopup = CaptureWindowFromScreen(calendar))
                    {
                        SaveImage(livePopup, capturePrefix + "date-calendar-live-light", TestContext);
                        AssertLightSurface(livePopup);
                        AssertCalendarLayoutAndText(calendar, livePopup, dark: false);
                    }

                    using (Bitmap popup = RenderWindow(calendar))
                    {
                        SaveImage(popup, capturePrefix + "date-calendar-light", TestContext);
                        AssertLightSurface(popup);
                        AssertCalendarLayoutAndText(calendar, popup, dark: false);
                    }

                    UiTheme.SetDarkMode(true);
                    WinFormsApplication.DoEvents();
                    AssertCalendarVisibleWithinAncestors(calendar);
                    using (Bitmap livePopup = CaptureWindowFromScreen(calendar))
                    {
                        SaveImage(livePopup, capturePrefix + "date-calendar-live-returned-dark", TestContext);
                        AssertDarkSurface(livePopup);
                        AssertCalendarLayoutAndText(calendar, livePopup, dark: true);
                    }

                    using (Bitmap popup = RenderWindow(calendar))
                    {
                        SaveImage(popup, capturePrefix + "date-calendar-returned-dark", TestContext);
                        AssertDarkSurface(popup);
                        AssertCalendarLayoutAndText(calendar, popup, dark: true);
                    }

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
                Capture(host, capturePrefix + "date-controls-dark", TestContext);
                host.Close();
            });
        }

        [TestMethod]
        public void DarkDateAndTimeFieldsRetainNativeKeyboardEditing()
        {
            RunOnUiThread(() =>
            {
                UiTheme.SetDarkMode(true);
                using ThemedForm host = new() { ClientSize = new Size(600, 240), StartPosition = FormStartPosition.Manual, Location = new Point(32, 32), TopMost = true };
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
                    using (Bitmap liveField = CaptureWindowFromScreen(handle))
                    {
                        SaveImage(liveField, picker == date ? "date-keyboard-live-dark" : "time-keyboard-live-dark", TestContext);
                        AssertDarkSurface(liveField);
                        AssertFieldHasGlyphs(liveField);
                    }

                    using Bitmap field = RenderWindow(handle);
                    SaveImage(field, picker == date ? "date-keyboard-dark" : "time-keyboard-dark", TestContext);
                    AssertDarkSurface(field);
                    AssertFieldHasGlyphs(field);
                }

                host.Close();
            });
        }

        private static void ClickCalendarButton(DateTimePicker picker)
        {
            DateTimePickerInfo info = new() { Size = (uint)Marshal.SizeOf<DateTimePickerInfo>() };
            _ = SendMessage(picker.Handle, _getPickerInfo, IntPtr.Zero, ref info);
            int x = (info.ButtonBounds.Left + info.ButtonBounds.Right) / 2;
            int y = (info.ButtonBounds.Top + info.ButtonBounds.Bottom) / 2;
            IntPtr location = new((y << 16) | (x & 0xFFFF));
            Assert.IsTrue(PostMessage(picker.Handle, 0x0201, new IntPtr(1), location));
            Assert.IsTrue(PostMessage(picker.Handle, 0x0202, IntPtr.Zero, location));
        }

        private void RecordNativeWindows(DateTimePicker picker, IntPtr calendar)
        {
            DateTimePickerInfo info = new() { Size = (uint)Marshal.SizeOf<DateTimePickerInfo>() };
            _ = SendMessage(picker.Handle, _getPickerInfo, IntPtr.Zero, ref info);
            RecordNativeWindow("date picker", picker.Handle);
            RecordNativeWindow("edit field", info.Edit);
            RecordNativeWindow("spinner", info.UpDown);
            RecordNativeWindow("dropdown", info.DropDown);
            RecordNativeWindow("calendar", calendar);
            RecordNativeWindow("calendar parent", GetParent(calendar));
        }

        private void RecordNativeWindow(string description, IntPtr window)
        {
            if (window == IntPtr.Zero)
            {
                TestContext.WriteLine($"Native {description}: no window.");
                return;
            }

            StringBuilder className = new(128);
            _ = GetClassName(window, className, className.Capacity);
            _ = GetWindowRect(window, out NativeRectangle bounds);
            TestContext.WriteLine($"Native {description}: HWND={window.ToInt64():X}, class={className}, parent={GetParent(window).ToInt64():X}, style={GetWindowLong(window, -16):X8}, extendedStyle={GetWindowLong(window, -20):X8}, bounds=({bounds.Left},{bounds.Top})-({bounds.Right},{bounds.Bottom}).");
        }

        private static void AssertCalendarVisibleWithinAncestors(IntPtr calendar)
        {
            Rectangle calendarClient = GetScreenClientBounds(calendar);
            IntPtr current = calendar;

            while ((GetWindowLong(current, -16) & 0x40000000) != 0)
            {
                IntPtr parent = GetParent(current);
                Assert.AreNotEqual(IntPtr.Zero, parent, "A native child calendar must have a visible parent.");
                Assert.IsTrue(IsWindowVisible(parent));
                Rectangle parentClient = GetScreenClientBounds(parent);
                Assert.IsTrue(parentClient.Contains(calendarClient), $"Calendar client {calendarClient} must be inside its native parent client {parentClient}; an offscreen child can still pass PrintWindow checks.");
                current = parent;
            }

            Assert.IsTrue(GetWindowRect(current, out NativeRectangle popup));
            Rectangle popupBounds = Rectangle.FromLTRB(popup.Left, popup.Top, popup.Right, popup.Bottom);
            Assert.IsTrue(Screen.FromHandle(current).WorkingArea.Contains(popupBounds), "The native popup must remain inside its monitor's working area.");
        }

        private static Rectangle GetScreenClientBounds(IntPtr window)
        {
            Assert.IsTrue(GetClientRect(window, out NativeRectangle client));
            NativePoint origin = default;
            Assert.IsTrue(ClientToScreen(window, ref origin));
            return new Rectangle(origin.X, origin.Y, client.Right - client.Left, client.Bottom - client.Top);
        }

        private static Bitmap CaptureWindowFromScreen(IntPtr window)
        {
            Assert.IsTrue(IsWindowVisible(window));
            WinFormsApplication.DoEvents();
            _ = DwmFlush();
            Assert.IsTrue(GetWindowRect(window, out NativeRectangle bounds));
            Rectangle source = Rectangle.FromLTRB(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
            Assert.IsTrue(SystemInformation.VirtualScreen.Contains(source), "Live rendering checks require the complete native control to be on the desktop.");
            Bitmap image = new(source.Width, source.Height, PixelFormat.Format24bppRgb);

            try
            {
                using Graphics graphics = Graphics.FromImage(image);
                graphics.CopyFromScreen(source.Location, Point.Empty, source.Size, CopyPixelOperation.SourceCopy);
                return image;
            }
            catch
            {
                image.Dispose();
                throw;
            }
        }

        private static void AssertCalendarLayoutAndText(IntPtr calendar, Bitmap image, bool dark)
        {
            NativeRectangle minimum = default;
            Assert.AreNotEqual(IntPtr.Zero, SendMessage(calendar, _getMinimumCalendarRectangle, IntPtr.Zero, ref minimum));
            Assert.IsGreaterThanOrEqualTo(minimum.Right, image.Width, "The popup must fit the calendar's current native width.");
            Assert.IsGreaterThanOrEqualTo(minimum.Bottom, image.Height, "The popup must fit the full month and Today footer after its visual styles change.");
            int todayWidth = (int)SendMessage(calendar, _getMaximumTodayWidth, IntPtr.Zero, IntPtr.Zero);
            Assert.IsGreaterThanOrEqualTo(todayWidth, image.Width, "The localized Today label must fit within the popup.");

            if (dark)
            {
                Color weekday = ColorTranslator.FromWin32((int)SendMessage(calendar, _getCalendarColor, new IntPtr(2), IntPtr.Zero));
                Color title = ColorTranslator.FromWin32((int)SendMessage(calendar, _getCalendarColor, new IntPtr(3), IntPtr.Zero));
                Color month = ColorTranslator.FromWin32((int)SendMessage(calendar, _getCalendarColor, new IntPtr(4), IntPtr.Zero));
                Assert.IsGreaterThanOrEqualTo(4.5D, Contrast(weekday, month), "Classic weekday text shares the title background color and must contrast with the month background.");
                Assert.IsGreaterThanOrEqualTo(4.5D, Contrast(title, weekday), "The month title must remain readable on its title background.");
            }

            Color background = dark ? UiTheme.InputBackground : SystemColors.Window;
            AssertCalendarPartHasText(calendar, image, _calendarWeekday, background, "weekday headings");
            AssertCalendarPartHasText(calendar, image, _calendarDate, background, "day numbers");
            AssertCalendarPartHasText(calendar, image, _calendarTodayLink, background, "Today footer");
        }

        private static void AssertCalendarPartHasText(IntPtr calendar, Bitmap image, uint part, Color background, string description)
        {
            Assert.IsTrue(GetClientRect(calendar, out NativeRectangle client));
            Assert.IsTrue(GetWindowRect(calendar, out NativeRectangle window));
            NativePoint origin = default;
            Assert.IsTrue(ClientToScreen(calendar, ref origin));
            int top = -1;
            int bottom = -1;

            for (int y = client.Top; y < client.Bottom; y++)
            {
                CalendarHitTestInfo hit = new()
                {
                    Size = (uint)Marshal.SizeOf<CalendarHitTestInfo>(),
                    Point = new NativePoint { X = client.Right / 2, Y = y }
                };
                _ = SendMessage(calendar, _hitTestCalendar, IntPtr.Zero, ref hit);

                if (hit.Hit == part)
                {
                    top = top < 0 ? y : top;
                    bottom = y + 1;
                }
            }

            Assert.IsGreaterThanOrEqualTo(0, top, $"The native calendar must expose its {description} within the popup.");
            Assert.IsGreaterThan(6, bottom - top, $"The native {description} must not be clipped to a partial text row.");
            int glyphPixels = 0;

            for (int y = top + 2; y < bottom - 2; y++)
            {
                for (int x = client.Right / 2; x < client.Right - 3; x++)
                {
                    Color pixel = image.GetPixel(x + origin.X - window.Left, y + origin.Y - window.Top);

                    if (Contrast(pixel, background) >= 4.5D)
                    {
                        glyphPixels++;
                    }
                }
            }

            Assert.IsGreaterThan(12, glyphPixels, $"The rendered {description} must contain readable text on its background.");
        }

        private static double Contrast(Color first, Color second)
        {
            static double Luminance(Color color)
            {
                static double Linear(byte channel)
                {
                    double value = channel / 255D;
                    return value <= 0.04045D ? value / 12.92D : Math.Pow((value + 0.055D) / 1.055D, 2.4D);
                }

                return (0.2126D * Linear(color.R)) + (0.7152D * Linear(color.G)) + (0.0722D * Linear(color.B));
            }

            double firstLuminance = Luminance(first);
            double secondLuminance = Luminance(second);
            return (Math.Max(firstLuminance, secondLuminance) + 0.05D) / (Math.Min(firstLuminance, secondLuminance) + 0.05D);
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

        [DllImport("user32.dll", EntryPoint = "SendMessageW", ExactSpelling = true)]
        private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, ref NativeRectangle rectangle);

        [DllImport("user32.dll", EntryPoint = "SendMessageW", ExactSpelling = true)]
        private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, ref CalendarHitTestInfo hitTest);

        [DllImport("user32.dll", EntryPoint = "SendMessageW", ExactSpelling = true)]
        private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, ref DateTimePickerInfo info);

        [DllImport("user32.dll", EntryPoint = "PostMessageW", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW", ExactSpelling = true)]
        private static extern int GetWindowLong(IntPtr window, int index);

        [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int GetClassName(IntPtr window, StringBuilder className, int capacity);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr GetParent(IntPtr window);

        [DllImport("dwmapi.dll", ExactSpelling = true)]
        private static extern int DwmFlush();

        [DllImport("user32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetClientRect(IntPtr window, out NativeRectangle rectangle);

        [DllImport("user32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ClientToScreen(IntPtr window, ref NativePoint point);

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
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DateTimePickerInfo
        {
            public uint Size;
            public NativeRectangle CheckBounds;
            public uint CheckState;
            public NativeRectangle ButtonBounds;
            public uint ButtonState;
            public IntPtr Edit;
            public IntPtr UpDown;
            public IntPtr DropDown;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeSystemTime
        {
            public ushort Year;
            public ushort Month;
            public ushort DayOfWeek;
            public ushort Day;
            public ushort Hour;
            public ushort Minute;
            public ushort Second;
            public ushort Milliseconds;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CalendarHitTestInfo
        {
            public uint Size;
            public NativePoint Point;
            public uint Hit;
            public NativeSystemTime Date;
            public NativeRectangle Bounds;
            public int Offset;
            public int Row;
            public int Column;
        }

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
