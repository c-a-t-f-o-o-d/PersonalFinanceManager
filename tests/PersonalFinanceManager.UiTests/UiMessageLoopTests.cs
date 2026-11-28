namespace PersonalFinanceManager.UiTests
{
    [TestClass]
    public sealed class UiMessageLoopTests
    {
        [TestMethod]
        public void DelayedContinuationsStayOnTheUiThreadAfterNestedMessagePumps()
        {
            UiTestSupport.RunOnUiThread(() =>
            {
                int uiThread = Environment.CurrentManagedThreadId;
                System.Windows.Forms.Application.DoEvents();
                UiTestSupport.PumpUntil(() => true, "The initial message pump did not complete.");
                Assert.IsTrue(System.Windows.Forms.Application.MessageLoop);
                Assert.IsInstanceOfType<WindowsFormsSynchronizationContext>(SynchronizationContext.Current);
                TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
                int continuationThread = 0;

                async Task ContinueOnUiThreadAsync()
                {
                    await completion.Task.ConfigureAwait(true);
                    continuationThread = Environment.CurrentManagedThreadId;
                    Assert.IsInstanceOfType<WindowsFormsSynchronizationContext>(SynchronizationContext.Current);
                }

                Task continuation = ContinueOnUiThreadAsync();
                Task.Run(() => completion.SetResult()).GetAwaiter().GetResult();
                UiTestSupport.PumpUntil(() => continuation.IsCompleted, "The delayed continuation did not return to the UI thread.");
                continuation.GetAwaiter().GetResult();
                Assert.AreEqual(uiThread, continuationThread);
                Assert.IsTrue(Control.CheckForIllegalCrossThreadCalls);
            });
        }
    }
}
