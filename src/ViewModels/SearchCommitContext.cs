using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>
///     コミット検索機能のコンテキストを管理するViewModel。
///     SHA、メッセージ、作者、ファイルパスなど複数の検索メソッドをサポートし、
///     検索結果のリストと選択状態を管理する。
/// </summary>
public class SearchCommitContext : ObservableObject, IDisposable
{
    /// <summary>
    ///     検索メソッド（SHA、メッセージ、作者、ファイルパスなど）のインデックス。
    /// </summary>
    public int Method
    {
        get => _method;
        set
        {
            if (SetProperty(ref _method, value))
            {
                UpdateSuggestions();
                StartSearch();
            }
        }
    }

    /// <summary>
    ///     検索フィルタ文字列。変更時にサジェストを更新する。
    /// </summary>
    public string Filter
    {
        get => _filter;
        set
        {
            if (SetProperty(ref _filter, value))
                UpdateSuggestions();
        }
    }

    /// <summary>
    ///     現在のブランチのみを検索対象にするかどうか。
    /// </summary>
    public bool OnlySearchCurrentBranch
    {
        get => _onlySearchCurrentBranch;
        set
        {
            if (SetProperty(ref _onlySearchCurrentBranch, value))
                StartSearch();
        }
    }

    /// <summary>
    ///     ファイルパス検索時のオートコンプリート候補リスト。
    /// </summary>
    public List<string> Suggestions
    {
        get => _suggestions;
        private set => SetProperty(ref _suggestions, value);
    }

    /// <summary>
    ///     検索クエリの実行中かどうか。
    /// </summary>
    public bool IsQuerying
    {
        get => _isQuerying;
        private set => SetProperty(ref _isQuerying, value);
    }

    /// <summary>
    ///     検索結果のコミットリスト。
    /// </summary>
    public List<Models.Commit> Results
    {
        get => _results;
        private set => SetProperty(ref _results, value);
    }

    /// <summary>
    ///     選択されたコミット。選択時に履歴画面をそのコミットにナビゲートする。
    /// </summary>
    public Models.Commit Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value) && value is not null)
                _repo.NavigateToCommit(value.SHA);
        }
    }

    /// <summary>
    ///     コンストラクタ。対象リポジトリを設定する。
    /// </summary>
    public SearchCommitContext(Repository repo)
    {
        _repo = repo;
    }

    /// <summary>
    ///     リソースを解放する。
    /// </summary>
    public void Dispose()
    {
        _repo = null;
        _suggestions?.Clear();
        _results?.Clear();
        _worktreeFiles?.Clear();
    }

    /// <summary>
    ///     検索フィルタと結果をクリアする。
    /// </summary>
    public void ClearFilter()
    {
        Filter = string.Empty;
        Selected = null;
        Results = null;
    }

    /// <summary>
    ///     サジェスト候補をクリアする。
    /// </summary>
    public void ClearSuggestions()
    {
        Suggestions = null;
    }

    /// <summary>
    ///     検索を開始する。バックグラウンドスレッドでgitコマンドを実行し、結果をUIに反映する。
    /// </summary>
    public void StartSearch()
    {
        Results = null;
        Selected = null;
        Suggestions = null;

        if (string.IsNullOrEmpty(_filter))
            return;

        IsQuerying = true;

        Task.Run(async () =>
        {
            var result = new List<Models.Commit>();
            var method = (Models.CommitSearchMethod)_method;
            var repoPath = _repo.FullPath;

            if (method == Models.CommitSearchMethod.BySHA)
            {
                var isCommitSHA = await new Commands.IsCommitSHA(repoPath, _filter)
                    .GetResultAsync()
                    .ConfigureAwait(false);

                if (isCommitSHA)
                {
                    var commit = await new Commands.QuerySingleCommit(repoPath, _filter)
                        .GetResultAsync()
                        .ConfigureAwait(false);

                    commit.IsMerged = await new Commands.IsAncestor(repoPath, commit.SHA, "HEAD")
                        .GetResultAsync()
                        .ConfigureAwait(false);

                    result.Add(commit);
                }
            }
            else if (_onlySearchCurrentBranch)
            {
                result = await new Commands.QueryCommits(repoPath, _filter, method, true)
                    .GetResultAsync()
                    .ConfigureAwait(false);

                foreach (var c in result)
                    c.IsMerged = true;
            }
            else
            {
                result = await new Commands.QueryCommits(repoPath, _filter, method, false)
                    .GetResultAsync()
                    .ConfigureAwait(false);

                if (result.Count > 0)
                {
                    var set = await new Commands.QueryCurrentBranchCommitHashes(repoPath, result[^1].CommitterTime)
                        .GetResultAsync()
                        .ConfigureAwait(false);

                    foreach (var c in result)
                        c.IsMerged = set.Contains(c.SHA);
                }
            }

            Dispatcher.UIThread.Post(() =>
            {
                IsQuerying = false;

                if (_repo.IsSearchingCommits)
                    Results = result;
            });
        });
    }

    /// <summary>
    ///     検索を終了してリソースを解放する。
    /// </summary>
    public void EndSearch()
    {
        _worktreeFiles = null;
        Suggestions = null;
        Results = null;
        GC.Collect();
    }

    /// <summary>
    ///     ファイルパス検索時のサジェスト候補を更新する。
    ///     ワークツリーのファイル一覧を遅延取得し、フィルタにマッチするファイルを提示する。
    /// </summary>
    private void UpdateSuggestions()
    {
        if (_method != (int)Models.CommitSearchMethod.ByPath || _requestingWorktreeFiles)
        {
            Suggestions = null;
            return;
        }

        if (_worktreeFiles is null)
        {
            _requestingWorktreeFiles = true;

            Task.Run(async () =>
            {
                var files = await new Commands.QueryRevisionFileNames(_repo.FullPath, "HEAD")
                    .GetResultAsync()
                    .ConfigureAwait(false);

                Dispatcher.UIThread.Post(() =>
                {
                    _requestingWorktreeFiles = false;

                    if (_repo.IsSearchingCommits)
                    {
                        _worktreeFiles = files;
                        UpdateSuggestions();
                    }
                });
            });

            return;
        }

        if (_worktreeFiles.Count == 0 || _filter.Length < 3)
        {
            Suggestions = null;
            return;
        }

        var matched = new List<string>();
        foreach (var file in _worktreeFiles)
        {
            if (file.Contains(_filter, StringComparison.OrdinalIgnoreCase) && file.Length != _filter.Length)
            {
                matched.Add(file);
                if (matched.Count > 100)
                    break;
            }
        }

        Suggestions = matched;
    }

    private Repository _repo = null;
    private int _method = (int)Models.CommitSearchMethod.ByMessage;
    private string _filter = string.Empty;
    private bool _onlySearchCurrentBranch = false;
    private List<string> _suggestions = null;
    private bool _isQuerying = false;
    private List<Models.Commit> _results = null;
    private Models.Commit _selected = null;
    private bool _requestingWorktreeFiles = false;
    private List<string> _worktreeFiles = null;
}
