using PersonalFinanceManager.App.UI.Controls;

namespace PersonalFinanceManager.UiTests
{
    [TestClass]
    public sealed class AsyncPageControlTests
    {
        [TestMethod]
        public void SupersededReadCannotReplaceNewerResultsOrClearAnotherPendingLoad()
        {
            UiTestSupport.RunOnUiThread(() =>
            {
                using Form host = new();
                using TestPage page = new() { Dock = DockStyle.Fill };
                host.Controls.Add(page);
                host.Show();
                TaskCompletionSource<int> first = new(TaskCreationOptions.RunContinuationsAsynchronously);
                TaskCompletionSource<int> second = new(TaskCreationOptions.RunContinuationsAsynchronously);
                TaskCompletionSource<int> accounts = new(TaskCreationOptions.RunContinuationsAsynchronously);
                CancellationToken firstToken = default;
                int result = 0;
                Task firstLoad = page.LoadDataAsync("rows", token =>
                {
                    firstToken = token;
                    return first.Task;
                }, value => result = value);
                Task accountLoad = page.LoadDataAsync("accounts", _ => accounts.Task, _ => { });
                Task secondLoad = page.LoadDataAsync("rows", _ => second.Task, value => result = value);

                Assert.IsTrue(firstToken.IsCancellationRequested);
                second.SetResult(2);
                UiTestSupport.PumpUntil(() => secondLoad.IsCompleted, "The newer request did not complete.");
                secondLoad.GetAwaiter().GetResult();
                first.SetResult(1);
                UiTestSupport.PumpUntil(() => firstLoad.IsCompleted, "The superseded request did not complete.");
                firstLoad.GetAwaiter().GetResult();
                Assert.AreEqual(2, result);
                Assert.IsTrue(page.Status.Visible);
                StringAssert.Contains(page.Status.Text, "accounts");

                accounts.SetResult(3);
                UiTestSupport.PumpUntil(() => accountLoad.IsCompleted, "The independent account request did not complete.");
                accountLoad.GetAwaiter().GetResult();
                Assert.IsFalse(page.Status.Visible);
            });
        }

        [TestMethod]
        public void BindingAResultCannotClearANewRequestStartedByTheBindingCallback()
        {
            UiTestSupport.RunOnUiThread(() =>
            {
                using Form host = new();
                using TestPage page = new() { Dock = DockStyle.Fill };
                host.Controls.Add(page);
                host.Show();
                TaskCompletionSource<int> next = new(TaskCreationOptions.RunContinuationsAsynchronously);
                Task? nextLoad = null;
                int result = 0;
                Task load = page.LoadDataAsync("rows", _ => Task.FromResult(1), value =>
                {
                    result = value;
                    nextLoad = page.LoadDataAsync("rows", _ => next.Task, newer => result = newer);
                });
                load.GetAwaiter().GetResult();
                Assert.IsNotNull(nextLoad);
                Assert.IsTrue(page.Status.Visible, "The binding callback's pending request must retain its loading status.");
                next.SetResult(2);
                UiTestSupport.PumpUntil(() => nextLoad.IsCompleted, "The request started during binding did not finish.");
                nextLoad.GetAwaiter().GetResult();
                Assert.AreEqual(2, result);
                Assert.IsFalse(page.Status.Visible);
            });
        }

        [TestMethod]
        public void DisposingPageCancelsReadsAndIgnoresLateSuccessfulCompletion()
        {
            UiTestSupport.RunOnUiThread(() =>
            {
                using Form host = new();
                TestPage page = new() { Dock = DockStyle.Fill };
                host.Controls.Add(page);
                host.Show();
                TaskCompletionSource<int> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
                CancellationToken token = default;
                bool applied = false;
                Task load = page.LoadDataAsync("rows", requestToken =>
                {
                    token = requestToken;
                    return completion.Task;
                }, _ => applied = true);

                page.Dispose();
                Assert.IsTrue(token.IsCancellationRequested);
                completion.SetResult(1);
                UiTestSupport.PumpUntil(() => load.IsCompleted, "The abandoned read did not finish.");
                load.GetAwaiter().GetResult();
                Assert.IsFalse(applied);
            });
        }

        [TestMethod]
        public void FailedReadRemainsObservableAndSuccessfulRetryClearsErrorStatus()
        {
            UiTestSupport.RunOnUiThread(() =>
            {
                using Form host = new();
                using TestPage page = new() { Dock = DockStyle.Fill };
                host.Controls.Add(page);
                host.Show();
                TaskCompletionSource<int> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
                InvalidOperationException expected = new("The data source is unavailable.");
                Task load = page.LoadDataAsync("rows", _ => completion.Task, _ => Assert.Fail("Failed reads must not bind results."));
                completion.SetException(expected);
                UiTestSupport.PumpUntil(() => load.IsCompleted, "The failed read did not finish.");
                Assert.IsTrue(load.IsFaulted);
                Assert.AreSame(expected, load.Exception!.InnerException);
                Assert.IsTrue(page.Status.Visible);
                StringAssert.Contains(page.Status.Text, "could not be loaded");

                int result = 0;
                Task retry = page.LoadDataAsync("rows", _ => Task.FromResult(7), value => result = value);
                UiTestSupport.PumpUntil(() => retry.IsCompleted, "The retry did not complete.");
                retry.GetAwaiter().GetResult();
                Assert.AreEqual(7, result);
                Assert.IsFalse(page.Status.Visible);
            });
        }

        private sealed class TestPage : AsyncPageControl
        {
            public Label Status => Controls.OfType<Label>().Single(control => control.Name == "PageLoadStatus");

            public Task LoadDataAsync(string key, Func<CancellationToken, Task<int>> load, Action<int> apply)
            {
                return LoadLatestAsync(key, $"Loading {key}...", load, apply);
            }
        }
    }
}
