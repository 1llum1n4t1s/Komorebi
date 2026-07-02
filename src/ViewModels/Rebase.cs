using System;
using System.Threading.Tasks;

using Avalonia.Threading;

namespace Komorebi.ViewModels;

/// <summary>
/// リベース事前チェック（`git replay --onto`）の進行状態。
/// </summary>
public enum RebaseTestingState
{
    /// <summary>チェック無効（git 2.44.0未満、またはHEADがマージベースと一致）。</summary>
    Disabled,
    /// <summary>チェック実行中。</summary>
    Testing,
    /// <summary>リベースするとコンフリクトが発生する。</summary>
    WillCauseConflicts,
    /// <summary>チェック中に不明なエラーが発生した。</summary>
    UnknownError,
    /// <summary>コンフリクトなしでリベース可能。</summary>
    NoConflicts,
}

/// <summary>
/// 現在のブランチを別のブランチまたはコミットにリベースするダイアログのViewModel。
/// ブランチまたはコミットをリベース先として指定でき、AutoStashオプションに対応する。
/// </summary>
public class Rebase : Popup
{
    /// <summary>
    /// リベース対象の現在のブランチ。
    /// </summary>
    public Models.Branch Current
    {
        get;
        private set;
    }

    /// <summary>
    /// リベース先のブランチまたはコミット。
    /// </summary>
    public object On
    {
        get;
        private set;
    }

    /// <summary>
    /// リベース前にローカル変更を自動スタッシュするかどうか。
    /// </summary>
    public bool AutoStash
    {
        get;
        set;
    }

    /// <summary>
    /// リベース事前チェックの進行状態。ポップアップ表示直後にバックグラウンドで
    /// `git replay --onto` を実行し、コンフリクトの有無を判定する（git 2.44.0以上のみ）。
    /// </summary>
    public RebaseTestingState TestingState
    {
        get => _testingState;
        private set => SetProperty(ref _testingState, value);
    }

    /// <summary>
    /// ブランチを指定してリベースダイアログを初期化する。
    /// ブランチのHEADコミットをリビジョンとして使用する。
    /// </summary>
    public Rebase(Repository repo, Models.Branch current, Models.Branch on)
    {
        _repo = repo;
        _revision = on.Head;
        Current = current;
        On = on;
        AutoStash = true;

        Test();
    }

    /// <summary>
    /// コミットを指定してリベースダイアログを初期化する。
    /// コミットのSHAをリビジョンとして使用する。
    /// </summary>
    public Rebase(Repository repo, Models.Branch current, Models.Commit on)
    {
        _repo = repo;
        _revision = on.SHA;
        Current = current;
        On = on;
        AutoStash = true;

        Test();
    }

    /// <summary>
    /// リベースを実行する。
    /// コミットメッセージをクリアし、指定されたリビジョンにリベースする。
    /// </summary>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();
        // リベース前にコミットメッセージをクリア
        _repo.ClearCommitMessage();
        ProgressDescription = App.Text("Progress.Rebasing");

        var log = _repo.CreateLog("Rebase");
        Use(log);

        // git rebase コマンドを実行
        await new Commands.Rebase(_repo.FullPath, _revision, AutoStash)
            .Use(log)
            .ExecAsync();

        log.Complete();
        return true;
    }

    /// <summary>
    /// バックグラウンドでリベース事前チェックを実行する。
    /// マージベースを求めた上で `git replay --onto` を実行し、
    /// 終了コードからコンフリクトの有無を判定してTestingStateへ反映する。
    /// `git replay`未対応のGitバージョンではチェックを行わない。
    /// </summary>
    private void Test()
    {
        if (Native.OS.GitVersion < Models.GitVersions.REPLAY)
            return;

        var head = Current.Head;
        TestingState = RebaseTestingState.Testing;

        Task.Run(async () =>
        {
            var mergeBase = await new Commands.MergeBase(_repo.FullPath, head, _revision)
                .GetResultAsync()
                .ConfigureAwait(false);

            if (string.IsNullOrEmpty(mergeBase))
            {
                Dispatcher.UIThread.Post(() => TestingState = RebaseTestingState.UnknownError);
                return;
            }
            else if (head.Equals(mergeBase, StringComparison.Ordinal))
            {
                // 既にリベース先の子孫であり、リベースしてもコンフリクトは発生しない
                Dispatcher.UIThread.Post(() => TestingState = RebaseTestingState.NoConflicts);
                return;
            }

            var exitCode = await new Commands.Replay(_repo.FullPath, _revision, $"{mergeBase}..{head}")
                .GetExitCodeAsync()
                .ConfigureAwait(false);

            Dispatcher.UIThread.Post(() => TestingState = exitCode switch
            {
                0 => RebaseTestingState.NoConflicts,
                1 => RebaseTestingState.WillCauseConflicts,
                _ => RebaseTestingState.UnknownError,
            });
        });
    }

    /// <summary>対象リポジトリ</summary>
    private readonly Repository _repo;
    /// <summary>リベース先のリビジョン（SHA）</summary>
    private readonly string _revision;
    /// <summary>リベース事前チェックの進行状態</summary>
    private RebaseTestingState _testingState = RebaseTestingState.Disabled;
}
