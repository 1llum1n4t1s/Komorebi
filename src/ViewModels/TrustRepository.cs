#nullable disable warnings
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

public class TrustRepository : Popup
{
    public string TargetPath
    {
        get;
    }

    public string SafeDirectory
    {
        get;
    }

    public string Command
    {
        get;
    }

    public string Reason
    {
        get;
    }

    public TrustRepository(string pageId, string reason, string safeDirectory, RepositoryNode parent, bool moveNode, bool open, int bookmark)
        : this(pageId, reason, safeDirectory, parent, moveNode, open, bookmark, false)
    {
    }

    /// <summary>
    /// 信頼の追加（git config --global --add safe.directory）だけを行うモード。
    /// エラー通知の「safe.directory に追加」から使う。既に開いているリポジトリ上の操作で起きたエラーなので、
    /// 通常モードのようなノード登録・Welcome 更新・Open は行わない（勝手にノードを増やしたりタブを開いたりしない）。
    /// </summary>
    public static TrustRepository CreateTrustOnly(string pageId, string reason, string safeDirectory)
    {
        return new TrustRepository(pageId, reason, safeDirectory, null, false, false, 0, true);
    }

    private TrustRepository(string pageId, string reason, string safeDirectory, RepositoryNode parent, bool moveNode, bool open, int bookmark, bool trustOnly)
    {
        _pageId = pageId;
        _trustOnly = trustOnly;
        _parent = parent;
        _moveNode = moveNode;
        _open = open;
        _bookmark = bookmark;

        TargetPath = safeDirectory.StartsWith("%(prefix)/", System.StringComparison.Ordinal) ? safeDirectory[10..] : safeDirectory;
        SafeDirectory = safeDirectory;
        Command = App.Text("TrustRepository.CommandTip", safeDirectory);
        Reason = GetReason(reason);
    }

    public override async Task<bool> Sure()
    {
        var log = new CommandLog("Trust Repository");
        Use(log);

        ProgressDescription = App.Text("Progress.TrustingRepository", SafeDirectory);
        using var cancellation = BeginCancellableOperation();
        var added = await new Commands.AddSafeDirectory(_pageId, SafeDirectory) { CancellationToken = cancellation.Token }.Use(log).ExecAsync();
        if (!added || cancellation.Token.IsCancellationRequested)
        {
            log.Complete();
            return false;
        }

        if (_trustOnly)
        {
            // 追加に成功したことだけを通知して閉じる（失敗時は AddSafeDirectory が RaiseError で通知済み）。
            log.Complete();
            App.SendNotification(_pageId, App.Text("GitError.DubiousOwnership.Added", SafeDirectory));
            return true;
        }

        ProgressDescription = App.Text("Progress.OpeningRepository", TargetPath);

        var root = TargetPath;
        var isBare = await new Commands.IsBareRepository(TargetPath) { CancellationToken = cancellation.Token }.GetResultAsync();
        if (cancellation.Token.IsCancellationRequested)
        {
            log.Complete();
            return false;
        }
        if (!isBare)
        {
            var test = await new Commands.QueryRepositoryRootPath(TargetPath) { CancellationToken = cancellation.Token }.GetResultAsync();
            if (cancellation.Token.IsCancellationRequested)
            {
                log.Complete();
                return false;
            }
            if (!test.IsSuccess || string.IsNullOrWhiteSpace(test.StdOut))
            {
                log.Complete();
                App.RaiseException(_pageId, string.IsNullOrWhiteSpace(test.StdErr) ? App.Text("Error.FailedToOpenTrustedRepository") : test.StdErr);
                return false;
            }

            root = test.StdOut.Trim();
        }

        log.Complete();

        if (cancellation.Token.IsCancellationRequested)
            return false;
        var normalizedRoot = root.Replace('\\', '/').TrimEnd('/');
        var existingNode = Preferences.Instance.FindNode(normalizedRoot);
        var node = Preferences.Instance.FindOrAddNodeByRepositoryPath(normalizedRoot, _parent, _moveNode);
        if (existingNode is null)
            node.Bookmark = _bookmark;
        await node.UpdateStatusAsync(false, cancellation.Token);
        if (cancellation.Token.IsCancellationRequested)
            return false;
        Welcome.Instance.Refresh();

        if (_open)
            node.Open();

        return true;
    }

    private static string GetReason(string stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr))
            return string.Empty;

        // Only the first line (e.g. "fatal: detected dubious ownership in repository at '...'") is shown.
        // The remaining lines only contain the owner SIDs and git's suggested `git config` command.
        var line = stderr.Trim();
        var idx = line.IndexOf('\n');
        if (idx >= 0)
            line = line.Substring(0, idx);

        return line.Trim();
    }

    private readonly string _pageId;
    private readonly bool _trustOnly;
    private readonly RepositoryNode _parent;
    private readonly bool _moveNode;
    private readonly bool _open;
    private readonly int _bookmark;
}
