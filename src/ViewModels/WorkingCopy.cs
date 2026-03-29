using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>
/// ワーキングコピー（作業ディレクトリ）のViewModel。
/// ステージング、アンステージング、コミット、コンフリクト解決、マージ操作を管理する。
/// </summary>
public class WorkingCopy : ObservableObject, IDisposable
{
    /// <summary>
    /// 対応するリポジトリViewModel。
    /// </summary>
    public Repository Repository
    {
        get => _repo;
    }

    /// <summary>
    /// 未追跡ファイルを変更一覧に含めるかどうか。
    /// </summary>
    public bool IncludeUntracked
    {
        get => _repo.IncludeUntracked;
        set
        {
            if (_repo.IncludeUntracked != value)
            {
                _repo.IncludeUntracked = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// リモートが存在するかどうか。
    /// </summary>
    public bool HasRemotes
    {
        get => _hasRemotes;
        set => SetProperty(ref _hasRemotes, value);
    }

    /// <summary>
    /// 未解決のコンフリクトが存在するかどうか。
    /// </summary>
    public bool HasUnsolvedConflicts
    {
        get => _hasUnsolvedConflicts;
        set => SetProperty(ref _hasUnsolvedConflicts, value);
    }

    /// <summary>
    /// 進行中の操作コンテキスト（チェリーピック、リベース、リバート、マージ）。
    /// </summary>
    public InProgressContext InProgressContext
    {
        get => _inProgressContext;
        private set => SetProperty(ref _inProgressContext, value);
    }

    /// <summary>
    /// ステージング操作が実行中かどうか。
    /// </summary>
    public bool IsStaging
    {
        get => _isStaging;
        private set => SetProperty(ref _isStaging, value);
    }

    /// <summary>
    /// アンステージング操作が実行中かどうか。
    /// </summary>
    public bool IsUnstaging
    {
        get => _isUnstaging;
        private set => SetProperty(ref _isUnstaging, value);
    }

    /// <summary>
    /// コミット操作が実行中かどうか。
    /// </summary>
    public bool IsCommitting
    {
        get => _isCommitting;
        private set => SetProperty(ref _isCommitting, value);
    }

    /// <summary>
    /// Signed-off-byをコミットに付加するかどうか。
    /// </summary>
    public bool EnableSignOff
    {
        get => _repo.UIStates.EnableSignOffForCommit;
        set => _repo.UIStates.EnableSignOffForCommit = value;
    }

    /// <summary>
    /// pre-commitフックの検証をスキップするかどうか（--no-verify）。
    /// </summary>
    public bool NoVerifyOnCommit
    {
        get => _repo.UIStates.NoVerifyOnCommit;
        set => _repo.UIStates.NoVerifyOnCommit = value;
    }

    /// <summary>
    /// 直前のコミットを修正（amend）するかどうか。
    /// 有効化時にHEADのコミットメッセージを読み込み、無効化時にメッセージをクリアする。
    /// </summary>
    public bool UseAmend
    {
        get => _useAmend;
        set
        {
            if (SetProperty(ref _useAmend, value))
            {
                if (value)
                {
                    // amend有効化: HEADコミットのメッセージを取得
                    var currentBranch = _repo.CurrentBranch;
                    if (currentBranch is null)
                    {
                        App.RaiseException(_repo.FullPath, App.Text("Error.NoCommitsToAmend"));
                        _useAmend = false;
                        OnPropertyChanged();
                        return;
                    }

                    CommitMessage = new Commands.QueryCommitFullMessage(_repo.FullPath, currentBranch.Head).GetResult();
                }
                else
                {
                    // amend無効化: メッセージと作者リセットをクリア
                    CommitMessage = string.Empty;
                    ResetAuthor = false;
                }

                // ステージング状態を再計算
                Staged = GetStagedChanges(_cached);
                VisibleStaged = GetVisibleChanges(_staged);
                SelectedStaged = [];
            }
        }
    }

    /// <summary>
    /// amend時にコミット作者をリセットするかどうか（--reset-author）。
    /// </summary>
    public bool ResetAuthor
    {
        get => _resetAuthor;
        set => SetProperty(ref _resetAuthor, value);
    }

    /// <summary>
    /// 変更ファイルの表示フィルタ文字列。変更時にフィルタ結果を再計算する。
    /// </summary>
    public string Filter
    {
        get => _filter;
        set
        {
            if (SetProperty(ref _filter, value))
            {
                if (_isLoadingData)
                    return;

                // フィルタに基づいて表示対象を再計算
                VisibleUnstaged = GetVisibleChanges(_unstaged);
                VisibleStaged = GetVisibleChanges(_staged);
                SelectedUnstaged = [];
            }
        }
    }

    /// <summary>
    /// アンステージド（未ステージ）の変更リスト。
    /// </summary>
    public List<Models.Change> Unstaged
    {
        get => _unstaged;
        private set => SetProperty(ref _unstaged, value);
    }

    /// <summary>
    /// フィルタ適用後の表示用アンステージド変更リスト。
    /// </summary>
    public List<Models.Change> VisibleUnstaged
    {
        get => _visibleUnstaged;
        private set => SetProperty(ref _visibleUnstaged, value);
    }

    /// <summary>
    /// ステージド（ステージ済み）の変更リスト。
    /// </summary>
    public List<Models.Change> Staged
    {
        get => _staged;
        private set => SetProperty(ref _staged, value);
    }

    /// <summary>
    /// フィルタ適用後の表示用ステージド変更リスト。
    /// </summary>
    public List<Models.Change> VisibleStaged
    {
        get => _visibleStaged;
        private set => SetProperty(ref _visibleStaged, value);
    }

    /// <summary>
    /// 選択されたアンステージド変更リスト。
    /// 選択変更時に対応する差分詳細を更新する。ステージド側の選択はクリアされる。
    /// </summary>
    public List<Models.Change> SelectedUnstaged
    {
        get => _selectedUnstaged;
        set
        {
            if (SetProperty(ref _selectedUnstaged, value))
            {
                if (value is null || value.Count == 0)
                {
                    if (_selectedStaged is null || _selectedStaged.Count == 0)
                        SetDetail(null, true);
                }
                else
                {
                    // アンステージド選択時はステージド側の選択をクリア
                    if (_selectedStaged is { Count: > 0 })
                        SelectedStaged = [];

                    if (value.Count == 1)
                        SetDetail(value[0], true);
                    else
                        SetDetail(null, true);
                }
            }
        }
    }

    /// <summary>
    /// 選択されたステージド変更リスト。
    /// 選択変更時に対応する差分詳細を更新する。アンステージド側の選択はクリアされる。
    /// </summary>
    public List<Models.Change> SelectedStaged
    {
        get => _selectedStaged;
        set
        {
            if (SetProperty(ref _selectedStaged, value))
            {
                if (value is null || value.Count == 0)
                {
                    if (_selectedUnstaged is null || _selectedUnstaged.Count == 0)
                        SetDetail(null, false);
                }
                else
                {
                    // ステージド選択時はアンステージド側の選択をクリア
                    if (_selectedUnstaged is { Count: > 0 })
                        SelectedUnstaged = [];

                    if (value.Count == 1)
                        SetDetail(value[0], false);
                    else
                        SetDetail(null, false);
                }
            }
        }
    }

    /// <summary>
    /// 選択された変更の詳細コンテキスト（差分ビューまたはコンフリクトビュー）。
    /// </summary>
    public object DetailContext
    {
        get => _detailContext;
        private set => SetProperty(ref _detailContext, value);
    }

    /// <summary>
    /// コミットメッセージ。
    /// </summary>
    public string CommitMessage
    {
        get => _commitMessage;
        set => SetProperty(ref _commitMessage, value);
    }

    /// <summary>
    /// コンストラクタ。リポジトリViewModelを設定する。
    /// </summary>
    public WorkingCopy(Repository repo)
    {
        _repo = repo;
    }

    /// <summary>
    /// リソースを解放する。進行中のマージメッセージがあればファイルに保存する。
    /// </summary>
    public void Dispose()
    {
        // 進行中の操作がある場合、コミットメッセージをMERGE_MSGに保存
        if (_inProgressContext is not null && !string.IsNullOrEmpty(_commitMessage))
            File.WriteAllText(Path.Combine(_repo.GitDir, "MERGE_MSG"), _commitMessage);

        _repo = null;
        _inProgressContext = null;

        // 全コレクションをクリアしてUI通知を発行
        _selectedUnstaged.Clear();
        OnPropertyChanged(nameof(SelectedUnstaged));

        _selectedStaged.Clear();
        OnPropertyChanged(nameof(SelectedStaged));

        _visibleUnstaged.Clear();
        OnPropertyChanged(nameof(VisibleUnstaged));

        _visibleStaged.Clear();
        OnPropertyChanged(nameof(VisibleStaged));

        _unstaged.Clear();
        OnPropertyChanged(nameof(Unstaged));

        _staged.Clear();
        OnPropertyChanged(nameof(Staged));

        _detailContext = null;
        _commitMessage = string.Empty;
    }

    /// <summary>
    /// 変更データを設定し、ステージド/アンステージドリストを更新する。
    /// 変更がない場合は選択の再描画のみ行う。変更がある場合は前回の選択状態を可能な限り復元する。
    /// </summary>
    public void SetData(List<Models.Change> changes, CancellationToken cancellationToken)
    {
        if (!IsChanged(_cached, changes))
        {
            // 変更なし: 選択の差分詳細のみリフレッシュ
            Dispatcher.UIThread.Invoke(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                HasUnsolvedConflicts = _cached.Find(x => x.IsConflicted) is not null;
                UpdateInProgressState();
                UpdateDetail();
            });

            return;
        }

        // 前回の選択状態を記憶
        HashSet<string> lastSelectedUnstaged = [];
        HashSet<string> lastSelectedStaged = [];
        if (_selectedUnstaged is { Count: > 0 })
        {
            foreach (var c in _selectedUnstaged)
                lastSelectedUnstaged.Add(c.Path);
        }
        else if (_selectedStaged is { Count: > 0 })
        {
            foreach (var c in _selectedStaged)
                lastSelectedStaged.Add(c.Path);
        }

        // アンステージド変更を抽出
        List<Models.Change> unstaged = [];
        var hasConflict = false;
        foreach (var c in changes)
        {
            if (c.WorkTree != Models.ChangeState.None)
            {
                unstaged.Add(c);
                hasConflict |= c.IsConflicted;
            }
        }

        if (hasConflict)
        {
            // コンフリクトファイルを先頭にソート
            unstaged.Sort((a, b) =>
            {
                if (a.IsConflicted != b.IsConflicted)
                    return a.IsConflicted ? -1 : 1;
                return Models.NumericSort.Compare(a.Path, b.Path);
            });
        }

        // フィルタ適用と前回の選択を復元
        var visibleUnstaged = GetVisibleChanges(unstaged);
        List<Models.Change> selectedUnstaged = [];
        foreach (var c in visibleUnstaged)
        {
            if (lastSelectedUnstaged.Contains(c.Path))
                selectedUnstaged.Add(c);
        }

        var staged = GetStagedChanges(changes);

        var visibleStaged = GetVisibleChanges(staged);
        List<Models.Change> selectedStaged = [];
        foreach (var c in visibleStaged)
        {
            if (lastSelectedStaged.Contains(c.Path))
                selectedStaged.Add(c);
        }

        // 何も選択されていないがコンフリクトがある場合、最初のコンフリクトを自動選択
        if (selectedUnstaged.Count == 0 && selectedStaged.Count == 0 && hasConflict)
        {
            var firstConflict = visibleUnstaged.Find(x => x.IsConflicted);
            selectedUnstaged.Add(firstConflict);
        }

        // UIスレッドでプロパティを一括更新
        Dispatcher.UIThread.Invoke(() =>
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            _isLoadingData = true;
            _cached = changes;
            HasUnsolvedConflicts = hasConflict;
            VisibleUnstaged = visibleUnstaged;
            VisibleStaged = visibleStaged;
            Unstaged = unstaged;
            Staged = staged;
            SelectedUnstaged = selectedUnstaged;
            SelectedStaged = selectedStaged;
            _isLoadingData = false;

            UpdateInProgressState();
            UpdateDetail();
        });
    }

    /// <summary>
    /// 変更をステージングする。コンフリクトが未解決の変更はスキップされる。
    /// </summary>
    public async Task StageChangesAsync(List<Models.Change> changes, Models.Change next)
    {
        var canStaged = await GetCanStageChangesAsync(changes);
        var count = canStaged.Count;
        if (count == 0)
            return;

        IsStaging = true;
        _selectedUnstaged = next is not null ? [next] : [];

        using var lockWatcher = _repo.LockWatcher();

        // パスリストを一時ファイルに書き出してgit addを実行
        var log = _repo.CreateLog("Stage");
        var pathSpecFile = Path.GetTempFileName();
        await using (var writer = new StreamWriter(pathSpecFile))
        {
            foreach (var c in canStaged)
                await writer.WriteLineAsync(c.Path);
        }

        await new Commands.Add(_repo.FullPath, pathSpecFile).Use(log).ExecAsync();
        File.Delete(pathSpecFile);
        log.Complete();

        _repo.MarkWorkingCopyDirtyManually();
        IsStaging = false;
    }

    /// <summary>
    /// 変更をアンステージングする。amend中はupdate-index、通常時はgit resetを使用する。
    /// </summary>
    public async Task UnstageChangesAsync(List<Models.Change> changes, Models.Change next)
    {
        var count = changes.Count;
        if (count == 0)
            return;

        IsUnstaging = true;
        _selectedStaged = next is not null ? [next] : [];

        using var lockWatcher = _repo.LockWatcher();

        var log = _repo.CreateLog("Unstage");
        if (_useAmend)
        {
            // amend中はupdate-indexコマンドを使用
            log.AppendLine("$ git update-index --index-info ");
            await new Commands.UpdateIndexInfo(_repo.FullPath, changes).ExecAsync();
        }
        else
        {
            // リネームされたファイルは元のパスも含める
            var pathSpecFile = Path.GetTempFileName();
            await using (var writer = new StreamWriter(pathSpecFile))
            {
                foreach (var c in changes)
                {
                    await writer.WriteLineAsync(c.Path);
                    if (c.Index == Models.ChangeState.Renamed)
                        await writer.WriteLineAsync(c.OriginalPath);
                }
            }

            await new Commands.Reset(_repo.FullPath, pathSpecFile).Use(log).ExecAsync();
            File.Delete(pathSpecFile);
        }
        log.Complete();

        _repo.MarkWorkingCopyDirtyManually();
        IsUnstaging = false;
    }

    /// <summary>
    /// 変更をパッチファイルとして保存する。
    /// </summary>
    public async Task SaveChangesToPatchAsync(List<Models.Change> changes, bool isUnstaged, string saveTo)
    {
        var succ = await Commands.SaveChangesAsPatch.ProcessLocalChangesAsync(_repo.FullPath, changes, isUnstaged, saveTo);
        if (succ)
            App.SendNotification(_repo.FullPath, App.Text("SaveAsPatchSuccess"));
    }

    /// <summary>
    /// 変更を破棄するダイアログを表示する。
    /// </summary>
    public void Discard(List<Models.Change> changes)
    {
        if (_repo.CanCreatePopup())
            _repo.ShowPopup(new Discard(_repo, changes));
    }

    /// <summary>
    /// 変更フィルタをクリアする。
    /// </summary>
    public void ClearFilter()
    {
        Filter = string.Empty;
    }

    /// <summary>
    /// コンフリクトを「相手側（theirs）」で解決する。
    /// 削除コンフリクトはファイルを削除し、その他はcheckout --theirsで解決する。
    /// </summary>
    public async Task UseTheirsAsync(List<Models.Change> changes)
    {
        using var lockWatcher = _repo.LockWatcher();

        List<string> files = [];
        List<string> needStage = [];
        var log = _repo.CreateLog("Use Theirs");

        foreach (var change in changes)
        {
            if (!change.IsConflicted)
                continue;

            // 削除系コンフリクトはファイルを直接削除
            if (change.ConflictReason is Models.ConflictReason.BothDeleted or Models.ConflictReason.DeletedByThem or Models.ConflictReason.AddedByUs)
            {
                var fullpath = Path.Combine(_repo.FullPath, change.Path);
                if (File.Exists(fullpath))
                    File.Delete(fullpath);

                needStage.Add(change.Path);
            }
            else
            {
                files.Add(change.Path);
            }
        }

        if (files.Count > 0)
        {
            var succ = await new Commands.Checkout(_repo.FullPath).Use(log).UseTheirsAsync(files);
            if (succ)
                needStage.AddRange(files);
        }

        // 解決したファイルをステージング
        if (needStage.Count > 0)
        {
            var pathSpecFile = Path.GetTempFileName();
            await File.WriteAllLinesAsync(pathSpecFile, needStage);
            await new Commands.Add(_repo.FullPath, pathSpecFile).Use(log).ExecAsync();
            File.Delete(pathSpecFile);
        }

        log.Complete();
        _repo.MarkWorkingCopyDirtyManually();
    }

    /// <summary>
    /// コンフリクトを「自分側（mine/ours）」で解決する。
    /// 削除コンフリクトはファイルを削除し、その他はcheckout --oursで解決する。
    /// </summary>
    public async Task UseMineAsync(List<Models.Change> changes)
    {
        using var lockWatcher = _repo.LockWatcher();

        List<string> files = [];
        List<string> needStage = [];
        var log = _repo.CreateLog("Use Mine");

        foreach (var change in changes)
        {
            if (!change.IsConflicted)
                continue;

            // 削除系コンフリクトはファイルを直接削除
            if (change.ConflictReason is Models.ConflictReason.BothDeleted or Models.ConflictReason.DeletedByUs or Models.ConflictReason.AddedByThem)
            {
                var fullpath = Path.Combine(_repo.FullPath, change.Path);
                if (File.Exists(fullpath))
                    File.Delete(fullpath);

                needStage.Add(change.Path);
            }
            else
            {
                files.Add(change.Path);
            }
        }

        if (files.Count > 0)
        {
            var succ = await new Commands.Checkout(_repo.FullPath).Use(log).UseMineAsync(files);
            if (succ)
                needStage.AddRange(files);
        }

        // 解決したファイルをステージング
        if (needStage.Count > 0)
        {
            var pathSpecFile = Path.GetTempFileName();
            await File.WriteAllLinesAsync(pathSpecFile, needStage);
            await new Commands.Add(_repo.FullPath, pathSpecFile).Use(log).ExecAsync();
            File.Delete(pathSpecFile);
        }

        log.Complete();
        _repo.MarkWorkingCopyDirtyManually();
    }

    /// <summary>
    /// 外部マージツールでコンフリクトを解決する。
    /// </summary>
    public async Task<bool> UseExternalMergeToolAsync(Models.Change change)
    {
        return await new Commands.MergeTool(_repo.FullPath, change?.Path).OpenAsync();
    }

    /// <summary>
    /// 外部差分ツールで変更を表示する。
    /// </summary>
    public void UseExternalDiffTool(Models.Change change, bool isUnstaged)
    {
        new Commands.DiffTool(_repo.FullPath, new Models.DiffOption(change, isUnstaged)).Open();
    }

    /// <summary>
    /// 進行中のマージ/リベース/チェリーピック/リバートを続行する。
    /// コミットメッセージがあればMERGE_MSGに保存してから続行する。
    /// </summary>
    public async Task ContinueMergeAsync()
    {
        if (_inProgressContext is not null)
        {
            using var lockWatcher = _repo.LockWatcher();
            IsCommitting = true;

            // カスタムコミットメッセージをMERGE_MSGに保存
            var mergeMsgFile = Path.Combine(_repo.GitDir, "MERGE_MSG");
            if (File.Exists(mergeMsgFile) && !string.IsNullOrWhiteSpace(_commitMessage))
                await File.WriteAllTextAsync(mergeMsgFile, _commitMessage);

            var log = _repo.CreateLog($"Continue {_inProgressContext.Name}");
            await _inProgressContext.ContinueAsync(log);
            log.Complete();

            CommitMessage = string.Empty;
            IsCommitting = false;
        }
        else
        {
            _repo.MarkWorkingCopyDirtyManually();
        }
    }

    /// <summary>
    /// 進行中のマージ/リベース操作の現在のステップをスキップする。
    /// </summary>
    public async Task SkipMergeAsync()
    {
        if (_inProgressContext is not null)
        {
            using var lockWatcher = _repo.LockWatcher();
            IsCommitting = true;

            var log = _repo.CreateLog($"Skip {_inProgressContext.Name}");
            await _inProgressContext.SkipAsync(log);
            log.Complete();

            CommitMessage = string.Empty;
            IsCommitting = false;
        }
        else
        {
            _repo.MarkWorkingCopyDirtyManually();
        }
    }

    /// <summary>
    /// 進行中のマージ/リベース操作を中止して元の状態に戻す。
    /// </summary>
    public async Task AbortMergeAsync()
    {
        if (_inProgressContext is not null)
        {
            using var lockWatcher = _repo.LockWatcher();
            IsCommitting = true;

            var log = _repo.CreateLog($"Abort {_inProgressContext.Name}");
            await _inProgressContext.AbortAsync(log);
            log.Complete();

            CommitMessage = string.Empty;
            IsCommitting = false;
        }
        else
        {
            _repo.MarkWorkingCopyDirtyManually();
        }
    }

    /// <summary>
    /// コミットメッセージテンプレートを適用する。
    /// </summary>
    public void ApplyCommitMessageTemplate(Models.CommitTemplate tmpl)
    {
        CommitMessage = tmpl.Apply(_repo.CurrentBranch, _staged);
    }

    /// <summary>
    /// コミットメッセージの履歴をクリアする（確認ダイアログ付き）。
    /// </summary>
    public async Task ClearCommitMessageHistoryAsync()
    {
        var sure = await App.AskConfirmAsync(App.Text("WorkingCopy.ClearCommitHistories.Confirm"));
        if (sure)
            _repo.Settings.CommitMessages.Clear();
    }

    /// <summary>
    /// コミットを実行する。デタッチドHEAD確認、空コミット確認、自動ステージ、自動プッシュに対応する。
    /// </summary>
    public async Task CommitAsync(bool autoStage, bool autoPush)
    {
        if (string.IsNullOrWhiteSpace(_commitMessage))
            return;

        if (!_repo.CanCreatePopup())
        {
            App.RaiseException(_repo.FullPath, App.Text("Error.RepoHasUnfinishedJob"));
            return;
        }

        // 未解決コンフリクトがある場合は自動ステージ不可
        if (autoStage && HasUnsolvedConflicts)
        {
            App.RaiseException(_repo.FullPath, App.Text("Error.RepoHasConflicts"));
            return;
        }

        // デタッチドHEADの場合は確認
        if (_repo.CurrentBranch is { IsDetachedHead: true })
        {
            var msg = App.Text("WorkingCopy.ConfirmCommitWithDetachedHead");
            var sure = await App.AskConfirmAsync(msg);
            if (!sure)
                return;
        }

        // フィルタにより非表示のステージド変更がある場合は確認
        if (!string.IsNullOrEmpty(_filter) && _staged.Count > _visibleStaged.Count)
        {
            var msg = App.Text("WorkingCopy.ConfirmCommitWithFilter", _staged.Count, _visibleStaged.Count, _staged.Count - _visibleStaged.Count);
            var sure = await App.AskConfirmAsync(msg);
            if (!sure)
                return;
        }

        // 空コミットの確認
        if (!_useAmend)
        {
            if ((!autoStage && _staged.Count == 0) || (autoStage && _cached.Count == 0))
            {
                var rs = await App.AskConfirmEmptyCommitAsync(_cached.Count > 0, _selectedUnstaged is { Count: > 0 });
                if (rs == Models.ConfirmEmptyCommitResult.Cancel)
                    return;

                if (rs == Models.ConfirmEmptyCommitResult.StageAllAndCommit)
                    autoStage = true;
                else if (rs == Models.ConfirmEmptyCommitResult.StageSelectedAndCommit)
                    await StageChangesAsync(_selectedUnstaged, null);
            }
        }

        using var lockWatcher = _repo.LockWatcher();
        IsCommitting = true;
        _repo.Settings.PushCommitMessage(_commitMessage);

        // 自動ステージが有効な場合は全アンステージド変更をステージ
        if (autoStage && _unstaged.Count > 0)
            await StageChangesAsync(_unstaged, null);

        var log = _repo.CreateLog("Commit");
        var succ = await new Commands.Commit(_repo.FullPath, _commitMessage, EnableSignOff, NoVerifyOnCommit, _useAmend, _resetAuthor)
                .Use(log)
                .RunAsync()
                .ConfigureAwait(false);

        log.Complete();

        if (succ)
        {
            CommitMessage = string.Empty;
            UseAmend = false;
            // 自動プッシュが有効でリモートがある場合はプッシュダイアログを表示
            if (autoPush && _repo.Remotes.Count > 0)
            {
                Models.Branch pushBranch = null;
                if (_repo.CurrentBranch is null)
                {
                    var currentBranchName = await new Commands.QueryCurrentBranch(_repo.FullPath).GetResultAsync();
                    pushBranch = new Models.Branch() { Name = currentBranchName };
                }

                if (_repo.CanCreatePopup())
                    await _repo.ShowAndStartPopupAsync(new Push(_repo, pushBranch));
            }
        }

        _repo.MarkBranchesDirtyManually();
        IsCommitting = false;
    }

    /// <summary>
    /// フィルタ文字列に基づいて表示対象の変更リストを取得する。
    /// </summary>
    private List<Models.Change> GetVisibleChanges(List<Models.Change> changes)
    {
        if (string.IsNullOrEmpty(_filter))
            return changes;

        List<Models.Change> visible = [];

        foreach (var c in changes)
        {
            if (c.Path.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                visible.Add(c);
        }

        return visible;
    }

    /// <summary>
    /// ステージング可能な変更を取得する。未解決コンフリクトは解決済みのもののみ含める。
    /// </summary>
    private async Task<List<Models.Change>> GetCanStageChangesAsync(List<Models.Change> changes)
    {
        if (!HasUnsolvedConflicts)
            return changes;

        List<Models.Change> outs = [];
        foreach (var c in changes)
        {
            if (c.IsConflicted)
            {
                // BothAdded/BothModifiedのみ解決済みかチェック
                var isResolved = c.ConflictReason switch
                {
                    Models.ConflictReason.BothAdded or Models.ConflictReason.BothModified =>
                        await new Commands.IsConflictResolved(_repo.FullPath, c).GetResultAsync(),
                    _ => false,
                };

                if (!isResolved)
                    continue;
            }

            outs.Add(c);
        }

        return outs;
    }

    /// <summary>
    /// ステージド変更リストを取得する。amend時はHEADとの差分を、通常時はインデックスの変更を返す。
    /// </summary>
    private List<Models.Change> GetStagedChanges(List<Models.Change> cached)
    {
        if (_useAmend)
        {
            // amend時: HEADの親との差分を取得
            var head = new Commands.QuerySingleCommit(_repo.FullPath, "HEAD").GetResult();
            return new Commands.QueryStagedChangesWithAmend(_repo.FullPath, head.Parents.Count == 0 ? Models.Commit.EmptyTreeSHA1 : $"{head.SHA}^").GetResult();
        }

        List<Models.Change> rs = [];
        foreach (var c in cached)
        {
            if (c.Index != Models.ChangeState.None)
                rs.Add(c);
        }
        return rs;
    }

    /// <summary>
    /// 現在の選択状態に基づいて差分詳細を更新する。
    /// </summary>
    private void UpdateDetail()
    {
        if (_selectedUnstaged.Count == 1)
            SetDetail(_selectedUnstaged[0], true);
        else if (_selectedStaged.Count == 1)
            SetDetail(_selectedStaged[0], false);
        else
            SetDetail(null, false);
    }

    /// <summary>
    /// gitディレクトリ内のファイルを検査して進行中の操作状態を更新する。
    /// チェリーピック、リベース、リバート、マージの各状態を検出する。
    /// </summary>
    private void UpdateInProgressState()
    {
        var oldType = _inProgressContext is not null ? _inProgressContext.GetType() : null;

        // gitディレクトリ内のマーカーファイルで進行中の操作を判定
        if (File.Exists(Path.Combine(_repo.GitDir, "CHERRY_PICK_HEAD")))
            InProgressContext = new CherryPickInProgress(_repo);
        else if (Directory.Exists(Path.Combine(_repo.GitDir, "rebase-merge")) || Directory.Exists(Path.Combine(_repo.GitDir, "rebase-apply")))
            InProgressContext = new RebaseInProgress(_repo);
        else if (File.Exists(Path.Combine(_repo.GitDir, "REVERT_HEAD")))
            InProgressContext = new RevertInProgress(_repo);
        else if (File.Exists(Path.Combine(_repo.GitDir, "MERGE_HEAD")))
            InProgressContext = new MergeInProgress(_repo);
        else
            InProgressContext = null;

        // 状態が変わっていなくて既にメッセージがある場合はスキップ
        if (_inProgressContext is not null && _inProgressContext.GetType() == oldType && !string.IsNullOrEmpty(_commitMessage))
            return;

        // MERGE_MSGファイルからコミットメッセージを読み込み
        if (LoadCommitMessageFromFile(Path.Combine(_repo.GitDir, "MERGE_MSG")))
            return;

        if (_inProgressContext is not RebaseInProgress { } rebasing)
            return;

        // リベース中: rebase-merge/messageファイルからメッセージを読み込み
        if (LoadCommitMessageFromFile(Path.Combine(_repo.GitDir, "rebase-merge", "message")))
            return;

        CommitMessage = new Commands.QueryCommitFullMessage(_repo.FullPath, rebasing.StoppedAt.SHA).GetResult();
    }

    /// <summary>
    /// ファイルからコミットメッセージを読み込む。成功した場合trueを返す。
    /// </summary>
    private bool LoadCommitMessageFromFile(string file)
    {
        if (!File.Exists(file))
            return false;

        var msg = File.ReadAllText(file).Trim();
        if (string.IsNullOrEmpty(msg))
            return false;

        CommitMessage = msg;
        return true;
    }

    /// <summary>
    /// 選択された変更に応じて詳細コンテキストを設定する。
    /// コンフリクトの場合はConflict、それ以外はDiffContextを生成する。
    /// </summary>
    private void SetDetail(Models.Change change, bool isUnstaged)
    {
        if (_isLoadingData)
            return;

        if (change is null)
            DetailContext = null;
        else if (change.IsConflicted)
            DetailContext = new Conflict(_repo, this, change);
        else
            DetailContext = new DiffContext(_repo.FullPath, new Models.DiffOption(change, isUnstaged), _detailContext as DiffContext);
    }

    /// <summary>
    /// 2つの変更リストが異なるかどうかを判定する。パス、インデックス、ワークツリー状態を比較する。
    /// </summary>
    private static bool IsChanged(List<Models.Change> old, List<Models.Change> cur)
    {
        if (old.Count != cur.Count)
            return true;

        for (int idx = 0; idx < old.Count; idx++)
        {
            var o = old[idx];
            var c = cur[idx];
            if (!o.Path.Equals(c.Path, StringComparison.Ordinal) || o.Index != c.Index || o.WorkTree != c.WorkTree)
                return true;
        }

        return false;
    }

    private Repository _repo = null;
    private bool _isLoadingData = false;
    private bool _isStaging = false;
    private bool _isUnstaging = false;
    private bool _isCommitting = false;
    private bool _useAmend = false;
    private bool _resetAuthor = false;
    private bool _hasRemotes = false;
    private List<Models.Change> _cached = [];
    private List<Models.Change> _unstaged = [];
    private List<Models.Change> _visibleUnstaged = [];
    private List<Models.Change> _staged = [];
    private List<Models.Change> _visibleStaged = [];
    private List<Models.Change> _selectedUnstaged = [];
    private List<Models.Change> _selectedStaged = [];
    private object _detailContext = null;
    private string _filter = string.Empty;
    private string _commitMessage = string.Empty;

    private bool _hasUnsolvedConflicts = false;
    private InProgressContext _inProgressContext = null;
}
