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
Check("all-locales-resource-and-format-arguments", () =>
{
    var resources = Application.Current!.Resources;
    var reference = ((ResourceDictionary)resources["en_US"]!).ToDictionary(x => (string)x.Key, x => (string)x.Value!);
    foreach (var locale in Locale.Supported)
    {
        var strings = ((ResourceDictionary)resources[locale.Key]!).ToDictionary(x => (string)x.Key, x => (string)x.Value!);
        Require(reference.Keys.ToHashSet().SetEquals(strings.Keys), $"{locale.Key}: resource keys differ from English.");
        foreach (var (key, english) in reference)
        {
            Require(!string.IsNullOrWhiteSpace(strings[key]), $"{locale.Key}: {key} is empty.");
            var expected = FormatArguments(english);
            Require(expected.SetEquals(FormatArguments(strings[key])), $"{locale.Key}: {key} format arguments differ.");
            if (expected.Count > 0)
                _ = System.Text.CompositeFormat.Parse(strings[key]);
        }
    }
});
Check("editor-search-localization-and-live-language-switch", () =>
{
    var editor = new AvaloniaEdit.TextEditor { Text = "alpha alpha\nbeta\n" };
    var host = new Window { Width = 750, Height = 250, Content = editor };
    try
    {
        host.Show();
        editor.SearchPanel.Open();
        editor.SearchPanel.SearchPattern = "absent";
        Pump();
        var search = editor.SearchPanel;
        var label = search.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "PART_MessageContent");
        var input = search.GetVisualDescendants().OfType<TextBox>().Single(x => x.Name == "PART_searchTextBox");
        foreach (var locale in new[] { "en_US", "ja_JP", "ca_ES" })
        {
            Komorebi.App.SetLocale(locale);
            editor.SearchPanel.SearchPattern = "absent";
            Pump();
            Require(label.Text == Komorebi.App.Text("TextSearch.NoMatches"),
                $"{locale}: search result='{label.Text}', expected='{Komorebi.App.Text("TextSearch.NoMatches")}', resource='{search.NoMatchesText}', closed={search.IsClosed}.");
            Require(input.PlaceholderText == Komorebi.App.Text("TextSearch.Placeholder"), $"{locale}: search placeholder was not localized.");
            Require(search.GetVisualDescendants().OfType<Button>().Any(x =>
                Equals(ToolTip.GetTip(x), Komorebi.App.Text("TextSearch.Next"))), $"{locale}: search tooltip was not localized.");
            search.SearchPattern = "alpha";
            Pump();
            Require(label.Text != Komorebi.App.Text("TextSearch.NoMatches") && label.Text!.Contains('2'), $"{locale}: match count was lost.");
            search.SearchPattern = "beta";
            Pump();
            Require(label.Text == Komorebi.App.Text("TextSearch.OneMatch") || label.Text == Komorebi.App.Text("TextSearch.MatchOfTotal", 1, 1),
                $"{locale}: single result was not localized.");
        }
    }
    finally
    {
        host.Close();
        Komorebi.App.SetLocale("en_US");
    }
});
Check("first-setup-empty-path-validation-in-all-locales", () =>
{
    try
    {
        foreach (var locale in Locale.Supported)
        {
            Komorebi.App.SetLocale(locale.Key);
            var setup = new Komorebi.ViewModels.InitSetup();
            Require(!setup.Check(), $"{locale.Key}: empty clone path was accepted.");
            Require(setup.GetErrors(nameof(setup.DefaultCloneDir)).Single().ErrorMessage == Komorebi.App.Text("Validation.PathRequired"),
                $"{locale.Key}: first setup validation used the framework's default English message.");
        }
    }
    finally
    {
        Komorebi.App.SetLocale("en_US");
    }
});
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
Check("clone-https-clears-hidden-ssh-key-validation", () =>
{
    // View の SSHKeyPicker は実 ~/.ssh を走査するため、このケースは VM のみを使う。
    var model = new Komorebi.ViewModels.Clone(string.Empty)
    {
        ParentFolder = root,
        Remote = "git@example.invalid:fixture.git",
        SSHKey = Path.Combine(root, "missing-private-key"),
    };
    Require(model.GetErrors(nameof(model.SSHKey)).Any() && !model.Check(), "Missing SSH key did not fail validation.");
    model.Remote = "https://example.invalid/fixture.git";
    Require(!model.UseSSH && !model.GetErrors(nameof(model.SSHKey)).Any() && model.Check(),
        "Changing to HTTPS left the hidden SSH key error blocking confirmation.");
    model.Remote = "git@example.invalid:fixture.git";
    Require(!model.Check(), "Returning to SSH failed to validate the missing key.");
    model.Cleanup();
});
Check("apply-clipboard-clears-disabled-file-validation", () =>
{
    var model = new Komorebi.ViewModels.Apply(repoA);
    var view = new Komorebi.Views.Apply { DataContext = model };
    var host = new Window { Width = 700, Height = 450, Content = view };
    try
    {
        host.Show();
        Pump();
        var input = view.FindControl<TextBox>("TxtPatchFile")!;
        input.Focus();
        host.KeyTextInput(Path.Combine(root, "missing.patch"));
        Pump();
        Require(model.GetErrors(nameof(model.PatchFile)).Any() && !model.Check(), "Missing patch did not fail validation.");
        var clipboard = view.GetVisualDescendants().OfType<RadioButton>().Single(x => Equals(x.Content, Komorebi.App.Text("Apply.Source.Clipboard")));
        var point = clipboard.TranslatePoint(new Point(8, clipboard.Bounds.Height / 2), host)!.Value;
        host.MouseDown(point, MouseButton.Left);
        host.MouseUp(point, MouseButton.Left);
        Pump();
        Require(model.FromClipboard && !input.IsEnabled && !model.GetErrors(nameof(model.PatchFile)).Any() && model.Check(),
            "Selecting clipboard left the disabled patch path error blocking confirmation.");
    }
    finally
    {
        host.Close();
        model.Cleanup();
    }
});
Check("git-flow-base-branch-edits-clear-dependent-validation", () =>
{
    var model = new Komorebi.ViewModels.InitGitFlow(repoA);
    var view = new Komorebi.Views.InitGitFlow { DataContext = model };
    var host = new Window { Width = 700, Height = 450, Content = view };
    try
    {
        host.Show();
        Pump();
        var inputs = view.GetVisualDescendants().OfType<TextBox>().ToArray();
        SetText(inputs[1], model.Master);
        Require(model.GetErrors(nameof(model.Develop)).Any() && !model.Check(), "Duplicate base branches did not fail validation.");
        SetText(inputs[0], "production");
        Require(!model.HasErrors && model.Check(), "Editing master left develop's duplicate-name error.");
        SetText(inputs[0], model.Develop);
        Require(model.GetErrors(nameof(model.Master)).Any() && !model.Check(), "Duplicate master did not fail validation.");
        SetText(inputs[1], "development");
        Require(!model.HasErrors && model.Check(), "Editing develop left master's duplicate-name error.");

        void SetText(TextBox input, string text)
        {
            input.Focus();
            input.SelectAll();
            host.KeyTextInput(text);
            Pump();
        }
    }
    finally
    {
        host.Close();
        model.Cleanup();
    }
});
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
foreach (var selection in new[] { "root", "child", "linked-child" })
{
    Check($"trust-confirmation-cancel-and-existing-bookmark-{selection}", () =>
    {
        var repository = MakeRepository($"trust-{selection}");
        var path = repository.FullPath;
        if (selection == "linked-child")
        {
            var worktree = Path.Combine(root, "linked-worktree");
            Git("-C", path, "worktree", "add", "-b", "linked", worktree);
            path = worktree;
        }
        var selectedPath = selection == "root" ? path : Path.Combine(path, "nested", "directory");
        Directory.CreateDirectory(selectedPath);
        var normalized = path.Replace('\\', '/').TrimEnd('/');
        var previous = Environment.GetEnvironmentVariable("GIT_TEST_ASSUME_DIFFERENT_OWNER");
        try
        {
            Environment.SetEnvironmentVariable("GIT_TEST_ASSUME_DIFFERENT_OWNER", "true");
            var blocked = new Komorebi.Commands.QueryRepositoryRootPath(selectedPath).GetResultAsync().GetAwaiter().GetResult();
            Require(!blocked.IsSuccess && SafeDirectories.IsUntrustedRepository(blocked.StdErr), "Fixture did not exercise Git ownership rejection.");
            Require(SafeDirectories.TryGetSafeDirectoryValue(selectedPath, blocked.StdErr, out var safeDirectory), "Could not identify the requested trust path.");
            Require(safeDirectory == normalized, "Trust target is not the actual repository root.");
            var popup = new Komorebi.ViewModels.TrustRepository(pageB.Node.Id, blocked.StdErr, safeDirectory, null!, false, false, 0);
            Require(popup.TargetPath.Replace('\\', '/') == normalized, "Confirmation does not display the repository root.");
            var trustView = new Komorebi.Views.TrustRepository { DataContext = popup };
            var trustHost = new Window { Content = trustView, Width = 800, Height = 300 };
            try
            {
                trustHost.Show();
                Pump();
                Require(trustView.GetVisualDescendants().OfType<SelectableTextBlock>().Single().Text == normalized,
                    "The displayed confirmation path is not the repository root.");
                Require(trustView.GetVisualDescendants().OfType<TextBlock>().Any(x => x.Text == popup.Command),
                    "The displayed trust command does not match the requested config write.");
            }
            finally
            {
                trustHost.Close();
            }
            Require(trustView.DataContext == popup && Preferences.Instance.FindNode(normalized) is null,
                "Showing the trust confirmation registered the repository.");
            var terminated = false;
            popup.PropertyChanged += (_, change) =>
            {
                if (change.PropertyName == nameof(popup.ProgressDescription) &&
                    popup.ProgressDescription == Komorebi.App.Text("Progress.OpeningRepository", popup.TargetPath))
                {
                    terminated = true;
                    popup.Terminate();
                }
            };
            var operation = popup.Sure();
            Until(() => operation.IsCompleted);
            Require(terminated && !operation.GetAwaiter().GetResult(), "Cancel after config write did not stop opening.");
            Require(Preferences.Instance.FindNode(normalized) is null, "Cancelled trust registered a repository node.");
            Require(File.ReadAllText(config).Contains("safe", StringComparison.Ordinal), "The fixture did not reach the real config write.");
            var existing = Preferences.Instance.FindOrAddNodeByRepositoryPath(path, null!, false, false);
            existing.Bookmark = 3;
            var retry = new Komorebi.ViewModels.TrustRepository(pageB.Node.Id, blocked.StdErr, safeDirectory, null!, false, false, 0);
            operation = retry.Sure();
            Until(() => operation.IsCompleted);
            Require(operation.GetAwaiter().GetResult() && existing.Bookmark == 3, "Trusting a registered repository lost its bookmark.");
            var reopened = new Komorebi.Commands.QueryRepositoryRootPath(selectedPath).GetResultAsync().GetAwaiter().GetResult();
            Require(reopened.IsSuccess && reopened.StdOut.Trim().Replace('\\', '/') == normalized,
                "Approving trust did not allow opening the selected directory.");
            Require(Git("config", "--global", "--get-all", "safe.directory").Split('\n', StringSplitOptions.RemoveEmptyEntries).All(x => x.Trim() != selectedPath.Replace('\\', '/') || selection == "root"),
                "The selected child directory was incorrectly registered as trusted.");
            popup.Cleanup();
            retry.Cleanup();
        }
        finally
        {
            Environment.SetEnvironmentVariable("GIT_TEST_ASSUME_DIFFERENT_OWNER", previous);
        }
    });
}
Check("statistics-pie-render-hover-and-author-toggle", () =>
{
    Git("-c", "user.name=Second", "-c", "user.email=second@example.invalid", "commit", "--allow-empty", "-m", "second author");
    Git("-c", "user.name=Third", "-c", "user.email=third@example.invalid", "commit", "--allow-empty", "-m", "third author");
    var model = new Komorebi.ViewModels.Statistics(repositoryPath);
    Until(() => !model.IsLoading && model.SelectedReport is { Total: 3 });
    var host = new Komorebi.Views.Statistics { DataContext = model };
    try
    {
        host.Show();
        Pump();
        Require(model.SelectedReport.Series[0].Values!.Cast<LiveChartsCore.Defaults.DateTimePoint>()
            .All(x => x.DateTime.Day == 1 && x.DateTime.TimeOfDay == TimeSpan.Zero),
            "Monthly history did not use calendar month-start coordinates.");
        var pie = host.GetVisualDescendants().OfType<PieChart>().Single();
        Require(pie.Bounds.Width > 0 && pie.Bounds.Height > 0, "Pie chart has no visible area.");
        using var image = new RenderTargetBitmap(new PixelSize((int)pie.Bounds.Width, (int)pie.Bounds.Height));
        image.Render(pie);
        image.Save(Path.Combine(root, "statistics-pie.png"), PngBitmapEncoderOptions.Default);
        var point = pie.TranslatePoint(new Point(pie.Bounds.Width / 2 + 10, pie.Bounds.Height / 2), host)!.Value;
        host.MouseMove(point);
        Pump();
        Require(pie.HoveringTip is { Count: 1 }, "Pie hover did not identify an author.");
        host.MouseDown(point, MouseButton.Left);
        host.MouseUp(point, MouseButton.Left);
        Pump();
        Require(model.SelectedAuthor is not null, "Pie click did not highlight author history.");
        host.MouseMove(new Point(1, 1));
        Pump();
        Require(pie.HoveringTip is null, "Pie hover survived leaving the chart.");
        image.Render(pie);
        host.MouseMove(point);
        host.MouseDown(point, MouseButton.Left);
        host.MouseUp(point, MouseButton.Left);
        Pump();
        Require(model.SelectedAuthor is null, "Clicking the selected author did not restore all authors.");
    }
    finally
    {
        host.Close();
    }
});
Check("remote-settings-preserve-custom-push-url-and-clear-to-fetch", () =>
{
    var repository = MakeRepository("remote-settings");
    var path = repository.FullPath;
    const string fetch = "https://fetch.example.invalid/repository.git";
    const string changedFetch = "https://fetch.example.invalid/renamed.git";
    var push = Path.Combine(root, "push[fixture].git").Replace('\\', '/');
    Git("-C", path, "remote", "add", "origin", fetch);
    Git("-C", path, "remote", "set-url", "--push", "origin", push);
    repository.Remotes.Add(new Remote { Name = "origin", URL = fetch, PushURL = push });
    var model = new Komorebi.ViewModels.RepositoryConfigure(repository);
    Until(() => model.SelectedRemotePushUrl == push);
    Require(!model.SelectedRemotePushDisabled, "A custom push URL was interpreted as a push prohibition.");
    model.UserName = "Fixture User";
    Save();
    Require(new Komorebi.Commands.Config(path).Get("remote.origin.pushurl") == push, "Unrelated settings overwrote the push URL.");
    model.SelectedRemoteUrl = changedFetch;
    Save();
    Require(new Komorebi.Commands.Config(path).Get("remote.origin.pushurl") == push, "Changing the fetch URL overwrote the push URL.");
    model.SelectedRemotePushDisabled = true;
    Save();
    Require(new Komorebi.Commands.Config(path).Get("remote.origin.pushurl") == "no_push", "Push prohibition was not saved.");
    model.SelectedRemotePushDisabled = false;
    Save();
    Require(new Komorebi.Commands.Config(path).Get("remote.origin.pushurl") == push, "Disabling push prohibition lost the custom URL.");
    model.SelectedRemotePushUrl = string.Empty;
    Save();
    Require(new Komorebi.Commands.Config(path).Get("remote.origin.pushurl").Length == 0, "Clearing a push URL containing regex characters failed.");
    var effectivePush = new Komorebi.Commands.Remote(path).GetURLAsync("origin", true);
    Until(() => effectivePush.IsCompleted);
    Require(effectivePush.GetAwaiter().GetResult() == changedFetch,
        "Clearing the push URL did not restore the fetch URL fallback.");

    const string sshFetch = "git@example.invalid:repository.git";
    var origin = repository.Remotes[0];
    var upstream = new Remote { Name = "upstream", URL = fetch };
    Git("-C", path, "remote", "set-url", "origin", sshFetch);
    Git("-C", path, "remote", "add", "upstream", fetch);
    origin.URL = sshFetch;
    repository.Remotes.Add(upstream);
    var sshKey = Path.Combine(root, "fixture-key");
    Git("-C", path, "config", "remote.origin.sshkey", sshKey);
    foreach (var preservedPush in new[] { push, "no_push" })
    {
        Git("-C", path, "remote", "set-url", "--push", "origin", preservedPush);
        // BranchTreeと同じ生成直後の切替。ロード完了前の空値を保存させない。
        model = new Komorebi.ViewModels.RepositoryConfigure(repository) { SelectedRemote = upstream };
        Save();
        Require(new Komorebi.Commands.Config(path).Get("remote.origin.pushurl") == preservedPush,
            "Selecting a remote before initial load overwrote the previous push URL.");
        Require(new Komorebi.Commands.Config(path).Get("remote.origin.sshkey") == sshKey,
            "Selecting a remote before initial load cleared the previous SSH key.");

        model.SelectedRemote = origin;
        model.SelectedRemote = upstream;
        model.SelectedRemote = origin;
        Until(() => model.IsRemoteSettingsLoaded);
        Require(model.SelectedRemoteSSHKey == sshKey && model.SelectedRemotePushDisabled == (preservedPush == "no_push"),
            "Rapid A/B/A selection loaded stale remote settings.");
        model.SelectedRemoteUrl = sshFetch + ".edited";
        model.SelectedRemote = upstream;
        model.SelectedRemote = origin;
        Save();
        Require(model.SelectedRemoteUrl == origin.URL && origin.URL == sshFetch + ".edited",
            "Reselecting a remote loaded its fetch URL before the pending save completed.");
        Require(new Komorebi.Commands.Config(path).Get("remote.origin.pushurl") == preservedPush &&
            new Komorebi.Commands.Config(path).Get("remote.origin.sshkey") == sshKey,
            "Rapid selection lost saved push or SSH settings.");
    }

    var settingsWindow = new Komorebi.Views.RepositoryConfigure { DataContext = model };
    try
    {
        settingsWindow.Show();
        Pump();
        var nameInput = settingsWindow.FindControl<TextBox>("TxtRemoteName")!;
        Require(nameInput.IsEffectivelyEnabled, "Loaded remote settings remained disabled.");
        model.SelectedRemote = upstream;
        Require(!nameInput.IsEffectivelyEnabled, "Remote settings remained editable during loading.");
        Until(() => model.IsRemoteSettingsLoaded);
        Require(nameInput.IsEffectivelyEnabled && nameInput.Text == upstream.Name, "Remote settings did not enable the loaded selection.");
        model.SelectedRemote = origin;
        Until(() => model.IsRemoteSettingsLoaded);
    }
    finally
    {
        settingsWindow.Close();
        Require(!settingsWindow.IsEnabled || !settingsWindow.IsVisible, "Settings remained interactive while saving on close.");
        settingsWindow.Close();
        Until(() => !settingsWindow.IsVisible);
    }

    Require(new Komorebi.Commands.Config(path).Get("remote.origin.pushurl") == "no_push" &&
        new Komorebi.Commands.Config(path).Get("remote.origin.sshkey") == sshKey,
        "Closing settings twice lost remote settings.");

    void Save()
    {
        var operation = model.SaveAsync();
        Until(() => operation.IsCompleted);
        operation.GetAwaiter().GetResult();
        Require(!model.HasSaveError, "Remote settings save failed.");
    }
});
Check("push-remote-triangular-and-upstream-destinations", () =>
{
    var repository = MakeRepository("push-destinations");
    var path = repository.FullPath;
    foreach (var name in new[] { "origin", "upstream" })
    {
        var remotePath = Path.Combine(root, name + ".git");
        Git("init", "--bare", remotePath);
        Git("-C", path, "remote", "add", name, remotePath);
        Git("-C", path, "push", name, "main");
        repository.Remotes.Add(new Remote { Name = name, URL = remotePath });
    }
    var mainHead = Git("-C", path, "rev-parse", "HEAD").Trim();
    Git("-C", path, "checkout", "-b", "feature");
    File.AppendAllText(Path.Combine(path, "readme.txt"), "\nfeature");
    Git("-C", path, "-c", "user.name=E2E", "-c", "user.email=e2e@example.invalid", "commit", "-am", "feature");
    Git("-C", path, "branch", "--set-upstream-to=upstream/main", "feature");
    Git("-C", path, "config", "branch.feature.pushRemote", "origin");
    foreach (var mode in new[] { "simple", "current" })
    {
        Git("-C", path, "config", "push.default", mode);
        Require(Git("-C", path, "push", "--dry-run", "--porcelain").Contains("refs/heads/feature:refs/heads/feature", StringComparison.Ordinal),
            $"Git {mode} did not select the expected triangular destination.");
        ReloadBranches();
        var model = new Komorebi.ViewModels.Push(repository, repository.Branches.Single(x => x.IsLocal && x.Name == "feature"));
        Require(model.SelectedRemote.Name == "origin" && model.SelectedRemoteBranch.Name == "feature",
            $"Push {mode} copied the upstream branch name to a different remote.");
        var operation = model.Sure();
        Until(() => operation.IsCompleted);
        Require(operation.GetAwaiter().GetResult(), "Pushing the selected destination failed.");
        Require(Git("--git-dir", Path.Combine(root, "origin.git"), "rev-parse", "refs/heads/main").Trim() == mainHead,
            "The triangular push modified origin/main.");
        Require(Git("--git-dir", Path.Combine(root, "origin.git"), "rev-parse", "refs/heads/feature").Trim() == Git("-C", path, "rev-parse", "HEAD").Trim(),
            "The triangular push did not update origin/feature.");
    }
    Git("-C", path, "config", "push.default", "upstream");
    Git("-C", path, "config", "branch.feature.pushRemote", "upstream");
    ReloadBranches();
    var central = new Komorebi.ViewModels.Push(repository, repository.Branches.Single(x => x.IsLocal && x.Name == "feature"));
    Require(central.SelectedRemote.Name == "upstream" && central.SelectedRemoteBranch.Name == "main",
        "Same-remote upstream push lost its tracked destination.");
    Require(Git("-C", path, "push", "--dry-run", "--porcelain").Contains("refs/heads/feature:refs/heads/main", StringComparison.Ordinal),
        "Git upstream did not select the expected tracked destination.");

    void ReloadBranches()
    {
        var query = new Komorebi.Commands.QueryBranches(path).GetResultAsync();
        Until(() => query.IsCompleted);
        typeof(VmRepository).GetProperty(nameof(VmRepository.Branches))!.SetValue(repository, query.GetAwaiter().GetResult());
    }
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

string Git(params string[] command)
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
    return output.Result;
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
    typeof(VmRepository).GetField("_settings", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(repo, RepositorySettings.Get(Path.Combine(repoPath, ".git")));
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
static HashSet<string> FormatArguments(string text) => System.Text.RegularExpressions.Regex.Matches(text, @"(?<!\{)\{(\d+)(?:[^{}]*)\}(?!\})")
    .Select(match => match.Groups[1].Value).ToHashSet();
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
