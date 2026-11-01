// nullable 移行未実施。1 ファイルずつ null 注釈を入れてこの 2 行を削除していく。
#nullable disable warnings
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>
/// コミット検索機能のコンテキストを管理するViewModel。
/// SHA、メッセージ、作者、ファイルパスなど複数の検索メソッドをサポートし、
/// 検索結果のリストと選択状態を管理する。
/// </summary>
public class SearchCommitContext : ObservableObject, IDisposable
{
    /// <summary>
    /// 検索メソッド（SHA、メッセージ、作者、ファイルパスなど）のインデックス。
    /// </summary>
    public int Method
    {
        get => _method;
        set
        {
            if (SetProperty(ref _method, value))
            {
                UpdateSuggestions();
                if (_repo?.IsSearchingCommits == true)
                    StartSearch();
            }
        }
    }

    /// <summary>
    /// 検索フィルタ文字列。変更時にサジェストを更新する。
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
    /// 現在のブランチのみを検索対象にするかどうか。
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
    /// 検索方法に応じたオートコンプリート候補リスト。
    /// ファイルパス検索時は <see cref="string"/>、作者/コミッター検索時は <see cref="Models.User"/> を格納する。
    /// </summary>
    public List<object> Suggestions
    {
        get => _suggestions;
        private set => SetProperty(ref _suggestions, value);
    }

    /// <summary>
    /// 検索クエリの実行中かどうか。
    /// </summary>
    public bool IsQuerying
    {
        get => _isQuerying;
        private set => SetProperty(ref _isQuerying, value);
    }

    /// <summary>
    /// 検索結果のコミットリスト。setter で SHA→Commit の lookup 辞書も同期更新する。
    /// </summary>
    public List<Models.Commit> Results
    {
        get => _results;
        private set
        {
            if (SetProperty(ref _results, value))
            {
                _resultsBySha.Clear();
                if (value is not null)
                {
                    foreach (var c in value)
                        _resultsBySha[c.SHA] = c;
                }
            }
        }
    }

    /// <summary>
    /// 検索結果から指定 SHA のコミットを O(1) で取得する。見つからなければ null。
    /// Histories の選択コミット変更時に O(n) Find を回避するためのアクセサ。
    /// </summary>
    public Models.Commit FindResultBySha(string sha)
    {
        return _resultsBySha.TryGetValue(sha, out var commit) ? commit : null;
    }

