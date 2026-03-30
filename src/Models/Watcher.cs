using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Komorebi.Models;

/// <summary>
/// FileSystemWatcherによるリポジトリ監視クラス。
/// ワーキングコピーとgitディレクトリの変更を検出し、
/// タイマーベースのデバウンスでブランチ・タグ・スタッシュ・サブモジュール・ワーキングコピーを更新する。
/// </summary>
public class Watcher : IDisposable
{
    /// <summary>
    /// 監視を一時停止するためのロックコンテキスト。
    /// usingパターンでスレッドセーフにロック・アンロックを行う。
    /// </summary>
    public class LockContext : IDisposable
    {
        /// <summary>
        /// ロックコンテキストを取得し、監視を一時停止する
        /// </summary>
        /// <param name="target">ロック対象のWatcher</param>
        public LockContext(Watcher target)
        {
            _target = target;
            Interlocked.Increment(ref _target._lockCount);
        }

        /// <summary>ロックを解放し、監視を再開する</summary>
        public void Dispose()
        {
            Interlocked.Decrement(ref _target._lockCount);
        }

        /// <summary>ロック対象のWatcherインスタンス</summary>
        private Watcher _target;
    }

    /// <summary>
    /// ファイル監視を初期化する。
    /// gitディレクトリがワーキングコピー内の場合は統合監視、
    /// 分離している場合（ワークツリー等）は個別の監視を設定する。
    /// </summary>
    /// <param name="repo">監視対象のリポジトリ</param>
    /// <param name="fullpath">リポジトリのルートパス</param>
    /// <param name="gitDir">gitディレクトリのパス</param>
    public Watcher(IRepository repo, string fullpath, string gitDir)
    {
        _repo = repo;
        _root = new DirectoryInfo(fullpath).FullName;
        _watchers = [];

        var testGitDir = new DirectoryInfo(Path.Combine(fullpath, ".git")).FullName;
        var desiredDir = new DirectoryInfo(gitDir).FullName;
        // gitディレクトリがワーキングコピー内にある場合は統合ウォッチャーを使用
        if (testGitDir.Equals(desiredDir, StringComparison.Ordinal))
        {
            var combined = new FileSystemWatcher();
            combined.Path = fullpath;
            combined.Filter = "*";
            combined.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.DirectoryName | NotifyFilters.FileName;
            combined.IncludeSubdirectories = true;
            combined.Created += OnRepositoryChanged;
            combined.Renamed += OnRepositoryChanged;
            combined.Changed += OnRepositoryChanged;
            combined.Deleted += OnRepositoryChanged;
            combined.EnableRaisingEvents = false;

            _watchers.Add(combined);
        }
        // gitディレクトリが分離している場合（ワークツリー等）は個別のウォッチャーを使用
        else
        {
            var wc = new FileSystemWatcher();
            wc.Path = fullpath;
            wc.Filter = "*";
            wc.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.DirectoryName | NotifyFilters.FileName;
            wc.IncludeSubdirectories = true;
            wc.Created += OnWorkingCopyChanged;
            wc.Renamed += OnWorkingCopyChanged;
            wc.Changed += OnWorkingCopyChanged;
            wc.Deleted += OnWorkingCopyChanged;
            wc.EnableRaisingEvents = false;

            var git = new FileSystemWatcher();
            git.Path = gitDir;
            git.Filter = "*";
            git.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.DirectoryName | NotifyFilters.FileName;
            git.IncludeSubdirectories = true;
            git.Created += OnGitDirChanged;
            git.Renamed += OnGitDirChanged;
            git.Changed += OnGitDirChanged;
            git.Deleted += OnGitDirChanged;
            git.EnableRaisingEvents = false;

            _watchers.Add(wc);
            _watchers.Add(git);
        }

        _timer = new Timer(Tick, null, 100, 100);

        // UIブロッキングを避けるため、別スレッドでファイルシステム監視を開始
        Task.Run(() =>
        {
            try
            {
                foreach (var watcher in _watchers)
                    watcher.EnableRaisingEvents = true;
            }
            catch
            {
                // 例外を無視（Dispose呼び出し中に発生する可能性がある）
            }
        });
    }

