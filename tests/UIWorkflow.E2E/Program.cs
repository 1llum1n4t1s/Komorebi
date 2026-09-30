using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Komorebi.Models;
using Komorebi.ViewModels;
using Komorebi.Views;
using SkiaSharp;
using Launcher = Komorebi.ViewModels.Launcher;
using LauncherPage = Komorebi.ViewModels.LauncherPage;
using Preferences = Komorebi.ViewModels.Preferences;
using VmRepository = Komorebi.ViewModels.Repository;

// 失敗候補: 削除画像の旧側欠落/古い差分残存、再利用Viewの購読残存、
// キーボード・UIAの無反応、Ctrlクリックの退行、BuildDateによるrevision非表示。
// 実Gitと実AXAMLを使うが、データ・設定・描画はbin配下とヘッドレス環境へ隔離する。
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "artifacts", Guid.NewGuid().ToString("N")));
Directory.CreateDirectory(root);
Console.WriteLine($"Artifacts: {root}");
var git = args.Length > 0 ? args[0] : @"C:\Program Files\Git\cmd\git.exe";
var config = Path.Combine(root, "gitconfig");
File.WriteAllText(config, "[core]\nlongpaths = true\n");
Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", config);
Environment.SetEnvironmentVariable("GIT_CONFIG_NOSYSTEM", "1");
Environment.SetEnvironmentVariable("GIT_ALLOW_PROTOCOL", "file");
var data = Path.Combine(root, "data");
Directory.CreateDirectory(data);
typeof(Komorebi.Native.OS).GetProperty("DataDir")!.SetValue(null, data);
Komorebi.Native.OS.GitExecutable = git;
var repositoryPath = Path.Combine(root, "repository");
Directory.CreateDirectory(repositoryPath);
Git("init", "--initial-branch=main");
using (var bitmap = new SKBitmap(4, 4))
{
    bitmap.Erase(SKColors.Orange);
    using var image = SKImage.FromBitmap(bitmap);
    using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
    File.WriteAllBytes(Path.Combine(repositoryPath, "deleted.png"), encoded.ToArray());
    File.WriteAllBytes(Path.Combine(repositoryPath, "other.png"), encoded.ToArray());
}
Git("add", ".");
Git("-c", "user.name=E2E", "-c", "user.email=e2e@example.invalid", "commit", "-m", "検証fixture");
File.Delete(Path.Combine(repositoryPath, "deleted.png"));
File.AppendAllText(Path.Combine(repositoryPath, "other.png"), "changed");
AppBuilder.Configure<Komorebi.App>().UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
Preferences.Instance.EnableAutoFetch = false;
Komorebi.Native.OS.GitExecutable = git;

List<object> results = [];
var failures = 0;
Check("deleted-image-through-git-diff-and-ui-apply", () =>
{
    var previous = new DiffContext(repositoryPath, new DiffOption(new Change { Path = "other.png", WorkTree = ChangeState.Modified }, true));
    Until(() => previous.Content is ImageDiff);
    var deleted = new DiffContext(repositoryPath, new DiffOption(new Change { Path = "deleted.png", WorkTree = ChangeState.Deleted }, true), previous);
    Until(() => deleted.Content is ImageDiff { Old: not null, New: null, NewFileSize: 0 });
    var diff = (ImageDiff)deleted.Content;
    Require(!ReferenceEquals(deleted.Content, previous.Content) && diff.OldFileSize > 0, "Old content survived the deleted-image update.");
    using var preview = new RenderTargetBitmap(new PixelSize(160, 80));
    var panel = new Image { Source = diff.Old, Width = 160, Height = 80, Stretch = Stretch.Uniform };
    panel.Measure(new Size(160, 80));
    panel.Arrange(new Rect(0, 0, 160, 80));
    preview.Render(panel);
    preview.Save(Path.Combine(root, "deleted-image-old-side.png"), PngBitmapEncoderOptions.Default);
});

