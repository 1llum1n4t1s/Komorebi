using System.Diagnostics;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Komorebi.AI;
using Komorebi.Models;
using Komorebi.ViewModels;

if (args is ["logger", var loggerRoot])
{
    Directory.CreateDirectory(loggerRoot);
    Logger.Initialize(new LoggerConfig { LogDirectory = loggerRoot, FilePrefix = "Komorebi" });
    Logger.Log("logger-before-unobserved");
    var handler = typeof(Komorebi.App).GetMethod("OnUnobservedTaskException", BindingFlags.Static | BindingFlags.NonPublic)!
        .CreateDelegate<EventHandler<UnobservedTaskExceptionEventArgs>>();
    using var observed = new ManualResetEventSlim();
    TaskScheduler.UnobservedTaskException += (_, e) =>
    {
        handler(null, e);
        if (e.Observed)
            observed.Set();
    };
    CreateUnobservedTask();
    for (var attempt = 0; attempt < 20 && !observed.IsSet; attempt++)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        observed.Wait(TimeSpan.FromMilliseconds(50));
    }
    Require(observed.IsSet, "Application handler did not observe the task exception.");
    Logger.Log("logger-after-unobserved");
    Logger.Dispose();
    var log = string.Join('\n', Directory.GetFiles(loggerRoot, "Komorebi_*.log").Select(File.ReadAllText));
    Require(log.Contains("logger-before-unobserved", StringComparison.Ordinal), "Initial log was lost.");
    Require(log.Contains("UnobservedTaskException", StringComparison.Ordinal), "Task exception log was lost.");
    Require(log.Contains("logger-after-unobserved", StringComparison.Ordinal), "Continuing application lost logging after the task exception.");
    return 0;
}

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
    await CheckAsync("logging-continues-after-unobserved-task-exception", VerifyLoggingAsync);
    await CheckAsync("encrypted-preferences-read-failure-save-twice-recover", VerifyCredentialsAsync);
    if (OperatingSystem.IsWindows())
        await CheckAsync("legacy-key-atomic-migration-failure-and-recovery", VerifyLegacyKeyMigrationAsync);
    await CheckAsync("ai-headers-through-provider-http-and-retry", VerifyHeadersAsync);
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

