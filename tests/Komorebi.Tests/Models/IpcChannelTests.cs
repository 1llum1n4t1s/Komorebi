using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

using Komorebi.Models;

using Xunit;

namespace Komorebi.Tests.Models
{
    public class IpcChannelTests : IDisposable
    {
        private readonly string _tempDir;

        public IpcChannelTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"IpcTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, true); }
            catch { /* cleanup best-effort */ }
        }

        private string UniquePipeName() => $"IpcTest_{Guid.NewGuid():N}";
        private string LockPath() => Path.Combine(_tempDir, "process.lock");

        [Fact]
        public void FirstInstance_SetsIsFirstInstance()
        {
            var pipe = UniquePipeName();
            using var ipc = new IpcChannel(pipe, LockPath());
            Assert.True(ipc.IsFirstInstance);
        }

        [Fact]
        public void SecondInstance_IsNotFirstInstance()
        {
            var pipe = UniquePipeName();
            var lockFile = LockPath();

            using var first = new IpcChannel(pipe, lockFile);
            Assert.True(first.IsFirstInstance);

            // Same lock file → second instance cannot acquire it.
            using var second = new IpcChannel(pipe, lockFile);
            Assert.False(second.IsFirstInstance);
        }

        [Fact]
        public async Task SendMessage_IsReceived()
        {
            var pipe = UniquePipeName();
            using var ipc = new IpcChannel(pipe, LockPath());

            // Allow the server loop to start.
            await Task.Delay(100);

            var received = new TaskCompletionSource<string>();
            ipc.MessageReceived += msg => received.TrySetResult(msg);

            ipc.SendToFirstInstance("hello");

            var result = await Task.WhenAny(received.Task, Task.Delay(5000));
            Assert.Same(received.Task, result);
            Assert.Equal("hello", await received.Task);
        }

        [Fact]
        public async Task SendMultipleMessages_AllReceived()
        {
            var pipe = UniquePipeName();
            using var ipc = new IpcChannel(pipe, LockPath());
            await Task.Delay(100);

            var messages = new System.Collections.Concurrent.ConcurrentBag<string>();
            var count = new CountdownEvent(3);

            ipc.MessageReceived += msg =>
            {
                messages.Add(msg);
                count.Signal();
            };

            ipc.SendToFirstInstance("msg1");
            // Small delay between sends to let the server cycle.
            await Task.Delay(200);
            ipc.SendToFirstInstance("msg2");
            await Task.Delay(200);
            ipc.SendToFirstInstance("msg3");

            var signaled = count.Wait(TimeSpan.FromSeconds(10));
            Assert.True(signaled, $"Only received {3 - count.CurrentCount} of 3 messages");
            Assert.Contains("msg1", messages);
            Assert.Contains("msg2", messages);
            Assert.Contains("msg3", messages);
        }

        [Fact]
        public async Task Dispose_CompletesCleanly()
        {
            var pipe = UniquePipeName();
            var ipc = new IpcChannel(pipe, LockPath());
            Assert.True(ipc.IsFirstInstance);

            // Let the server loop start waiting for connections.
            await Task.Delay(200);

            // Dispose should unblock the server via dummy client and return quickly.
            var disposeTask = Task.Run(() => ipc.Dispose());
            var completed = await Task.WhenAny(disposeTask, Task.Delay(5000));
            Assert.Same(disposeTask, completed);

            // Dispose should not throw.
            await disposeTask;
        }

        [Fact]
        public async Task Dispose_ThenSend_DoesNotThrow()
        {
            var pipe = UniquePipeName();
            var ipc = new IpcChannel(pipe, LockPath());
            await Task.Delay(200);

            ipc.Dispose();
            await Task.Delay(100);

            // Sending to a disposed channel should silently fail, not throw.
            var ex = Record.Exception(() => ipc.SendToFirstInstance("after-dispose"));
            Assert.Null(ex);
        }

