// nullable 移行未実施。1 ファイルずつ null 注釈を入れてこの 2 行を削除していく。
#nullable disable warnings
using System;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Komorebi.Models;

public enum IpcChannelState
{
    FirstInstance,
    LockUnavailable,
    Failed,
}

/// <summary>
/// 名前付きパイプによるプロセス間通信チャネル。
/// アプリケーションの多重起動防止と、既存インスタンスへのメッセージ送信を行う。
/// </summary>
public class IpcChannel : IDisposable
{
    /// <summary>最初のインスタンス（サーバー側）かどうか</summary>
    public bool IsFirstInstance => State == IpcChannelState.FirstInstance;

    public IpcChannelState State { get; private set; } = IpcChannelState.Failed;

    public Exception LastError { get; private set; }

    /// <summary>他のインスタンスからメッセージを受信した際のイベント</summary>
    public event Action<string> MessageReceived;

    public IpcChannel()
        : this(CreatePipeName(Native.OS.DataDir), Path.Combine(Native.OS.DataDir, "process.lock"))
    {
    }

    internal static string CreatePipeName(string dataDir)
    {
        var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataDir));
        if (OperatingSystem.IsWindows())
            path = path.ToUpperInvariant();

        var identity = Encoding.UTF8.GetBytes(Environment.UserName + "\n" + path);
        return "KomorebiIPC_" + Convert.ToHexString(SHA256.HashData(identity))[..32];
    }

    internal IpcChannel(string pipeName, string lockFilePath)
    {
        _pipeName = pipeName;
        _lockFilePath = lockFilePath;
        TryAcquireFirstInstance();
    }

    internal void TryAcquireFirstInstance()
    {
        if (_disposed || IsFirstInstance)
            return;

        try
        {
            var dir = Path.GetDirectoryName(_lockFilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            try
            {
                _singletonLock = File.Open(_lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException ex)
            {
                // ロック未取得だけでは他インスタンスの存在を断定せず、送信結果で判定する。
                LastError = ex;
                State = IpcChannelState.LockUnavailable;
                return;
            }

            _server = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.In,
                -1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            State = IpcChannelState.FirstInstance;
            LastError = null;
            Task.Run(StartServer);
        }
        catch (Exception ex)
        {
            // Release the lock if we acquired it but failed to start the server.
            // Without this, the lock would be held indefinitely, blocking all
            // future instances from starting.
            try
            {
                _singletonLock?.Dispose();
            }
            catch
            {
                /* best-effort cleanup */
            }
            _singletonLock = null;

            LastError = ex;
            State = IpcChannelState.Failed;
        }
    }

    /// <summary>
    /// 最初のインスタンス（サーバー）にコマンドメッセージを送信する
    /// </summary>
    /// <param name="cmd">送信するコマンド文字列</param>
    /// <returns>パイプへの送信に成功した場合のみ true。</returns>
    public bool SendToFirstInstance(string cmd)
    {
        try
        {
            using (var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly))
            {
                client.Connect(1000);
                if (!client.IsConnected)
                    return false;

                using (var writer = new StreamWriter(client))
                {
                    writer.WriteLine(cmd);
                    writer.Flush();
                    if (OperatingSystem.IsWindows())
                        client.WaitForPipeDrain();
                }
                // POSIX では writer Dispose で write end が close され、サーバー側 ReadToEndAsync が EOF で完了する。
                // 旧コードは Thread.Sleep(1000) で代替していたが、CLI 起動シナリオで毎回 1 秒遅延の原因だった。
            }
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex;
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_server is not null)
        {
            // Connect a dummy client to unblock WaitForConnectionAsync cleanly.
            // This avoids IOException/OperationCanceledException leaking through
            // .NET's internal ValueTask-to-Task adapter on the thread pool.
            try
            {
                using var dummy = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
                dummy.Connect(500);
            }
            catch { /* pipe already closed or timeout */ }

            try
            { _server.Dispose(); }
            catch { /* already disposed */ }
            _server = null;
        }

        try
        { _singletonLock?.Dispose(); }
        catch { /* already disposed */ }
        _singletonLock = null;
    }

    // async void → async Task に変更。未処理例外がプロセスをクラッシュさせるリスクを排除
    // Task.Run()経由で呼ばれるため、戻り値のTaskは自動的にスレッドプールに委譲される
    private async Task StartServer()
    {
        while (!_disposed)
        {
            try
            {
                await _server.WaitForConnectionAsync().ConfigureAwait(false);

                if (_disposed)
                    break;

                using var reader = new StreamReader(_server, leaveOpen: true);
                var line = await reader.ReadToEndAsync().ConfigureAwait(false);
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
    private readonly string _lockFilePath;
    private FileStream _singletonLock;
    private NamedPipeServerStream _server;
}
