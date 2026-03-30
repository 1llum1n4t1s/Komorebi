using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// ディレクトリ内のGitリポジトリを自動スキャンするポップアップダイアログのViewModel。
/// 指定ディレクトリ配下を再帰的に探索し、未管理のリポジトリを発見してツリーに追加する。
/// </summary>
public class ScanRepositories : Popup
{
    /// <summary>
    /// カスタムディレクトリを使用するかどうか。falseの場合はプリセットから選択。
    /// </summary>
    public bool UseCustomDir
    {
        get => _useCustomDir;
        set => SetProperty(ref _useCustomDir, value);
    }

    /// <summary>
    /// ユーザーが指定したカスタムスキャンディレクトリのパス。
    /// </summary>
    public string CustomDir
    {
        get => _customDir;
        set => SetProperty(ref _customDir, value);
    }

    /// <summary>
    /// スキャン対象ディレクトリの選択肢リスト（ワークスペース/グローバル設定から取得）。
    /// </summary>
    public List<Models.ScanDir> ScanDirs
    {
        get;
    }

    /// <summary>
    /// 選択されたスキャンディレクトリ。
    /// </summary>
    public Models.ScanDir Selected
    {
        get => _selected;
        set => SetProperty(ref _selected, value, true);
    }

    /// <summary>
    /// コンストラクタ。ワークスペースとグローバル設定からスキャンディレクトリ候補を初期化する。
    /// </summary>
    public ScanRepositories()
    {
        ScanDirs = [];

        var workspace = Preferences.Instance.GetActiveWorkspace();
        if (!string.IsNullOrEmpty(workspace.DefaultCloneDir))
            ScanDirs.Add(new Models.ScanDir(workspace.DefaultCloneDir, "Workspace"));

        if (!string.IsNullOrEmpty(Preferences.Instance.GitDefaultCloneDir))
            ScanDirs.Add(new Models.ScanDir(Preferences.Instance.GitDefaultCloneDir, "Global"));

        if (ScanDirs.Count > 0)
            _selected = ScanDirs[0];
        else
            _useCustomDir = true;

        GetManagedRepositories(Preferences.Instance.RepositoryNodes, _managed);
    }

    /// <summary>
    /// 指定ディレクトリ内のGitリポジトリをスキャンしてツリーに追加する。
    /// ポップアップダイアログなしで直接実行されるスタティックメソッド。
    /// </summary>
    public static async Task ScanDirectoryAsync(string rootDir)
    {
        if (string.IsNullOrEmpty(rootDir) || !Directory.Exists(rootDir))
            return;

        if (!Preferences.Instance.IsGitConfigured())
            return;

        try
        {
            HashSet<string> managed = [];
            GetManagedRepositories(Preferences.Instance.RepositoryNodes, managed);

            var rootDirInfo = new DirectoryInfo(rootDir);
            List<string> found = [];

            await GetUnmanagedRepositoriesAsync(rootDirInfo, found, managed, new EnumerationOptions()
            {
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                IgnoreInaccessible = true,
            });

            if (found.Count > 0)
                await AddFoundRepositories(rootDirInfo, found);
        }
        catch (Exception ex)
        {
            App.RaiseException(null, App.Text("Error.FailedToScanRepositories", ex.Message));
        }
    }

    /// <summary>
    /// スキャン操作を実行する。選択またはカスタム指定されたディレクトリを再帰的に探索する。
    /// </summary>
    public override async Task<bool> Sure()
    {
        string selectedDir;
        if (_useCustomDir)
        {
            if (string.IsNullOrEmpty(_customDir))
            {
                App.RaiseException(null, App.Text("Error.MissingScanDir"));
                return false;
            }

            selectedDir = _customDir;
        }
        else
        {
            if (_selected is null || string.IsNullOrEmpty(_selected.Path))
            {
                App.RaiseException(null, App.Text("Error.MissingScanDir"));
                return false;
            }

            selectedDir = _selected.Path;
        }

        if (!Directory.Exists(selectedDir))
            return true;

        ProgressDescription = App.Text("Progress.ScanningRepositories", selectedDir);

        var minDelay = Task.Delay(500);
        var rootDir = new DirectoryInfo(selectedDir);
        List<string> found = [];

        await GetUnmanagedRepositoriesAsync(rootDir, found, _managed, new EnumerationOptions()
        {
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
            IgnoreInaccessible = true,
        }, desc => ProgressDescription = desc);

        // ポップアップが一瞬で消えないよう最低0.5秒の待機を保証
        await minDelay;

        if (found.Count > 0)
            await AddFoundRepositories(rootDir, found);

        return true;
    }