var repoA = MakeRepository("A");
var repoB = MakeRepository("B");
var launcher = new Launcher(string.Empty);
typeof(Komorebi.App).GetField("_launcher", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Application.Current, launcher);
var pageA = new LauncherPage(new RepositoryNode { Id = repoA.FullPath, Name = "A" }, repoA);
var pageB = new LauncherPage(new RepositoryNode { Id = repoB.FullPath, Name = "B" }, repoB);
launcher.Pages.Add(pageA);
launcher.Pages.Add(pageB);
var pageView = new Komorebi.Views.LauncherPage { DataContext = pageA };
var window = new Window { Width = 1050, Height = 700, Content = pageView };
window.Show();
Pump();
Check("recycled-repository-search-focus-and-old-vm-unsubscribe", () =>
{
    var firstView = pageView.GetVisualDescendants().OfType<Komorebi.Views.Repository>().Single();
    pageView.DataContext = pageB;
    Pump();
    var currentView = pageView.GetVisualDescendants().OfType<Komorebi.Views.Repository>().Single();
    Require(ReferenceEquals(firstView, currentView), "Fixture did not exercise view recycling.");
    repoB.IsSearchingCommits = true;
    var search = currentView.FindControl<TextBox>("TxtSearchCommitsBox")!;
    Until(() => search.IsFocused);
    window.KeyTextInput("RERE_SEARCH");
    Require(search.Text == "RERE_SEARCH", "Search start did not focus the input field.");
    var button = currentView.GetVisualDescendants().OfType<ModifierButton>().First();
    button.Focus();
    repoA.IsSearchingCommits = true;
    Pump();
    Require(button.IsFocused, "Old repository still controls search focus.");
});
Check("fetch-pull-push-keyboard-and-uia-through-repository", () =>
{
    var view = pageView.GetVisualDescendants().OfType<Komorebi.Views.Repository>().Single();
    foreach (var button in view.GetVisualDescendants().OfType<ModifierButton>())
    {
        foreach (var input in new[] { "Enter", "Space", "UIA" })
        {
            var before = pageB.Notifications.Count;
            button.Focus();
            if (input == "UIA")
                ((IInvokeProvider)ControlAutomationPeer.CreatePeerForElement(button)!).Invoke();
            else
            {
                var key = input == "Enter" ? Key.Enter : Key.Space;
                var physical = input == "Enter" ? PhysicalKey.Enter : PhysicalKey.Space;
                window.KeyPress(key, RawInputModifiers.None, physical, null);
                window.KeyRelease(key, RawInputModifiers.None, physical, null);
            }
            Until(() => pageB.Notifications.Count == before + 1);
            Pump();
            Require(pageB.Notifications.Count == before + 1, "Button operation was dispatched twice.");
        }
    }
});
window.Close();
Pump();
Check("modifier-button-ctrl-pointer-and-uia-isolation", () =>
{
    var button = new ModifierButton { Content = "Operation", Width = 160, Height = 80 };
    var host = new Window { Width = 200, Height = 120, Content = button };
    List<KeyModifiers> clicks = [];
    button.Click += (_, _) => clicks.Add(button.ClickModifiers);
    host.Show();
    Pump();
    var point = button.TranslatePoint(new Point(20, 20), host)!.Value;
    host.MouseDown(point, MouseButton.Left, Avalonia.Input.RawInputModifiers.Control);
    host.MouseUp(point, MouseButton.Left, Avalonia.Input.RawInputModifiers.Control);
    ((IInvokeProvider)ControlAutomationPeer.CreatePeerForElement(button)!).Invoke();
    Pump();
    Require(clicks.SequenceEqual(new[] { KeyModifiers.Control, KeyModifiers.None }), "Ctrl input leaked or was lost.");
    host.Close();
});
Check("about-build-date-and-git-revision", () =>
{
    var assembly = typeof(Komorebi.App).Assembly;
    var info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
    var revision = info[(info.IndexOf('+') + 1)..];
    Require(revision.Length >= 10 && info.Contains('+'), "Build lacks the commit metadata required by this fixture.");
    var about = new About();
    Require(about.FindControl<StackPanel>("PnlGitSourceRevision")!.IsVisible &&
        about.FindControl<SelectableTextBlock>("TxtGitSourceRevision")!.Text == revision[..10], "About omitted the source revision.");
    about.Close();
});
File.WriteAllText(Path.Combine(root, "result.json"), JsonSerializer.Serialize(new
{
    ArtifactDirectory = root,
    Runtime = Environment.Version.ToString(),
    Git = git,
    Failures = failures,
    Results = results,
    Limitation = "Headless control input and real local Git are exercised; native desktop and external SSH are not.",
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(Path.Combine(root, "result.json"));
return failures == 0 ? 0 : 1;

void Git(params string[] command)
{
    var start = new ProcessStartInfo(git) { WorkingDirectory = repositoryPath, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var item in command)
        start.ArgumentList.Add(item);
    using var process = Process.Start(start)!;
    var output = process.StandardOutput.ReadToEndAsync();
    var error = process.StandardError.ReadToEndAsync();
    process.WaitForExit();
    Task.WaitAll(output, error);
    Require(process.ExitCode == 0, error.Result);
}

VmRepository MakeRepository(string name)
{
    var repoPath = Path.Combine(root, name);
    Directory.CreateDirectory(repoPath);
    Git("-C", repoPath, "init", "--initial-branch=main");
    File.WriteAllText(Path.Combine(repoPath, "readme.txt"), "RERE_UI_FIXTURE");
    Git("-C", repoPath, "add", ".");
    Git("-C", repoPath, "-c", "user.name=E2E", "-c", "user.email=e2e@example.invalid", "commit", "-m", "検証fixture");
    var repo = new VmRepository(false, repoPath, Path.Combine(repoPath, ".git"));
    typeof(VmRepository).GetField("_uiStates", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(repo, new RepositoryUIStates());
    typeof(VmRepository).GetField("_settings", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(repo, new RepositorySettings());
    typeof(VmRepository).GetField("_searchCommitContext", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(repo, new SearchCommitContext(repo));
    return repo;
}

void Check(string name, Action action)
{
    try
    { action(); results.Add(new { Name = name, Passed = true }); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failures++; results.Add(new { Name = name, Passed = false, Error = ex.ToString() }); Console.WriteLine($"FAIL {name}: {ex.Message}"); }
}

static void Pump() => Dispatcher.UIThread.RunJobs();
static void Require(bool passed, string message) { if (!passed) throw new InvalidOperationException(message); }
static void Until(Func<bool> condition)
{
    var timeout = Stopwatch.StartNew();
    while (!condition())
    {
        Pump();
        if (timeout.Elapsed > TimeSpan.FromSeconds(8))
            throw new TimeoutException("Expected workflow state did not arrive.");
        Thread.Sleep(10);
    }
    Pump();
}
