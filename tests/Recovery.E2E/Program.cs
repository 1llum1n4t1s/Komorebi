using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using Komorebi.AI;
using Komorebi.Models;
using Komorebi.ViewModels;

// 実ファイル、Windows DPAPI / AES-GCM、別プロセスのロックとパイプで復旧経路を検証する。
// 内部 API は reflection で呼び、製品側に E2E 専用の公開 API を加えない。
if (args is ["peer", var peerRoot, var peerPipe, var peerMode])
{
    if (peerMode == "delayed")
    {
        using var held = File.Open(Path.Combine(peerRoot, "process.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        File.WriteAllText(Path.Combine(peerRoot, "ready"), "lock held");
        await Task.Delay(1300);
        using var server = new NamedPipeServerStream(peerPipe, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await server.WaitForConnectionAsync();
        using var reader = new StreamReader(server);
        File.WriteAllText(Path.Combine(peerRoot, "received"), (await reader.ReadToEndAsync()).Trim());
    }
    else
    {
        using var channel = CreateChannel(peerPipe, Path.Combine(peerRoot, "process.lock"));
        if (!channel.IsFirstInstance)
            return 2;
        channel.MessageReceived += message => File.WriteAllText(Path.Combine(peerRoot, "received"), message);
        File.WriteAllText(Path.Combine(peerRoot, "ready"), "server ready");
        await WaitUntilAsync(() => File.Exists(Path.Combine(peerRoot, "stop")));
    }
    return 0;
}

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "artifacts", Guid.NewGuid().ToString("N")));
Directory.CreateDirectory(root);
Console.WriteLine($"Artifacts: {root}");
List<object> results = [];
var failures = 0;
try
{
    await CheckAsync("encrypted-preferences-read-failure-save-twice-recover", VerifyCredentialsAsync);
    await CheckAsync("ipc-cross-process-forward-and-peer-exit-recovery", () => VerifyIpcAsync("normal"));
    await CheckAsync("ipc-lock-before-pipe-start-race", () => VerifyIpcAsync("delayed"));
    await CheckAsync("ipc-pipe-init-failure-and-lock-release", () =>
    {
        var lockPath = Path.Combine(root, "failed-pipe.lock");
        using var failed = CreateChannel(null, lockPath);
        Require(failed.State == IpcChannelState.Failed && failed.LastError is not null, "Pipe failure was classified as another instance.");
        using var recovered = CreateChannel(UniquePipe(), lockPath);
        Require(recovered.IsFirstInstance, "Failed pipe initialization retained the lock.");
        return Task.CompletedTask;
    });
    await CheckAsync("ipc-held-lock-without-server-send-fails-and-recovers", () =>
    {
        var lockPath = Path.Combine(root, "held.lock");
        using var held = File.Open(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        using var channel = CreateChannel(UniquePipe(), lockPath);
        Require(channel.State == IpcChannelState.LockUnavailable, "Held lock state was lost.");
        Require(!channel.SendToFirstInstance("synthetic-repository"), "Sending to an absent server succeeded.");
        held.Dispose();
        Retry(channel);
        Require(channel.IsFirstInstance, "Released lock did not recover in the same instance.");
        return Task.CompletedTask;
    });
}
finally
{
    File.WriteAllText(Path.Combine(root, "result.json"), JsonSerializer.Serialize(new
    {
        OS = Environment.OSVersion.ToString(),
        Runtime = Environment.Version.ToString(),
        ArtifactDirectory = root,
        Failures = failures,
        Results = results,
        Limitation = "GUI initialization and App exit codes are not executed by this console E2E.",
    }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine(Path.Combine(root, "result.json"));
}
return failures == 0 ? 0 : 1;

async Task CheckAsync(string name, Func<Task> run)
{
    try
    {
        await run();
        results.Add(new { Name = name, Passed = true });
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failures++;
        results.Add(new { Name = name, Passed = false, Error = ex.ToString() });
        Console.WriteLine($"FAIL {name}: {ex.Message}");
    }
}

Task VerifyCredentialsAsync()
{
    var fixture = Path.Combine(root, "credentials");
    Directory.CreateDirectory(fixture);
    var dataDir = typeof(Komorebi.Native.OS).GetProperty("DataDir", BindingFlags.Public | BindingFlags.Static)!;
    var oldDataDir = dataDir.GetValue(null);
    dataDir.SetValue(null, fixture);
    var keyOverride = typeof(Service).Assembly.GetType("Komorebi.AI.ApiKeyProtector", throwOnError: true)!
        .GetProperty("KeyDirectoryOverride", BindingFlags.NonPublic | BindingFlags.Static)!;
    var oldOverride = keyOverride.GetValue(null);
    var contextType = typeof(Service).Assembly.GetType("Komorebi.JsonCodeGen", throwOnError: true)!;
    var context = (JsonSerializerContext)contextType.GetProperty("Default", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
    var info = context.GetTypeInfo(typeof(Preferences))!;
    var file = Path.Combine(fixture, "preference.json");
    var keyFile = Path.Combine(fixture, "ai-api-key.key");
    const string syntheticSecret = "synthetic-e2e-key-only";
    Preferences ReadyToSave(Preferences preferences)
    {
        // 実アプリでは Instance が Load 後に読み込みフラグを下ろし、App が保存を許可する。
        typeof(Preferences).GetField("_isLoading", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(preferences, false);
        preferences.SetCanModify();
        return preferences;
    }
    Preferences Load() => ReadyToSave((Preferences)JsonSerializer.Deserialize(File.ReadAllText(file), info)!);
    string Cipher()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(file));
        return document.RootElement.GetProperty("OpenAIServices")[0].GetProperty("ApiKey").GetString()!;
    }
    void Save(Preferences preferences)
    {
        // 同じ暗号文が残っただけの silent no-op を成功扱いにせず、実際のファイル置換を確認する。
        var sentinel = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        if (File.Exists(file))
            File.SetLastWriteTimeUtc(file, sentinel);
        preferences.Save();
        Require(File.Exists(file) && File.GetLastWriteTimeUtc(file) != sentinel, "Preferences.Save did not write the isolated file.");
    }
    try
    {
        keyOverride.SetValue(null, fixture);
        var preferences = ReadyToSave(new Preferences());
        preferences.OpenAIServices.Add(new Service { Name = "isolated", ApiKey = syntheticSecret });
        Save(preferences);
        var cipher = Cipher();
        Require(cipher.StartsWith("komorebi:v1:aes:", StringComparison.Ordinal), "Synthetic key was not encrypted.");
        var originalKey = File.ReadAllBytes(keyFile);
        keyOverride.SetValue(null, fixture);
        using (var locked = File.Open(keyFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var unreadable = Load();
            Require(unreadable.OpenAIServices[0].ApiKey.Length == 0, "Key read failure did not occur.");
            Save(unreadable);
            Require(Cipher() == cipher, "First save lost the unreadable cipher.");
            Save(unreadable);
            Require(Cipher() == cipher, "Second save lost the unreadable cipher.");
        }
        Require(File.ReadAllBytes(keyFile).SequenceEqual(originalKey), "Read failure replaced the original key.");
        keyOverride.SetValue(null, fixture);
        Require(Load().OpenAIServices[0].ApiKey == syntheticSecret, "Restored key could not decrypt the saved credential.");

        // DPAPI / 不正形式の読込失敗でも既存鍵を再生成しない。
        File.WriteAllText(keyFile, OperatingSystem.IsWindows() ? "dpapi:v1:AQIDBA==" : "AQIDBA==");
        var damagedKey = File.ReadAllBytes(keyFile);
        keyOverride.SetValue(null, fixture);
        var unreadableAgain = Load();
        Save(unreadableAgain);
        Save(unreadableAgain);
        Require(Cipher() == cipher, "Invalid key lost the cipher.");
        Require(File.ReadAllBytes(keyFile).SequenceEqual(damagedKey), "Invalid key was regenerated.");
        Require(!File.Exists(keyFile + ".bak"), "Read failure created a replacement backup.");
        File.WriteAllBytes(keyFile, originalKey);
        keyOverride.SetValue(null, fixture);
        Require(Load().OpenAIServices[0].ApiKey == syntheticSecret, "Credential failed to recover after key restoration.");

        keyOverride.SetValue(null, fixture);
        using (var locked = File.Open(keyFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var clear = Load();
            clear.OpenAIServices[0].ApiKey = string.Empty;
            Save(clear);
            Require(Cipher().Length == 0, "Explicit clear retained an unreadable cipher.");
            var legacy = new Service { ProtectedApiKey = "synthetic-legacy-plain" };
            Require(legacy.ApiKey == "synthetic-legacy-plain" && legacy.ProtectedApiKey.Length == 0, "Failed legacy migration wrote plaintext back.");
        }

        // 旧平文 API キーの移行と環境変数名の解決も維持する。
        keyOverride.SetValue(null, fixture);
        var migrated = new Service { ProtectedApiKey = "synthetic-legacy-plain" };
        var migratedCipher = migrated.ProtectedApiKey;
        Require(new Service { ProtectedApiKey = migratedCipher }.ApiKey == "synthetic-legacy-plain", "Legacy plaintext did not migrate.");
        var variable = "KOMOREBI_RECOVERY_E2E_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(variable, syntheticSecret);
        try
        {
            var fromEnv = new Service { ApiKey = variable, ReadApiKeyFromEnv = true };
            Require(fromEnv.ResolvedApiKey == syntheticSecret, "Environment key resolution changed.");
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
        return Task.CompletedTask;
    }
    finally
    {
        keyOverride.SetValue(null, oldOverride);
        dataDir.SetValue(null, oldDataDir);
    }
}

async Task VerifyIpcAsync(string mode)
{
    var fixture = Path.Combine(root, "ipc-" + mode);
    Directory.CreateDirectory(fixture);
    var pipe = UniquePipe();
    var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
    if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet")
        start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
    foreach (var argument in new[] { "peer", fixture, pipe, mode })
        start.ArgumentList.Add(argument);
    using var peer = Process.Start(start)!;
    try
    {
        await WaitUntilAsync(() => File.Exists(Path.Combine(fixture, "ready")));
        using var channel = CreateChannel(pipe, Path.Combine(fixture, "process.lock"));
        Require(channel.State == IpcChannelState.LockUnavailable, "Second process acquired the peer lock.");
        var sent = false;
        for (var attempt = 0; attempt < 3 && !sent; attempt++)
            sent = channel.SendToFirstInstance("synthetic-repository");
        Require(sent, "Message was not forwarded after the server became ready.");
        await WaitUntilAsync(() => File.Exists(Path.Combine(fixture, "received")));
        Require(File.ReadAllText(Path.Combine(fixture, "received")) == "synthetic-repository", "Forwarded payload changed.");
        File.WriteAllText(Path.Combine(fixture, "stop"), "normal shutdown");
        await peer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Require(peer.ExitCode == 0, "Peer process failed.");
        Retry(channel);
        Require(channel.IsFirstInstance, "Peer shutdown did not allow lock recovery.");
    }
    finally
    {
        if (!peer.HasExited)
        {
            peer.Kill(entireProcessTree: true);
            await peer.WaitForExitAsync();
        }
    }
}

static IpcChannel CreateChannel(string? pipe, string path) => (IpcChannel)Activator.CreateInstance(typeof(IpcChannel), BindingFlags.Instance | BindingFlags.NonPublic, null, [pipe, path], null)!;
static void Retry(IpcChannel channel) => typeof(IpcChannel).GetMethod("TryAcquireFirstInstance", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(channel, null);
static string UniquePipe() => "KomorebiRecoveryE2E_" + Guid.NewGuid().ToString("N");
static void Require(bool passed, string message)
{
    if (!passed)
        throw new InvalidOperationException(message);
}
static async Task WaitUntilAsync(Func<bool> ready)
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    while (!ready())
        await Task.Delay(20, timeout.Token);
}
