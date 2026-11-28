using System.Drawing.Imaging;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using PersonalFinanceManager.App.UI.Styling;
using WinFormsApplication = System.Windows.Forms.Application;

namespace PersonalFinanceManager.UiTests
{
    internal static class UiTestSupport
    {
        public static void PumpUntil(Func<bool> completed, string message, int timeoutMilliseconds = 5000)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            while (!completed() && stopwatch.ElapsedMilliseconds < timeoutMilliseconds)
            {
                WinFormsApplication.DoEvents();
                Thread.Sleep(1);
            }

            WinFormsApplication.DoEvents();
            Assert.IsTrue(completed(), message);
        }

        public static void RunOnUiThread(Action action)
        {
            Exception? failure = null;
            Thread thread = new(() =>
            {
                try
                {
                    WinFormsApplication.EnableVisualStyles();
                    action();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
                finally
                {
                    try
                    {
                        UiTheme.SetDarkMode(false);
                    }
                    catch (Exception ex)
                    {
                        failure ??= ex;
                    }
                }
            }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(30)), "The UI operation did not complete within 30 seconds.");

            if (failure is not null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }

        public static IEnumerable<Control> Descendants(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                yield return child;

                foreach (Control descendant in Descendants(child))
                {
                    yield return descendant;
                }
            }
        }

        public static void Capture(Control control, string name, TestContext context)
        {
            using Bitmap image = new(control.Width, control.Height, PixelFormat.Format24bppRgb);
            control.DrawToBitmap(image, new Rectangle(Point.Empty, control.Size));
            SaveImage(image, name, context);
        }

        public static void SaveImage(Bitmap image, string name, TestContext context)
        {
            string? configuredDirectory = Environment.GetEnvironmentVariable("PFM_UI_PREVIEW_DIRECTORY");
            string directory = string.IsNullOrWhiteSpace(configuredDirectory)
                ? Path.Combine(AppContext.BaseDirectory, "TestResults", "Screenshots")
                : Path.GetFullPath(configuredDirectory);
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, name + ".png");
            image.Save(path, ImageFormat.Png);
            context.AddResultFile(path);
        }
    }
}
