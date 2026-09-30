using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

// 親とサブモジュールの実Git呼出しに渡る認証環境を、人工キーだけで記録する。
if (Environment.GetEnvironmentVariable("CLONE_E2E_PROXY_GIT") is { Length: > 0 } realGit)
{
    var proxyLog = Environment.GetEnvironmentVariable("CLONE_E2E_PROXY_LOG")!;
    File.AppendAllText(proxyLog, JsonSerializer.Serialize(new
    {
        Arguments = args,
        Ssh = Environment.GetEnvironmentVariable("GIT_SSH_COMMAND"),
    }) + Environment.NewLine);
    var proxyStart = new ProcessStartInfo(realGit)
    {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };
    foreach (var argument in args)
        proxyStart.ArgumentList.Add(argument);
    using var proxy = Process.Start(proxyStart)!;
    var proxyOutput = proxy.StandardOutput.ReadToEndAsync();
    var proxyError = proxy.StandardError.ReadToEndAsync();
    await proxy.WaitForExitAsync();
    Console.Out.Write(await proxyOutput);
    Console.Error.Write(await proxyError);
    Environment.ExitCode = proxy.ExitCode;
    return;
}

// 実Gitを使い、通常成功・親checkout失敗・入れ子の失敗・無関係な失敗・中断を検証する。
// 成果物とfixtureはbin配下に保持し、global設定はこのプロセスだけ隔離する。
if (!OperatingSystem.IsWindows())
    throw new PlatformNotSupportedException("このE2EはGit for Windowsのパス制限を検証します。");

var git = args.Length > 0 ? args[0] : "git";
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "artifacts", Guid.NewGuid().ToString("N")));
Directory.CreateDirectory(root);
Console.WriteLine($"Artifacts: {root}");
var globalConfig = Path.Combine(root, "gitconfig");
File.WriteAllText(globalConfig, "[core]\n\tlongpaths = true\n");
Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", globalConfig);
Environment.SetEnvironmentVariable("GIT_CONFIG_NOSYSTEM", "1");
Environment.SetEnvironmentVariable("GIT_ALLOW_PROTOCOL", "file");
Komorebi.Native.OS.GitExecutable = git;

List<object> results = [];
var longFile = $"deep/{new string('p', 160)}.txt";
var ordinary = await CreateRepositoryAsync("ordinary", "readme.txt");
var longRepository = await CreateRepositoryAsync("long", longFile);
var middle = await CreateRepositoryAsync("middle", "readme.txt");
await RunGitAsync(middle, "submodule", "add", longRepository, "modules/grammar");
await CommitAsync(middle);
var nested = await CreateRepositoryAsync("nested", "readme.txt");
await RunGitAsync(nested, "submodule", "add", middle, "modules/parsers");
await CommitAsync(nested);
var broken = await CreateRepositoryAsync("broken", "readme.txt");
await RunGitAsync(broken, "submodule", "add", ordinary, "modules/dependency");
await RunGitAsync(broken, "config", "--file", ".gitmodules", "submodule.modules/dependency.url", Path.Combine(root, "missing-source"));
await RunGitAsync(broken, "add", ".gitmodules");
await CommitAsync(broken);
var unrecoverable = await CreateRepositoryAsync("unrecoverable", new string('f', 256));

await CheckCloneAsync("ordinary", ordinary, true, string.Empty, true, false, false);
await CheckCloneAsync("parent-checkout", longRepository, true, string.Empty, true, true, false);
await CheckCloneAsync("nested-submodules", nested, true, string.Empty, true, true, false);
await CheckCloneAsync("recursive-clone", nested, false, "--recurse-submodules", true, true, false);
await CheckCloneAsync("no-checkout", longRepository, true, "--no-checkout", true, false, false);
await CheckCloneAsync("bare", ordinary, true, "--bare", true, false, false);
await CheckCloneAsync("unrelated-error", Path.Combine(root, "missing-source"), true, string.Empty, false, false, false);
await CheckCloneAsync("submodule-unrelated-error", broken, true, string.Empty, false, false, false);
await CheckCloneAsync("existing-target", longRepository, true, string.Empty, false, false, false);
await CheckCloneAsync("retry-fails", unrecoverable, false, string.Empty, false, true, false);
await CheckCloneAsync("cancelled", nested, true, string.Empty, false, false, true);
await CheckCloneAsync("cancel-at-recovery", longRepository, true, string.Empty, false, false, false);
await CheckSshEnvironmentAsync("submodule-ssh-scope", middle);
await CheckSshEnvironmentAsync("submodule-ssh-scope-recovery", nested);