    /// <summary>
    /// 選択されたコミット。選択時に履歴画面をそのコミットにナビゲートする。
    /// </summary>
    public Models.Commit Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value) && value is not null)
                _repo?.NavigateToCommit(value.SHA);
        }
    }

    /// <summary>
    /// コンストラクタ。対象リポジトリを設定する。
    /// </summary>
    public SearchCommitContext(Repository repo)
        : this(repo, QueryCommitsAsync, action => Dispatcher.UIThread.Post(action),
            path => new Commands.QueryUsers(path).GetResultAsync(),
            path => new Commands.QueryRevisionFileNames(path, "HEAD").GetResultAsync())
    {
    }

    internal SearchCommitContext(Repository repo,
        Func<string, string, Models.CommitSearchMethod, bool, Task<List<Models.Commit>>> query,
        Action<Action> post, Func<string, Task<List<Models.User>>> queryUsers,
        Func<string, Task<List<string>>> queryFiles)
    {
        _repo = repo;
        _query = query;
        _post = post;
        _queryUsers = queryUsers;
        _queryFiles = queryFiles;
    }

    /// <summary>
    /// リソースを解放する。
    /// </summary>
    public void Dispose()
    {
        InvalidateRequests();
        _repo = null;
        _suggestions?.Clear();
        _results?.Clear();
        _resultsBySha.Clear();
        _worktreeFiles?.Clear();
        _users = null;
        _pendingUsers = null;
        _pendingFiles = null;
    }

    /// <summary>
    /// 検索フィルタと結果をクリアする。
    /// </summary>
    public void ClearFilter()
    {
        InvalidateRequests();
        Filter = string.Empty;
        Selected = null;
        Results = null;
    }

    /// <summary>
    /// サジェスト候補をクリアする。
    /// </summary>
    public void ClearSuggestions()
    {
        ++_suggestionGeneration;
        Suggestions = null;
    }

    /// <summary>
    /// 検索を開始する。バックグラウンドスレッドでgitコマンドを実行し、結果をUIに反映する。
    /// </summary>
    public void StartSearch()
    {
        _ = StartSearchAsync();
    }

    // Komorebi: UI スレッドで入力と所有者を固定し、遅延結果は同じ要求だけへ反映する。
    internal Task StartSearchAsync()
    {
        var repo = _repo;
        if (repo is null || repo.IsClosed)
            return Task.CompletedTask;

        var generation = ++_searchGeneration;
        ++_suggestionGeneration;
        var filter = _filter;
        var method = (Models.CommitSearchMethod)_method;
        var onlyCurrentBranch = _onlySearchCurrentBranch;
        var repoPath = repo.FullPath;
        Results = null;
        Selected = null;
        Suggestions = null;
        IsQuerying = !string.IsNullOrEmpty(filter);
        if (!IsQuerying)
            return Task.CompletedTask;

        return Task.Run(async () =>
        {
            List<Models.Commit> result = null;
            try
            {
                result = await _query(repoPath, filter, method, onlyCurrentBranch).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Models.Logger.LogException($"SearchCommits失敗: {repoPath}", ex);
            }
            finally
            {
                _post(() =>
                {
                    if (_repo != repo || repo.IsClosed || generation != _searchGeneration)
                        return;

                    IsQuerying = false;
                    if (repo.IsSearchingCommits && result is not null)
                    {
                        Results = result;
                        if (method == Models.CommitSearchMethod.BySHA && result.Count == 1)
                            Selected = result[0];
                    }
                });
            }
        });
    }

    private static async Task<List<Models.Commit>> QueryCommitsAsync(string repoPath, string filter,
        Models.CommitSearchMethod method, bool onlyCurrentBranch)
    {
        List<Models.Commit> result = [];

        if (method == Models.CommitSearchMethod.BySHA)
        {
            var isCommitSHA = await new Commands.IsCommitSHA(repoPath, filter)
                .GetResultAsync()
                .ConfigureAwait(false);

            if (isCommitSHA)
            {
                // コミット情報取得と祖先チェックを並列実行する
                var commitTask = new Commands.QuerySingleCommit(repoPath, filter).GetResultAsync();
                var ancestorTask = new Commands.IsAncestor(repoPath, filter, "HEAD").GetResultAsync();
                await Task.WhenAll(commitTask, ancestorTask).ConfigureAwait(false);

                var commit = commitTask.Result;
                if (commit is not null)
                {
                    commit.IsMerged = ancestorTask.Result;
                    result.Add(commit);
                }
            }
        }
        else if (onlyCurrentBranch)
        {
            result = await new Commands.QueryCommits(repoPath, filter, method, true)
                .GetResultAsync()
                .ConfigureAwait(false);

            foreach (var c in result)
                c.IsMerged = true;
        }
        else
        {
            result = await new Commands.QueryCommits(repoPath, filter, method, false)
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

        return result;
    }

    private void InvalidateRequests()
    {
        ++_searchGeneration;
        ++_suggestionGeneration;
        IsQuerying = false;
    }

    /// <summary>
    /// 検索を終了してリソースを解放する。
    /// </summary>
    public void EndSearch()
    {
        InvalidateRequests();
        _worktreeFiles = null;
        _pendingFiles = null;
        Suggestions = null;
        Results = null;
    }

    /// <summary>
    /// 検索方法に応じてサジェスト候補を更新する。
    /// 作者検索時はリポジトリの全ユーザー一覧を遅延取得して名前・メールに部分一致するものを提示し、
    /// ファイルパス検索時はワークツリーのファイル一覧を遅延取得してフィルタにマッチするファイルを提示する。
    /// </summary>
    private void UpdateSuggestions()
    {
        var repo = _repo;
        if (repo is null || repo.IsClosed)
            return;

        var generation = ++_suggestionGeneration;
        var repoPath = repo.FullPath;
        Suggestions = null;
        if (_method == (int)Models.CommitSearchMethod.ByAuthor)
        {
            if (_users is null)
            {
                var pending = _pendingUsers ??= Task.Run(() => _queryUsers(repoPath));
                Task.Run(async () =>
                {
                    var users = await pending
                        .ConfigureAwait(false);

                    _post(() =>
                    {
                        if (_repo == repo && !repo.IsClosed && repo.IsSearchingCommits && generation == _suggestionGeneration)
                        {
                            _users = users;
                            UpdateSuggestions();
                        }
                    });
                });

                return;
            }

            if (_users.Count == 0 || _filter.Length < 2)
            {
                Suggestions = null;
                return;
            }

            List<object> matchedUsers = [];
            foreach (var user in _users)
            {
                if (user.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase) ||
                    user.Email.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                    matchedUsers.Add(user);
            }

            Suggestions = matchedUsers;
        }
        else if (_method == (int)Models.CommitSearchMethod.ByPath)
        {
            if (_worktreeFiles is null)
            {
                var pending = _pendingFiles ??= Task.Run(() => _queryFiles(repoPath));
                Task.Run(async () =>
                {
                    var files = await pending
                        .ConfigureAwait(false);

                    _post(() =>
                    {
                        if (_repo == repo && !repo.IsClosed && repo.IsSearchingCommits && generation == _suggestionGeneration)
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

            List<object> matchedFiles = [];
            foreach (var file in _worktreeFiles)
            {
                if (file.Contains(_filter, StringComparison.OrdinalIgnoreCase) && file.Length != _filter.Length)
                {
                    matchedFiles.Add(file);
                    if (matchedFiles.Count > 100)
                        break;
                }
            }

            Suggestions = matchedFiles;
        }
        else
        {
            Suggestions = null;
        }
    }

    private readonly Func<string, string, Models.CommitSearchMethod, bool, Task<List<Models.Commit>>> _query;
    private readonly Action<Action> _post;
    private readonly Func<string, Task<List<Models.User>>> _queryUsers;
    private readonly Func<string, Task<List<string>>> _queryFiles;
    private Task<List<Models.User>> _pendingUsers;
    private Task<List<string>> _pendingFiles;
    private long _searchGeneration;
    private long _suggestionGeneration;
    private Repository _repo = null;
    private int _method = (int)Models.CommitSearchMethod.ByMessage;
    private string _filter = string.Empty;
    private bool _onlySearchCurrentBranch = false;
    private List<object> _suggestions = null;
    private bool _isQuerying = false;
    private List<Models.Commit> _results = null;
    private readonly Dictionary<string, Models.Commit> _resultsBySha = new(StringComparer.Ordinal);
    private Models.Commit _selected = null;
    private List<string> _worktreeFiles = null;
    private List<Models.User> _users = null;
}
