using System;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace Komorebi.Models
{
    public class IpcChannel : IDisposable
    {
        public bool IsFirstInstance { get; }

        public event Action<string> MessageReceived;

        public IpcChannel()
            : this("KomorebiIPCChannel" + Environment.UserName, Path.Combine(Native.OS.DataDir, "process.lock"))
        {
        }

        internal IpcChannel(string pipeName, string lockFilePath)
        {
            try
            {
                _singletonLock = File.Open(lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                IsFirstInstance = true;
                _pipeName = pipeName;
                _server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    -1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                Task.Run(StartServer);
            }
            catch
            {
                IsFirstInstance = false;
            }
        }

        public void SendToFirstInstance(string cmd)
        {
            try
            {
                using (var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly))
                {
                    client.Connect(1000);
                    if (!client.IsConnected)
                        return;

                    using (var writer = new StreamWriter(client))
                    {
                        writer.WriteLine(cmd);
                        writer.Flush();
                    }

                    if (OperatingSystem.IsWindows())
                        client.WaitForPipeDrain();
                    else
                        System.Threading.Thread.Sleep(1000);
                }
            }
            catch
            {
                // IGNORE
            }
        }

        public void Dispose()
        {
            _disposed = true;

            // Connect a dummy client to unblock WaitForConnectionAsync cleanly.
            // This avoids IOException/OperationCanceledException leaking through
            // .NET's internal ValueTask-to-Task adapter on the thread pool.
            try
            {
                using var dummy = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
                dummy.Connect(500);
            }
            catch { /* pipe already closed or timeout */ }

            try { _server?.Dispose(); }
            catch { /* already disposed */ }

            try { _singletonLock?.Dispose(); }
            catch { /* already disposed */ }
        }

        private async void StartServer()
        {
            while (!_disposed)
            {
                try
                {
                    await _server.WaitForConnectionAsync();

                    if (_disposed)
                    {
                        try { _server.Disconnect(); }
                        catch { /* shutting down */ }
                        break;
                    }

                    using var reader = new StreamReader(_server, leaveOpen: true);
                    var line = await reader.ReadToEndAsync();
                    MessageReceived?.Invoke(line.Trim());

                    _server.Disconnect();
                }
                catch
                {
                    // Transient pipe error. If _disposed is set the loop
                    // exits naturally; otherwise we retry the next connection.
                }
            }
        }

        private volatile bool _disposed;
        private readonly string _pipeName;
        private FileStream _singletonLock;
        private NamedPipeServerStream _server;
    }
}