var report = new
{
    Git = git,
    Version = await RunGitAsync(root, "--version"),
    OS = Environment.OSVersion.ToString(),
    Runtime = Environment.Version.ToString(),
    WorkingDirectory = Environment.CurrentDirectory,
    ArtifactDirectory = root,
    IsolatedGlobalConfig = globalConfig,
    Results = results,
};
var reportPath = Path.Combine(root, "result.json");
File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(reportPath);

async Task<string> CreateRepositoryAsync(string name, string file)
{
    var repository = Path.Combine(root, "sources", name);
    Directory.CreateDirectory(repository);
    await RunGitAsync(repository, "init", "--initial-branch=main");
    var path = Path.Combine(repository, file);
    if (file.Split('/').All(component => component.Length <= 255))
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "fixture\n");
    }
    // addのディレクトリ走査自体にも制限があるため、fixtureのindexは直接構築する。
    var blob = (await RunGitWithInputAsync(repository, "fixture\n", "hash-object", "-w", "--stdin")).Trim();
    await RunGitAsync(repository, "update-index", "--add", "--cacheinfo", $"100644,{blob},{file}");
    await CommitAsync(repository);
    return repository;
}

async Task CommitAsync(string repository)
{
    await RunGitAsync(repository, "-c", "user.name=E2E", "-c", "user.email=e2e@example.invalid", "commit", "-m", "検証fixture");
}

async Task CheckCloneAsync(string name, string source, bool initialize, string extra, bool expectedSuccess, bool expectedRetry, bool cancel)
{
    var parent = Path.Combine(root, "clones");
    Directory.CreateDirectory(parent);
    var destination = Path.Combine(parent, name);
    File.WriteAllText(globalConfig, "[core]\n\tlongpaths = false\n");
    if (name == "existing-target")
    {
        Directory.CreateDirectory(destination);
        File.WriteAllText(Path.Combine(destination, "keep.txt"), "preserve\n");
    }
    var log = new CaptureLog();
    using var cancellation = new CancellationTokenSource();
    if (name == "cancel-at-recovery")
        log.OnLine = line =>
        {
            if (line.Contains("Filename too long", StringComparison.OrdinalIgnoreCase))
                cancellation.Cancel();
        };
    if (cancel)
        cancellation.Cancel();
    var command = new Komorebi.Commands.Clone(string.Empty, parent, source, name, string.Empty, extra)
    {
        RaiseError = false,
        Log = log,
        CancellationToken = cancellation.Token,
    };
    var success = await command.CloneAsync(destination, initialize);
    var lines = log.Lines.ToArray();
    File.WriteAllLines(Path.Combine(root, name + ".log"), lines);
    var retries = lines.Count(line => line.StartsWith("$ git config --global core.longpaths true", StringComparison.Ordinal));
    var globalValue = (await RunGitAsync(root, "config", "--global", "--get", "core.longpaths")).Trim();
    var passed = success == expectedSuccess && retries == (expectedRetry ? 1 : 0);
    passed &= globalValue == (expectedRetry ? "true" : "false");
    if (name == "existing-target")
        passed &= File.ReadAllText(Path.Combine(destination, "keep.txt")) == "preserve\n";

    if (success && extra != "--bare" && extra != "--no-checkout")
    {
        passed &= string.IsNullOrWhiteSpace(await RunGitAsync(destination, "status", "--porcelain"));
        if (expectedRetry)
        {
            var longPath = source == longRepository
                ? Path.Combine(destination, longFile)
                : Path.Combine(destination, "modules/parsers/modules/grammar", longFile);
            passed &= File.Exists(longPath) && File.ReadAllText(longPath) == "fixture\n";
        }
    }

    results.Add(new { Name = name, Success = success, RetryCount = retries, GlobalLongPaths = globalValue, Passed = passed, Error = command.ErrorMessage });
    Console.WriteLine($"{name}: {(passed ? "PASS" : "FAIL")}");
    if (!passed)
        throw new InvalidOperationException($"{name} の検証が失敗しました。ログ: {Path.Combine(root, name + ".log")}");
}

Task<string> RunGitAsync(string directory, params string[] arguments) => RunGitWithInputAsync(directory, null, arguments);