    /// <summary>
    /// 既に管理されているリポジトリのIDセットを再帰的に収集する。
    /// </summary>
    private static void GetManagedRepositories(List<RepositoryNode> group, HashSet<string> repos)
    {
        foreach (var node in group)
        {
            if (node.IsRepository)
                repos.Add(node.Id);
            else
                GetManagedRepositories(node.SubNodes, repos);
        }
    }

    /// <summary>
    /// 未管理のGitリポジトリを再帰的に検出する。最大深度5まで探索する。
    /// 隠しディレクトリやnode_modulesはスキップする。
    /// </summary>
    private static async Task GetUnmanagedRepositoriesAsync(DirectoryInfo dir, List<string> outs, HashSet<string> managed, EnumerationOptions opts, Action<string> onProgress = null, int depth = 0)
    {
        var subdirs = dir.GetDirectories("*", opts);
        foreach (var subdir in subdirs)
        {
            if (subdir.Name.StartsWith(".", StringComparison.Ordinal) ||
                subdir.Name.Equals("node_modules", StringComparison.Ordinal))
                continue;

            onProgress?.Invoke($"Scanning {subdir.FullName}...");

            var normalizedSelf = subdir.FullName.Replace('\\', '/').TrimEnd('/');
            if (managed.Contains(normalizedSelf))
                continue;

            var gitDir = Path.Combine(subdir.FullName, ".git");
            if (Directory.Exists(gitDir) || File.Exists(gitDir))
            {
                var test = await new Commands.QueryRepositoryRootPath(subdir.FullName).GetResultAsync();
                if (test.IsSuccess && !string.IsNullOrEmpty(test.StdOut))
                {
                    var normalized = test.StdOut.Trim().Replace('\\', '/').TrimEnd('/');
                    if (!managed.Contains(normalized))
                        outs.Add(normalized);
                }

                continue;
            }

            var isBare = await new Commands.IsBareRepository(subdir.FullName).GetResultAsync();
            if (isBare)
            {
                outs.Add(normalizedSelf);
                continue;
            }

            if (depth < 5)
                await GetUnmanagedRepositoriesAsync(subdir, outs, managed, opts, onProgress, depth + 1);
        }
    }

    /// <summary>
    /// 発見されたリポジトリをツリーに追加する。ディレクトリ構造に基づいてグループを自動作成する。
    /// </summary>
    private static async Task AddFoundRepositories(DirectoryInfo rootDir, List<string> found)
    {
        var normalizedRoot = rootDir.FullName.Replace('\\', '/').TrimEnd('/');
        foreach (var f in found)
        {
            var parent = new DirectoryInfo(f).Parent!.FullName.Replace('\\', '/').TrimEnd('/');
            if (parent.Equals(normalizedRoot, StringComparison.Ordinal))
            {
                var node = Preferences.Instance.FindOrAddNodeByRepositoryPath(f, null, false, false);
                await node.UpdateStatusAsync(false, null);
            }
            else if (parent.StartsWith(normalizedRoot, StringComparison.Ordinal))
            {
                var relative = parent.Substring(normalizedRoot.Length).TrimStart('/');
                var group = FindOrCreateGroupRecursive(Preferences.Instance.RepositoryNodes, relative);
                var node = Preferences.Instance.FindOrAddNodeByRepositoryPath(f, group, false, false);
                await node.UpdateStatusAsync(false, null);
            }
        }

        Preferences.Instance.AutoRemoveInvalidNode();
        Preferences.Instance.Save();
        Welcome.Instance.Refresh();
    }

    /// <summary>
    /// パスに基づいてグループノードを再帰的に検索または作成する。
    /// </summary>
    private static RepositoryNode FindOrCreateGroupRecursive(List<RepositoryNode> collection, string path)
    {
        RepositoryNode node = null;
        foreach (var name in path.Split('/'))
        {
            node = FindOrCreateGroup(collection, name);
            collection = node.SubNodes;
        }

        return node;
    }

    /// <summary>
    /// 指定名のグループノードを検索し、存在しなければ新規作成する。
    /// </summary>
    private static RepositoryNode FindOrCreateGroup(List<RepositoryNode> collection, string name)
    {
        foreach (var node in collection)
        {
            if (node.Name.Equals(name, StringComparison.Ordinal))
                return node;
        }

        var added = new RepositoryNode()
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            IsRepository = false,
            IsExpanded = true,
        };
        collection.Add(added);

        Preferences.Instance.SortNodes(collection);
        return added;
    }

    private HashSet<string> _managed = new();
    private bool _useCustomDir = false;
    private string _customDir = string.Empty;
    private Models.ScanDir _selected = null;
}