        [Fact]
        public async Task Dispose_NoExceptionOnThreadPool()
        {
            // Capture any unobserved exceptions on the thread pool.
            var unobserved = new TaskCompletionSource<Exception>();
            EventHandler<UnobservedTaskExceptionEventArgs> handler = (_, e) =>
            {
                unobserved.TrySetResult(e.Exception);
                e.SetObserved();
            };

            TaskScheduler.UnobservedTaskException += handler;
            try
            {
                var pipe = UniquePipeName();
                var ipc = new IpcChannel(pipe, LockPath());
                await Task.Delay(200);

                ipc.Dispose();
                await Task.Delay(500);

                // Force GC to finalize any tasks, which surfaces unobserved exceptions.
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                // Give the thread pool a moment to fire any unobserved events.
                await Task.Delay(200);

                // If no exception was caught, this times out (good).
                var result = await Task.WhenAny(unobserved.Task, Task.Delay(1000));
                if (result == unobserved.Task)
                    Assert.Fail($"Unobserved exception on thread pool: {await unobserved.Task}");
            }
            finally
            {
                TaskScheduler.UnobservedTaskException -= handler;
            }
        }

        [Fact]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            var pipe = UniquePipeName();
            var ipc = new IpcChannel(pipe, LockPath());

            var ex = Record.Exception(() =>
            {
                ipc.Dispose();
                ipc.Dispose();
                ipc.Dispose();
            });
            Assert.Null(ex);
        }

        [Fact]
        public async Task Dispose_ReleasesLockFile()
        {
            var pipe = UniquePipeName();
            var lockFile = LockPath();

            var ipc = new IpcChannel(pipe, lockFile);
            Assert.True(ipc.IsFirstInstance);
            await Task.Delay(100);

            ipc.Dispose();
            await Task.Delay(100);

            // After dispose, we should be able to acquire the lock again.
            using var newIpc = new IpcChannel(UniquePipeName(), lockFile);
            Assert.True(newIpc.IsFirstInstance);
        }

        [Fact]
        public async Task Dispose_DummyClientUnblocksServer()
        {
            // Verifies that the dummy client in Dispose() successfully connects
            // to unblock the server's WaitForConnectionAsync, even under rapid
            // create-dispose cycles (regression test for PipeOptions.CurrentUserOnly
            // causing ValidateRemotePipeUser failure).
            for (int i = 0; i < 5; i++)
            {
                var pipe = UniquePipeName();
                var ipc = new IpcChannel(pipe, LockPath());
                Assert.True(ipc.IsFirstInstance);

                // Let server start waiting.
                await Task.Delay(150);

                // Dispose must complete quickly (dummy client unblocks the server).
                var disposeTask = Task.Run(() => ipc.Dispose());
                var completed = await Task.WhenAny(disposeTask, Task.Delay(3000));
                Assert.Same(disposeTask, completed);
                await disposeTask; // must not throw
            }
        }

        [Fact]
        public async Task Dispose_ServerStopsAcceptingConnections()
        {
            var pipe = UniquePipeName();
            var ipc = new IpcChannel(pipe, LockPath());
            await Task.Delay(150);

            ipc.Dispose();
            await Task.Delay(200);

            // After dispose, connecting to the pipe should fail (server is gone).
            var ex = Record.Exception(() =>
            {
                using var client = new NamedPipeClientStream(".", pipe, PipeDirection.Out);
                client.Connect(500);
            });
            Assert.NotNull(ex);
        }

        [Fact]
        public void SecondInstance_SendDoesNotThrow()
        {
            var pipe = UniquePipeName();
            var lockFile = LockPath();

            using var first = new IpcChannel(pipe, lockFile);
            Assert.True(first.IsFirstInstance);

            using var second = new IpcChannel(pipe, lockFile);
            Assert.False(second.IsFirstInstance);

            // SendToFirstInstance must not throw even when this is not the first instance.
            var ex = Record.Exception(() => second.SendToFirstInstance("test"));
            Assert.Null(ex);
        }

        [Fact]
        public void SecondInstance_DisposeDoesNotThrow()
        {
            var pipe = UniquePipeName();
            var lockFile = LockPath();

            using var first = new IpcChannel(pipe, lockFile);
            var second = new IpcChannel(pipe, lockFile);
            Assert.False(second.IsFirstInstance);

            var ex = Record.Exception(() => second.Dispose());
            Assert.Null(ex);
        }

        [Fact]
        public void LockFileDirectoryDoesNotExist_DoesNotThrow()
        {
            var pipe = UniquePipeName();
            var lockFile = Path.Combine(_tempDir, "nonexistent", "subdir", "process.lock");

            var ex = Record.Exception(() =>
            {
                using var ipc = new IpcChannel(pipe, lockFile);
                Assert.True(ipc.IsFirstInstance);
            });
            Assert.Null(ex);
        }

        [Fact]
        public async Task Dispose_NoInvalidOperationException()
        {
            // Specifically tests that Dispose does not throw InvalidOperationException
            // (which was caused by PipeOptions.CurrentUserOnly triggering
            // ValidateRemotePipeUser/NativeObjectSecurity during shutdown).
            var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();
            EventHandler<UnobservedTaskExceptionEventArgs> handler = (_, e) =>
            {
                exceptions.Add(e.Exception);
                e.SetObserved();
            };

            TaskScheduler.UnobservedTaskException += handler;
            try
            {
                // Run several cycles to increase the chance of catching the race.
                for (int i = 0; i < 3; i++)
                {
                    var pipe = UniquePipeName();
                    var ipc = new IpcChannel(pipe, LockPath());
                    await Task.Delay(200);

                    ipc.Dispose();
                    await Task.Delay(300);
                }

                // Force GC to finalize tasks and surface any unobserved exceptions.
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                await Task.Delay(500);

                // No InvalidOperationException should have leaked.
                foreach (var ex in exceptions)
                {
                    var flat = ex is AggregateException agg ? agg.Flatten() : ex;
                    Assert.DoesNotContain("InvalidOperationException", flat.ToString());
                    Assert.DoesNotContain("ValidateRemotePipeUser", flat.ToString());
                }
            }
            finally
            {
                TaskScheduler.UnobservedTaskException -= handler;
            }
        }

        // --- Edge-case construction tests ---

        [Fact]
        public void InvalidLockFilePath_IsNotFirstInstance()
        {
            var pipe = UniquePipeName();
            using var ipc = new IpcChannel(pipe, "invalid\0path");
            Assert.False(ipc.IsFirstInstance);
        }

        [Fact]
        public void EmptyLockFilePath_IsNotFirstInstance()
        {
            var pipe = UniquePipeName();
            using var ipc = new IpcChannel(pipe, "");
            Assert.False(ipc.IsFirstInstance);
        }

        [Fact]
        public void NullLockFilePath_IsNotFirstInstance()
        {
            var pipe = UniquePipeName();
            using var ipc = new IpcChannel(pipe, null);
            Assert.False(ipc.IsFirstInstance);
        }

        [Fact]
        public void LockFileAlreadyHeld_IsNotFirstInstance()
        {
            var pipe = UniquePipeName();
            var lockFile = LockPath();

            // Simulate lock held by another process (e.g., zombie from previous run).
            using var hold = File.Open(lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            using var ipc = new IpcChannel(pipe, lockFile);
            Assert.False(ipc.IsFirstInstance);
        }

        // --- Failed-construction lifecycle safety ---

        [Fact]
        public void FailedConstruction_AllOperationsSafe()
        {
            var pipe = UniquePipeName();
            var lockFile = LockPath();

            // Hold the lock externally to force construction failure.
            using var hold = File.Open(lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var ipc = new IpcChannel(pipe, lockFile);
            Assert.False(ipc.IsFirstInstance);

            // Send and multiple Dispose must be no-ops, not throw.
            var sendEx = Record.Exception(() => ipc.SendToFirstInstance("test"));
            Assert.Null(sendEx);

            var disposeEx = Record.Exception(() =>
            {
                ipc.Dispose();
                ipc.Dispose();
            });
            Assert.Null(disposeEx);
        }

        // --- Recovery & lock-release tests ---

        [Fact]
        public void StaleLockFile_RecoveryAfterRelease()
        {
            var lockFile = LockPath();

            // Simulate a stale lock from a crashed/zombie process.
            var stale = File.Open(lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

            using var failed = new IpcChannel(UniquePipeName(), lockFile);
            Assert.False(failed.IsFirstInstance);

            // "Kill" the zombie — release the stale lock.
            stale.Dispose();

            // A new instance should now be able to acquire the lock.
            using var recovered = new IpcChannel(UniquePipeName(), lockFile);
            Assert.True(recovered.IsFirstInstance);
        }

        [Fact]
        public void PartialConstruction_ReleasesLock()
        {
            var lockFile = LockPath();

            // null pipe name: File.Open succeeds (lock acquired), but
            // NamedPipeServerStream(null) throws ArgumentNullException.
            // The catch block MUST release the acquired lock, otherwise
            // no future instance can ever start.
            var ipc = new IpcChannel(null, lockFile);
            Assert.False(ipc.IsFirstInstance);

            // If the catch block didn't release the lock, this would fail
            // with IsFirstInstance = false (IOException on File.Open).
            using var newIpc = new IpcChannel(UniquePipeName(), lockFile);
            Assert.True(newIpc.IsFirstInstance);
        }
    }
}