async Task CheckSshEnvironmentAsync(string name, string source)
{
    var parent = Path.Combine(root, "clones");
    var destination = Path.Combine(parent, name);
    var proxyLog = Path.Combine(root, name + ".jsonl");
    var key = Path.Combine(root, "synthetic-root-key");
    File.WriteAllText(key, "RERE_SYNTHETIC_KEY");
    File.WriteAllText(globalConfig, name.EndsWith("recovery", StringComparison.Ordinal)
        ? "[core]\n\tlongpaths = false\n" : "[core]\n\tlongpaths = true\n");
    var originalSsh = Environment.GetEnvironmentVariable("GIT_SSH_COMMAND");
    const string hostSsh = "ssh -F synthetic-host-config";
    Environment.SetEnvironmentVariable("GIT_SSH_COMMAND", hostSsh);
    Environment.SetEnvironmentVariable("CLONE_E2E_PROXY_GIT", git);
    Environment.SetEnvironmentVariable("CLONE_E2E_PROXY_LOG", proxyLog);
    Komorebi.Native.OS.GitExecutable = Path.ChangeExtension(typeof(CaptureLog).Assembly.Location, ".exe");
    var command = new Komorebi.Commands.Clone(string.Empty, parent, source, name, key, string.Empty)
    {
        RaiseError = false,
        Log = new CaptureLog(),
    };
    var credentialHelperFlag = typeof(Komorebi.Commands.Command).GetProperty("DisableCredentialHelper", BindingFlags.Instance | BindingFlags.NonPublic)!;
    credentialHelperFlag.SetValue(command, true);
    bool success;
    try
    {
        success = await command.CloneAsync(destination, true);
    }
    finally
    {
        Komorebi.Native.OS.GitExecutable = git;
        Environment.SetEnvironmentVariable("CLONE_E2E_PROXY_GIT", null);
        Environment.SetEnvironmentVariable("CLONE_E2E_PROXY_LOG", null);
        Environment.SetEnvironmentVariable("GIT_SSH_COMMAND", originalSsh);
    }

    var calls = File.ReadAllLines(proxyLog).Select(line => JsonDocument.Parse(line)).ToArray();
    try
    {
        var clone = calls.Single(call => call.RootElement.GetProperty("Arguments").EnumerateArray().Any(arg => arg.GetString() == "clone"));
        var submodules = calls.Where(call => call.RootElement.GetProperty("Arguments").EnumerateArray().Any(arg => arg.GetString() == "submodule")).ToArray();
        var passed = success && submodules.Length > 0 &&
            clone.RootElement.GetProperty("Ssh").GetString()!.Contains("synthetic-root-key", StringComparison.Ordinal) &&
            submodules.All(call => call.RootElement.GetProperty("Ssh").GetString() == hostSsh &&
                call.RootElement.GetProperty("Arguments").EnumerateArray().Select(arg => arg.GetString())
                    .LastOrDefault(arg => arg?.StartsWith("credential.helper=", StringComparison.Ordinal) == true) != "credential.helper=") &&
            command.SSHKey == key && (bool)credentialHelperFlag.GetValue(command)!;
        results.Add(new { Name = name, Success = success, Passed = passed, SubmoduleCalls = submodules.Length, Error = command.ErrorMessage });
        Console.WriteLine($"{name}: {(passed ? "PASS" : "FAIL")}");
        if (!passed)
            throw new InvalidOperationException($"{name} の認証環境が期待と異なります: {proxyLog}\n{command.ErrorMessage}");
    }
    finally
    {
        foreach (var call in calls)
            call.Dispose();
    }
}

async Task<string> RunGitWithInputAsync(string directory, string? input, params string[] arguments)
{
    var start = new ProcessStartInfo(git)
    {
        WorkingDirectory = directory,
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        RedirectStandardInput = input is not null,
    };
    start.ArgumentList.Add("-c");
    start.ArgumentList.Add("core.longpaths=true");
    start.ArgumentList.Add("-c");
    start.ArgumentList.Add("protocol.file.allow=always");
    foreach (var argument in arguments)
        start.ArgumentList.Add(argument);
    using var process = Process.Start(start)!;
    var stdout = process.StandardOutput.ReadToEndAsync();
    var stderr = process.StandardError.ReadToEndAsync();
    if (input is not null)
    {
        await process.StandardInput.WriteAsync(input);
        process.StandardInput.Close();
    }
    await process.WaitForExitAsync();
    var output = await stdout;
    var error = await stderr;
    if (process.ExitCode != 0)
        throw new InvalidOperationException($"git {string.Join(' ', arguments)}: {error}");
    return output;
}

internal sealed class CaptureLog : Komorebi.Models.ICommandLog
{
    public ConcurrentQueue<string> Lines { get; } = new();

    public Action<string>? OnLine { get; set; }

    public void AppendLine(string line)
    {
        Lines.Enqueue(line);
        OnLine?.Invoke(line);
    }
}