async Task VerifyHeadersAsync()
{
    foreach (var provider in Service.AllProviders)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var expectedRequests = provider == Provider.OpenAI ? 2 : 1;
        var receivedRequests = 0;
        var receive = Task.Run(async () =>
        {
            for (var attempt = 0; attempt < expectedRequests; attempt++)
            {
                using var client = await listener.AcceptTcpClientAsync(timeout.Token);
                await using var stream = client.GetStream();
                var headers = new StringBuilder();
                var one = new byte[1];
                var terminator = 0;
                do
                {
                    await stream.ReadExactlyAsync(one, timeout.Token);
                    headers.Append((char)one[0]);
                    terminator = (terminator << 8) | one[0];
                    Require(headers.Length < 65536, "HTTP header limit exceeded.");
                } while (terminator != 0x0d0a0d0a);
                var text = headers.ToString();
                Require(System.Text.RegularExpressions.Regex.IsMatch(text, @"(?im)^X-Fixture: synthetic-last-value\r?$"),
                    $"{provider}: additional header was missing or duplicated.");
                Require(System.Text.RegularExpressions.Regex.IsMatch(text, @"(?im)^Content-Type: application/json\r?$"),
                    $"{provider}: content header was not applied.");
                var length = System.Text.RegularExpressions.Regex.Match(text, @"(?im)^Content-Length:\s*(\d+)");
                Require(length.Success, "Fixture expects a fixed-length request body.");
                var size = int.Parse(length.Groups[1].Value);
                Require(size < 1024 * 1024, "HTTP body limit exceeded.");
                await stream.ReadExactlyAsync(new byte[size], timeout.Token);
                receivedRequests++;
                var retry = attempt == 0 && expectedRequests > 1;
                var body = retry ? "{\"error\":{\"message\":\"synthetic retry\",\"type\":\"rate_limit_error\"}}" :
                    provider == Provider.Anthropic
                        ? "{\"id\":\"fixture\",\"type\":\"message\",\"role\":\"assistant\",\"model\":\"fixture\",\"stop_reason\":\"end_turn\",\"content\":[{\"type\":\"text\",\"text\":\"test: header fixture\"}],\"usage\":{\"input_tokens\":1,\"output_tokens\":1}}"
                        : "{\"id\":\"fixture\",\"object\":\"chat.completion\",\"created\":1,\"model\":\"fixture\",\"choices\":[{\"index\":0,\"message\":{\"role\":\"assistant\",\"content\":\"test: header fixture\"},\"finish_reason\":\"stop\"}],\"usage\":{\"prompt_tokens\":1,\"completion_tokens\":1,\"total_tokens\":2}}";
                var bodyBytes = Encoding.UTF8.GetBytes(body);
                var status = retry ? "429 Too Many Requests" : "200 OK";
                var response = $"HTTP/1.1 {status}\r\nContent-Type: application/json\r\nContent-Length: {bodyBytes.Length}\r\nRetry-After: 0\r\nConnection: close\r\n\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(response), timeout.Token);
                await stream.WriteAsync(bodyBytes, timeout.Token);
            }
        }, timeout.Token);
        try
        {
            var service = new Service
            {
                Provider = provider,
                Server = $"http://127.0.0.1:{port}" + (provider == Provider.Anthropic ? "" : "/v1"),
                ApiKey = "synthetic-local-http-key",
                Model = "fixture",
                ExtraHeaders = "X-Fixture: synthetic-first-value\nContent-Type: application/json\nX-Fixture: synthetic-last-value",
            };
            var result = new StringBuilder();
            await new Agent(service).GenerateCommitMessageAsync(root, "fixture.txt", text => result.Append(text), timeout.Token);
            await receive;
            Require(receivedRequests == expectedRequests && result.ToString().Contains("test: header fixture", StringComparison.Ordinal),
                $"{provider}: provider output or retry was lost.");
            File.WriteAllText(Path.Combine(root, $"http-headers-{provider}.json"), JsonSerializer.Serialize(new
            {
                Provider = provider.ToString(),
                Requests = receivedRequests,
                ExpectedRequests = expectedRequests,
                AdditionalAndContentHeadersVerified = true,
                Output = result.ToString(),
            }));
        }
        finally
        {
            await timeout.CancelAsync();
            listener.Stop();
            try { await receive; }
            catch (Exception) when (timeout.IsCancellationRequested) { }
        }
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
    const string syntheticHeaders = "Authorization: Bearer synthetic-e2e-header-only\nX-Fixture: synthetic-header-value";
    Preferences ReadyToSave(Preferences preferences)
    {
        // 実アプリでは Instance が Load 後に読み込みフラグを下ろし、App が保存を許可する。
        typeof(Preferences).GetField("_isLoading", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(preferences, false);
        preferences.SetCanModify();
        return preferences;
    }
    Preferences Load() => ReadyToSave((Preferences)JsonSerializer.Deserialize(File.ReadAllText(file), info)!);
    string Cipher(string property = "ApiKey")
    {
        using var document = JsonDocument.Parse(File.ReadAllText(file));
        return document.RootElement.GetProperty("OpenAIServices")[0].GetProperty(property).GetString()!;
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
        preferences.OpenAIServices.Add(new Service { Name = "isolated", ApiKey = syntheticSecret, ExtraHeaders = syntheticHeaders });
        Save(preferences);
        var cipher = Cipher();
        var headersCipher = Cipher("ExtraHeaders");
        Require(cipher.StartsWith("komorebi:v1:aes:", StringComparison.Ordinal), "Synthetic key was not encrypted.");
        Require(headersCipher.StartsWith("komorebi:v1:aes:", StringComparison.Ordinal) &&
            !File.ReadAllText(file).Contains("synthetic-e2e-header-only", StringComparison.Ordinal), "Header credentials were stored in plaintext.");
        var originalKey = File.ReadAllBytes(keyFile);
        keyOverride.SetValue(null, fixture);
        using (var locked = File.Open(keyFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var unreadable = Load();
            Require(unreadable.OpenAIServices[0].ApiKey.Length == 0, "Key read failure did not occur.");
            Require(unreadable.OpenAIServices[0].ExtraHeaders.Length == 0, "Unreadable headers were exposed as ciphertext.");
            Save(unreadable);
            Require(Cipher() == cipher, "First save lost the unreadable cipher.");
            Save(unreadable);
            Require(Cipher() == cipher, "Second save lost the unreadable cipher.");
            Require(Cipher("ExtraHeaders") == headersCipher, "Saving lost unreadable encrypted headers.");
        }
        Require(File.ReadAllBytes(keyFile).SequenceEqual(originalKey), "Read failure replaced the original key.");
        keyOverride.SetValue(null, fixture);
        Require(Load().OpenAIServices[0].ApiKey == syntheticSecret, "Restored key could not decrypt the saved credential.");
        Require(Load().OpenAIServices[0].ExtraHeaders == syntheticHeaders, "Restored key could not decrypt the headers.");

        // DPAPI / 不正形式の読込失敗でも既存鍵を再生成しない。
        File.WriteAllText(keyFile, OperatingSystem.IsWindows() ? "dpapi:v1:AQIDBA==" : "AQIDBA==");
        var damagedKey = File.ReadAllBytes(keyFile);
        keyOverride.SetValue(null, fixture);
        var unreadableAgain = Load();
        Save(unreadableAgain);
        Save(unreadableAgain);
        Require(Cipher() == cipher, "Invalid key lost the cipher.");
        Require(Cipher("ExtraHeaders") == headersCipher, "Invalid key lost the header cipher.");
        Require(File.ReadAllBytes(keyFile).SequenceEqual(damagedKey), "Invalid key was regenerated.");
        Require(!File.Exists(keyFile + ".bak"), "Read failure created a replacement backup.");
        File.WriteAllBytes(keyFile, originalKey);
        keyOverride.SetValue(null, fixture);
        Require(Load().OpenAIServices[0].ApiKey == syntheticSecret, "Credential failed to recover after key restoration.");
        Require(Load().OpenAIServices[0].ExtraHeaders == syntheticHeaders, "Headers failed to recover after key restoration.");

        keyOverride.SetValue(null, fixture);
        using (var locked = File.Open(keyFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var clear = Load();
            clear.OpenAIServices[0].ApiKey = string.Empty;
            clear.OpenAIServices[0].ExtraHeaders = string.Empty;
            Save(clear);
            Require(Cipher().Length == 0, "Explicit clear retained an unreadable cipher.");
            Require(Cipher("ExtraHeaders").Length == 0, "Explicit clear retained unreadable headers.");
            var legacy = new Service { ProtectedApiKey = "synthetic-legacy-plain" };
            Require(legacy.ApiKey == "synthetic-legacy-plain" && legacy.ProtectedApiKey.Length == 0, "Failed legacy migration wrote plaintext back.");
            var legacyHeaders = new Service { ProtectedExtraHeaders = syntheticHeaders };
            Require(legacyHeaders.ExtraHeaders == syntheticHeaders && legacyHeaders.ProtectedExtraHeaders.Length == 0,
                "Failed header migration wrote plaintext back.");
        }

        // 旧平文 API キーの移行と環境変数名の解決も維持する。
        keyOverride.SetValue(null, fixture);
        var migrated = new Service { ProtectedApiKey = "synthetic-legacy-plain" };
        var migratedCipher = migrated.ProtectedApiKey;
        Require(new Service { ProtectedApiKey = migratedCipher }.ApiKey == "synthetic-legacy-plain", "Legacy plaintext did not migrate.");
        var migratedHeaders = new Service { ProtectedExtraHeaders = syntheticHeaders }.ProtectedExtraHeaders;
        Require(new Service { ProtectedExtraHeaders = migratedHeaders }.ExtraHeaders == syntheticHeaders, "Legacy headers did not migrate.");
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

Task VerifyLegacyKeyMigrationAsync()
{
    var fixture = Path.Combine(root, "legacy-key-migration");
    Directory.CreateDirectory(fixture);
    var protector = typeof(Service).Assembly.GetType("Komorebi.AI.ApiKeyProtector", throwOnError: true)!;
    var keyOverride = protector.GetProperty("KeyDirectoryOverride", BindingFlags.NonPublic | BindingFlags.Static)!;
    var readKey = protector.GetMethod("TryReadKeyFile", BindingFlags.NonPublic | BindingFlags.Static)!;
    var writeKey = protector.GetMethod("WriteKeyFile", BindingFlags.NonPublic | BindingFlags.Static)!;
    var oldOverride = keyOverride.GetValue(null);
    var contextType = typeof(Service).Assembly.GetType("Komorebi.JsonCodeGen", throwOnError: true)!;
    var context = (JsonSerializerContext)contextType.GetProperty("Default", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
    var info = context.GetTypeInfo(typeof(Preferences))!;
    var keyFile = Path.Combine(fixture, "ai-api-key.key");
    // 27664f25 の親版は32バイト鍵を Convert.ToBase64String + File.WriteAllText で保存していた。
    var rawKey = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
    var original = Encoding.UTF8.GetBytes(Convert.ToBase64String(rawKey));
    const string secret = "synthetic-legacy-migration-key";
    const string headers = "Authorization: Bearer synthetic-legacy-migration-header";
    void AssertNoStage() => Require(!Directory.EnumerateFiles(fixture, "ai-api-key.key.*.tmp").Any(), "Migration retained a staging key.");
    try
    {
        File.WriteAllBytes(keyFile, original);
        keyOverride.SetValue(null, fixture);
        var preferences = new Preferences();
        preferences.OpenAIServices.Add(new Service { ApiKey = secret, ExtraHeaders = headers });
        var json = JsonSerializer.Serialize(preferences, info);
        File.WriteAllText(Path.Combine(fixture, "preference.json"), json);
        File.WriteAllBytes(Path.Combine(fixture, "legacy-key-original.fixture"), original);
        Require(!json.Contains(secret, StringComparison.Ordinal) && !json.Contains(headers, StringComparison.Ordinal), "Legacy fixture stored plaintext credentials.");

        foreach (var failure in new[] { "partial-write", "replace", "success" })
        {
            File.WriteAllBytes(keyFile, original);
            keyOverride.SetValue(null, fixture);
            var injected = false;
            Action<Stream, string>? write = failure == "partial-write" ? (stream, content) =>
            {
                injected = true;
                var bytes = Encoding.UTF8.GetBytes(content);
                stream.Write(bytes, 0, bytes.Length / 2);
                throw new IOException("Synthetic staging write failure.");
            } : null;
            Action<string, string>? replace = failure == "replace" ? (stage, destination) =>
            {
                injected = true;
                Require(destination == keyFile && Path.GetDirectoryName(stage) == fixture, "Migration staged outside the key directory.");
                Require(File.ReadAllText(stage).StartsWith("dpapi:v1:", StringComparison.Ordinal), "Replacement received an incomplete staging key.");
                throw new IOException("Synthetic atomic replacement failure.");
            } : null;
            var loadedKey = (byte[])readKey.Invoke(null, [keyFile, write, replace])!;
            Require(loadedKey.SequenceEqual(rawKey), "Migration failure lost the usable legacy key.");
            if (failure != "success")
            {
                Require(injected, "Migration fault boundary was not exercised.");
                Require(File.ReadAllBytes(keyFile).SequenceEqual(original), "Migration failure changed the original key bytes.");
            }
            else
                Require(File.ReadAllText(keyFile).StartsWith("dpapi:v1:", StringComparison.Ordinal), "Successful migration did not protect the key with DPAPI.");
            AssertNoStage();

            // キャッシュを破棄して実 JSON を再読込し、次回起動相当の復号と移行を確認する。
            keyOverride.SetValue(null, fixture);
            var recovered = (Preferences)JsonSerializer.Deserialize(json, info)!;
            Require(recovered.OpenAIServices[0].ApiKey == secret && recovered.OpenAIServices[0].ExtraHeaders == headers,
                "Legacy credentials did not recover after a fresh key read.");
            var migratedKey = File.ReadAllBytes(keyFile);
            keyOverride.SetValue(null, fixture);
            var reloaded = (Preferences)JsonSerializer.Deserialize(json, info)!;
            Require(reloaded.OpenAIServices[0].ApiKey == secret && reloaded.OpenAIServices[0].ExtraHeaders == headers,
                "Migrated credentials did not survive another fresh key read.");
            Require(File.ReadAllBytes(keyFile).SequenceEqual(migratedKey), "Reading the migrated key rewrote it.");
            Require(Directory.EnumerateFiles(fixture, "ai-api-key.key*").Count() == 1, "Migration duplicated the authoritative key.");
            AssertNoStage();
        }

        // 新規生成の公開境界は既存鍵を上書きしない。生成競合の勝者を保つ。
        var winner = File.ReadAllBytes(keyFile);
        var collision = false;
        try
        {
            writeKey.Invoke(null, [keyFile, RandomNumberGenerator.GetBytes(32), false, null, null]);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is IOException)
        {
            collision = true;
        }
        Require(collision && File.ReadAllBytes(keyFile).SequenceEqual(winner), "New-key creation overwrote the winning key.");
        AssertNoStage();
        return Task.CompletedTask;
    }
    finally
    {
        keyOverride.SetValue(null, oldOverride);
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

async Task VerifyLoggingAsync()
{
    var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
    if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet")
        start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
    start.ArgumentList.Add("logger");
    start.ArgumentList.Add(Path.Combine(root, "logger"));
    using var peer = Process.Start(start)!;
    try
    {
        await peer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Require(peer.ExitCode == 0, "Logger child failed; inspect its retained log files.");
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

[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
static void CreateUnobservedTask() => _ = Task.FromException(new InvalidOperationException("synthetic-unobserved-task"));

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
