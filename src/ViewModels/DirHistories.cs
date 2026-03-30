using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>
/// ディレクトリの履歴（コミット一覧）を表示するためのViewModel。
/// 指定ディレクトリに関連するコミットを取得し、コミット詳細を表示する。
/// </summary>
public class DirHistories : ObservableObject
{
    /// <summary>
    /// 表示タイトル（ディレクトリパスとオプションのリビジョン）。
    /// </summary>
    public string Title
    {
        get;
    }

    /// <summary>
    /// コミット履歴を読み込み中かどうか。
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    /// <summary>
    /// 取得されたコミットのリスト。
    /// </summary>
    public List<Models.Commit> Commits
    {
        get => _commits;
        private set => SetProperty(ref _commits, value);
    }

    /// <summary>
    /// 選択されたコミット。変更時にコミット詳細を更新する。
    /// </summary>
    public Models.Commit SelectedCommit
    {
        get => _selectedCommit;
        set
        {
            if (SetProperty(ref _selectedCommit, value))
                Detail.Commit = value;
        }
    }

    /// <summary>
    /// 選択コミットの詳細情報ViewModel。
    /// </summary>
    public CommitDetail Detail
    {
        get => _detail;
    }

    /// <summary>
    /// コンストラクタ。非同期でディレクトリに関連するコミット一覧を取得する。
    /// </summary>
    public DirHistories(Repository repo, string dir, string revision = null)
    {
        if (!string.IsNullOrEmpty(revision))
            Title = $"{dir} @ {revision}";
        else
            Title = dir;

        _repo = repo;
        _detail = new CommitDetail(repo, null);
        _detail.SearchChangeFilter = dir;

        Task.Run(async () =>
        {
            var argsBuilder = new StringBuilder();
            argsBuilder
                .Append("--date-order -n 10000 ")
                .Append(revision ?? string.Empty)
                .Append(" -- ")
                .Append(dir.Quoted());

            var commits = await new Commands.QueryCommits(_repo.FullPath, argsBuilder.ToString(), false)
                .GetResultAsync()
                .ConfigureAwait(false);

            Dispatcher.UIThread.Post(() =>
            {
                Commits = commits;
                IsLoading = false;

                if (commits.Count > 0)
                    SelectedCommit = commits[0];
            });
        });
    }

    /// <summary>
    /// メインのリポジトリビューで指定コミットへナビゲートする。
    /// </summary>
    public void NavigateToCommit(Models.Commit commit)
    {
        _repo.NavigateToCommit(commit.SHA);
    }

    /// <summary>
    /// コミットの完全なメッセージを取得する（キャッシュ付き）。
    /// </summary>
    public string GetCommitFullMessage(Models.Commit commit)
    {
        var sha = commit.SHA;
        if (_cachedCommitFullMessage.TryGetValue(sha, out var msg))
            return msg;

        msg = new Commands.QueryCommitFullMessage(_repo.FullPath, sha).GetResult();
        _cachedCommitFullMessage[sha] = msg;
        return msg;
    }

    private Repository _repo = null;
    private bool _isLoading = true;
    private List<Models.Commit> _commits = [];
    private Models.Commit _selectedCommit = null;
    private CommitDetail _detail = null;
    private Dictionary<string, string> _cachedCommitFullMessage = new();
}