    /// <summary>監視を一時停止するロックを取得する</summary>
    public IDisposable Lock()
    {
        return new LockContext(this);
    }

    /// <summary>ブランチ更新の処理済みマークを付ける（ワーキングコピーも含む）</summary>
    public void MarkBranchUpdated()
    {
        Interlocked.Exchange(ref _updateBranch, 0);
        Interlocked.Exchange(ref _updateWC, 0);
    }

    /// <summary>タグ更新の処理済みマークを付ける</summary>
    public void MarkTagUpdated()
    {
        Interlocked.Exchange(ref _updateTags, 0);
    }

    /// <summary>ワーキングコピー更新の処理済みマークを付ける</summary>
    public void MarkWorkingCopyUpdated()
    {
        Interlocked.Exchange(ref _updateWC, 0);
    }

    /// <summary>スタッシュ更新の処理済みマークを付ける</summary>
    public void MarkStashUpdated()
    {
        Interlocked.Exchange(ref _updateStashes, 0);
    }

    /// <summary>サブモジュール更新の処理済みマークを付ける</summary>
    public void MarkSubmodulesUpdated()
    {
        Interlocked.Exchange(ref _updateSubmodules, 0);
    }

    /// <summary>ファイル監視とタイマーを停止・破棄する</summary>
    public void Dispose()
    {
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        _watchers.Clear();
        _timer.Dispose();
        _timer = null;
    }

    /// <summary>
    /// タイマーのティック処理。デバウンスされた更新要求を確認し、
    /// 期限が到来したものについてリポジトリの各要素を更新する。
    /// </summary>
    private void Tick(object sender)
    {
        // ロック中は更新をスキップ
        if (Interlocked.Read(ref _lockCount) > 0)
            return;

        var now = DateTime.Now.ToFileTime();
        var refreshCommits = false;
        var refreshSubmodules = false;
        var refreshWC = false;

        var oldUpdateBranch = Interlocked.Exchange(ref _updateBranch, -1);
        if (oldUpdateBranch > 0)
        {
            if (now > oldUpdateBranch)
            {
                refreshCommits = true;
                refreshSubmodules = _repo.MayHaveSubmodules();
                refreshWC = true;

                _repo.RefreshBranches();
                _repo.RefreshWorktrees();
            }
            else
            {
                Interlocked.CompareExchange(ref _updateBranch, oldUpdateBranch, -1);
            }
        }

        if (refreshWC)
        {
            Interlocked.Exchange(ref _updateWC, -1);
            _repo.RefreshWorkingCopyChanges();
        }
        else
        {
            var oldUpdateWC = Interlocked.Exchange(ref _updateWC, -1);
            if (oldUpdateWC > 0)
            {
                if (now > oldUpdateWC)
                    _repo.RefreshWorkingCopyChanges();
                else
                    Interlocked.CompareExchange(ref _updateWC, oldUpdateWC, -1);
            }
        }

        if (refreshSubmodules)
        {
            Interlocked.Exchange(ref _updateSubmodules, -1);
            _repo.RefreshSubmodules();
        }
        else
        {
            var oldUpdateSubmodule = Interlocked.Exchange(ref _updateSubmodules, -1);
            if (oldUpdateSubmodule > 0)
            {
                if (now > oldUpdateSubmodule)
                    _repo.RefreshSubmodules();
                else
                    Interlocked.CompareExchange(ref _updateSubmodules, oldUpdateSubmodule, -1);
            }
        }

        var oldUpdateStashes = Interlocked.Exchange(ref _updateStashes, -1);
        if (oldUpdateStashes > 0)
        {
            if (now > oldUpdateStashes)
                _repo.RefreshStashes();
            else
                Interlocked.CompareExchange(ref _updateStashes, oldUpdateStashes, -1);
        }

        var oldUpdateTags = Interlocked.Exchange(ref _updateTags, -1);
        if (oldUpdateTags > 0)
        {
            if (now > oldUpdateTags)
            {
                refreshCommits = true;
                _repo.RefreshTags();
            }
            else
            {
                Interlocked.CompareExchange(ref _updateTags, oldUpdateTags, -1);
            }
        }

        if (refreshCommits)
            _repo.RefreshCommits();
    }

