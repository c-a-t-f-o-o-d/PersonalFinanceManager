using System.Runtime.InteropServices;
using PersonalFinanceManager.App.UI;
using PersonalFinanceManager.App.UI.Controls;
using PersonalFinanceManager.App.UI.Styling;
using PersonalFinanceManager.App.UI.Views;
using PersonalFinanceManager.Data.Configuration;
using static PersonalFinanceManager.UiTests.UiTestSupport;
using WinFormsApplication = System.Windows.Forms.Application;

namespace PersonalFinanceManager.UiTests
{
    [TestClass]
    public sealed class PageScrollingTests
    {
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        public void TransactionsUseOnlyTheGridScrollbarAtNormalAndSmallSizes()
        {
            RunOnUiThread(() =>
            {
                UiTheme.SetDarkMode(true);
                using ThemedForm host = new() { Text = "Transactions", ClientSize = new Size(1400, 820) };
                TransactionsView transactions = new(new PreviewTransactionService());
                PageHost page = new(transactions);
                host.Controls.Add(page);
                host.Show();
                WinFormsApplication.DoEvents();
                DataGridView grid = Descendants(transactions).OfType<DataGridView>().Single();
                Assert.AreEqual(180, grid.RowCount);

                foreach (Size size in new[] { new Size(1400, 820), new Size(950, 600), new Size(750, 500) })
                {
                    host.ClientSize = size;
                    WinFormsApplication.DoEvents();
                    Assert.AreEqual(PageScrollMode.Viewport, page.ScrollMode);
                    Assert.IsFalse(page.VerticalScroll.Visible);
                    Assert.IsFalse(page.HorizontalScroll.Visible);
                    Assert.AreEqual(1, VerticalScrollOwners(page).Count(), "The transaction grid must be the page's sole vertical scroll owner.");
                    Assert.IsTrue(grid.DisplayedRowCount(false) >= 2, "Resizing must leave usable transaction rows visible.");

                    foreach (Button button in Descendants(transactions).OfType<Button>().Where(button => button.Visible))
                    {
                        Rectangle screenBounds = button.RectangleToScreen(button.ClientRectangle);

                        for (Control? parent = button.Parent; parent is not null && parent != page; parent = parent.Parent)
                        {
                            Rectangle bounds = parent.RectangleToClient(screenBounds);
                            Assert.IsTrue(parent.ClientRectangle.Contains(bounds), $"Action '{button.Text}' is clipped by {parent.GetType().Name} at {size}.");
                        }
                    }

                    grid.FirstDisplayedScrollingRowIndex = 30;
                    Assert.AreEqual(30, grid.FirstDisplayedScrollingRowIndex);
                    Assert.AreEqual(Point.Empty, page.AutoScrollPosition);
                    grid.FirstDisplayedScrollingRowIndex = 0;
                }

                host.ClientSize = new Size(1400, 820);
                WinFormsApplication.DoEvents();
                Capture(host, "transactions-dark", TestContext);
                UiTheme.SetDarkMode(false);
                WinFormsApplication.DoEvents();
                Capture(host, "transactions-light", TestContext);
                Assert.IsFalse(page.VerticalScroll.Visible);
                host.Close();
            });
        }

        [TestMethod]
        public void ReportTabsDoNotCreateNestedPageAndTableScrolling()
        {
            RunOnUiThread(() =>
            {
                UiTheme.SetDarkMode(true);
                using ThemedForm host = new() { Text = "Expense analysis", ClientSize = new Size(1100, 680) };
                ReportsView report = new(new PreviewAnalysisService());
                PageHost page = new(report);
                host.Controls.Add(page);
                host.Show();
                WinFormsApplication.DoEvents();
                Assert.IsFalse(page.VerticalScroll.Visible);

                foreach (TabControl tabs in Descendants(report).OfType<TabControl>())
                {
                    for (int index = 0; index < tabs.TabPages.Count; index++)
                    {
                        tabs.SelectedIndex = index;
                        WinFormsApplication.DoEvents();
                        Assert.IsFalse(page.VerticalScroll.Visible);
                        Assert.IsTrue(VerticalScrollOwners(page).Count() <= 1, $"Report tab '{tabs.SelectedTab!.Text}' has competing vertical scrollbars.");
                        Capture(host, "expense-tab-" + index, TestContext);
                    }
                }

                host.Close();
            });
        }

        [TestMethod]
        public void SettingsHaveOnePageScrollbarAndReachableSaveAction()
        {
            RunOnUiThread(() =>
            {
                string directory = Path.Combine(Path.GetTempPath(), "PFM_UiTests_" + Guid.NewGuid().ToString("N"));
                SettingsService settings = new(directory);
                using ThemedForm host = new() { ClientSize = new Size(800, 550) };
                SettingsView view = new(settings, new AppSettings(), () => Task.CompletedTask);
                PageHost page = new(view);
                host.Controls.Add(page);
                host.Show();
                WinFormsApplication.DoEvents();
                Assert.AreEqual(PageScrollMode.Page, page.ScrollMode);
                Assert.IsTrue(page.VerticalScroll.Visible);
                Assert.IsFalse(page.HorizontalScroll.Visible);
                Button save = Descendants(view).OfType<Button>().Single(button => button.Text == "Save settings");
                page.ScrollControlIntoView(save);
                WinFormsApplication.DoEvents();
                Rectangle visibleSave = page.RectangleToClient(save.RectangleToScreen(save.ClientRectangle));
                Assert.IsTrue(page.ClientRectangle.IntersectsWith(visibleSave), "The page scrollbar must reach the save action.");
                Assert.IsFalse(view.VerticalScroll.Visible);
                host.Close();
            });
        }

        [TestMethod]
        public void SalarySectionsKeepLongBucketListsWithinOneScrollArea()
        {
            RunOnUiThread(() =>
            {
                UiTheme.SetDarkMode(true);
                using ThemedForm host = new() { Text = "Salary planning", ClientSize = new Size(1050, 680) };
                SalaryAnalysisView salary = new(new PreviewAnalysisService { AdditionalScopeCount = 70 });
                PageHost page = new(salary);
                host.Controls.Add(page);
                host.Show();
                WinFormsApplication.DoEvents();
                TabControl sections = Descendants(salary).OfType<TabControl>().Single(tabs => tabs.Name == "SalarySections");

                foreach (Size size in new[] { new Size(1050, 680), new Size(750, 500) })
                {
                    host.ClientSize = size;

                    for (int index = 0; index < sections.TabPages.Count; index++)
                    {
                        sections.SelectedIndex = index;
                        WinFormsApplication.DoEvents();
                        Assert.IsFalse(page.VerticalScroll.Visible);
                        Assert.IsTrue(VerticalScrollOwners(page).Count() <= 1, $"Salary section '{sections.SelectedTab!.Text}' has competing vertical scrollbars at {size}.");
                        Capture(host, "salary-section-" + index + "-" + size.Width, TestContext);
                    }
                }

                host.Close();
            });
        }

        private static IEnumerable<Control> VerticalScrollOwners(Control page)
        {
            return Descendants(page).Prepend(page).Where(control => control.Visible && control.IsHandleCreated &&
                (control is VScrollBar || control is not TextBoxBase && (GetWindowLong(control.Handle, -16) & 0x00200000) != 0));
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW", ExactSpelling = true)]
        private static extern int GetWindowLong(IntPtr window, int index);
    }
}