    /// <summary>
    /// 統合ウォッチャーのイベントハンドラー。
    /// .gitディレクトリ内の変更とワーキングコピーの変更を振り分ける。
    /// </summary>
    private void OnRepositoryChanged(object o, FileSystemEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Name) || e.Name.Equals(".git", StringComparison.Ordinal))
            return;

        var name = e.Name.Replace('\\', '/').TrimEnd('/');
        if (name.EndsWith("/.git", StringComparison.Ordinal))
            return;

        if (name.StartsWith(".git/", StringComparison.Ordinal))
            HandleGitDirFileChanged(name[5..]);
        else
            HandleWorkingCopyFileChanged(name, e.FullPath);
    }

    /// <summary>gitディレクトリのファイル変更イベントハンドラー（分離ウォッチャー用）</summary>
    private void OnGitDirChanged(object o, FileSystemEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Name))
            return;

        var name = e.Name.Replace('\\', '/').TrimEnd('/');
        HandleGitDirFileChanged(name);
    }

    /// <summary>ワーキングコピーのファイル変更イベントハンドラー（分離ウォッチャー用）</summary>
    private void OnWorkingCopyChanged(object o, FileSystemEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Name))
            return;

        var name = e.Name.Replace('\\', '/').TrimEnd('/');
        if (name.Equals(".git", StringComparison.Ordinal) ||
            name.StartsWith(".git/", StringComparison.Ordinal) ||
            name.EndsWith("/.git", StringComparison.Ordinal))
            return;

        HandleWorkingCopyFileChanged(name, e.FullPath);
    }

    /// <summary>
    /// gitディレクトリ内のファイル変更を処理する。
    /// 変更されたファイルのパスに応じて、適切な更新フラグを設定する。
    /// HEAD、refs/heads/*, refs/tags/*, refs/stash, modules/*, reftable/*等を監視する。
    /// </summary>
    /// <param name="name">gitディレクトリからの相対パス</param>
    private void HandleGitDirFileChanged(string name)
    {
        if (name.Contains("fsmonitor--daemon/", StringComparison.Ordinal) ||
            name.EndsWith(".lock", StringComparison.Ordinal) ||
            name.StartsWith("lfs/", StringComparison.Ordinal))
            return;

        if (name.StartsWith("modules", StringComparison.Ordinal))
        {
            if (name.EndsWith("/HEAD", StringComparison.Ordinal) ||
                name.EndsWith("/ORIG_HEAD", StringComparison.Ordinal))
            {
                var desired = DateTime.Now.AddSeconds(1).ToFileTime();
                Interlocked.Exchange(ref _updateSubmodules, desired);
                Interlocked.Exchange(ref _updateWC, desired);
            }
        }
        else if (name.Equals("MERGE_HEAD", StringComparison.Ordinal) ||
            name.Equals("AUTO_MERGE", StringComparison.Ordinal))
        {
            if (_repo.MayHaveSubmodules())
                Interlocked.Exchange(ref _updateSubmodules, DateTime.Now.AddSeconds(1).ToFileTime());
        }
        else if (name.StartsWith("refs/tags", StringComparison.Ordinal))
        {
            Interlocked.Exchange(ref _updateTags, DateTime.Now.AddSeconds(.5).ToFileTime());
        }
        else if (name.StartsWith("refs/stash", StringComparison.Ordinal))
        {
            Interlocked.Exchange(ref _updateStashes, DateTime.Now.AddSeconds(.5).ToFileTime());
        }
        else if (name.Equals("HEAD", StringComparison.Ordinal) ||
            name.Equals("BISECT_START", StringComparison.Ordinal) ||
            name.StartsWith("refs/heads/", StringComparison.Ordinal) ||
            name.StartsWith("refs/remotes/", StringComparison.Ordinal) ||
            (name.StartsWith("worktrees/", StringComparison.Ordinal) && name.EndsWith("/HEAD", StringComparison.Ordinal)))
        {
            Interlocked.Exchange(ref _updateBranch, DateTime.Now.AddSeconds(.5).ToFileTime());
        }
        else if (name.StartsWith("reftable/", StringComparison.Ordinal))
        {
            var desired = DateTime.Now.AddSeconds(.5).ToFileTime();
            Interlocked.Exchange(ref _updateBranch, desired);
            Interlocked.Exchange(ref _updateTags, desired);
            Interlocked.Exchange(ref _updateStashes, desired);
        }
        else if (name.StartsWith("objects/", StringComparison.Ordinal) || name.Equals("index", StringComparison.Ordinal))
        {
            Interlocked.Exchange(ref _updateWC, DateTime.Now.AddSeconds(1).ToFileTime());
        }
    }

    /// <summary>
    /// ワーキングコピー内のファイル変更を処理する。
    /// .gitmodulesの変更はサブモジュール更新をトリガーし、
    /// サブモジュール内の変更はサブモジュール更新のみ行う。
    /// </summary>
    /// <param name="name">リポジトリルートからの相対パス</param>
    /// <param name="fullpath">ファイルの絶対パス</param>
    private void HandleWorkingCopyFileChanged(string name, string fullpath)
    {
        if (name.StartsWith(".vs/", StringComparison.Ordinal))
            return;

        if (name.Equals(".gitmodules", StringComparison.Ordinal))
        {
            var desired = DateTime.Now.AddSeconds(1).ToFileTime();
            Interlocked.Exchange(ref _updateSubmodules, desired);
            Interlocked.Exchange(ref _updateWC, desired);
            return;
        }

        var dir = Directory.Exists(fullpath) ? fullpath : Path.GetDirectoryName(fullpath);
        if (IsInSubmodule(dir))
        {
            Interlocked.Exchange(ref _updateSubmodules, DateTime.Now.AddSeconds(1).ToFileTime());
            return;
        }

        Interlocked.Exchange(ref _updateWC, DateTime.Now.AddSeconds(1).ToFileTime());
    }

    /// <summary>
    /// 指定フォルダがサブモジュール内にあるかどうかを再帰的に判定する。
    /// .gitファイル（ディレクトリではなくファイル）の存在でサブモジュールを検出する。
    /// </summary>
    /// <param name="folder">判定対象のフォルダパス</param>
    /// <returns>サブモジュール内の場合true</returns>
    private bool IsInSubmodule(string folder)
    {
        if (string.IsNullOrEmpty(folder) || folder.Equals(_root, StringComparison.Ordinal))
            return false;

        if (File.Exists($"{folder}/.git"))
            return true;

        return IsInSubmodule(Path.GetDirectoryName(folder));
    }

    /// <summary>監視対象のリポジトリ</summary>
    private readonly IRepository _repo;
    /// <summary>リポジトリのルートパス</summary>
    private readonly string _root;
    /// <summary>FileSystemWatcherのリスト</summary>
    private List<FileSystemWatcher> _watchers;
    /// <summary>デバウンス用のタイマー（100msごとにTickを呼び出す）</summary>
    private Timer _timer;

    /// <summary>ロックカウント（0より大きい場合は更新を抑制）</summary>
    private long _lockCount;
    /// <summary>ワーキングコピー更新の予定時刻（FileTime形式）</summary>
    private long _updateWC;
    /// <summary>ブランチ更新の予定時刻（FileTime形式）</summary>
    private long _updateBranch;
    /// <summary>サブモジュール更新の予定時刻（FileTime形式）</summary>
    private long _updateSubmodules;
    /// <summary>スタッシュ更新の予定時刻（FileTime形式）</summary>
    private long _updateStashes;
    /// <summary>タグ更新の予定時刻（FileTime形式）</summary>
    private long _updateTags;
}
