using System.IO;
using System.Threading.Tasks;

using Avalonia.Threading;

namespace Komorebi.ViewModels;

/// <summary>
/// マージ事前チェック（`git merge-tree --write-tree`）の進行状態。
/// </summary>
public enum MergeTestingState
{
    /// <summary>チェック無効（Fast-Forward強制時など）。</summary>
    Disabled,
    /// <summary>チェック実行中。</summary>
    Testing,
    /// <summary>マージするとコンフリクトが発生する。</summary>
    WillCauseConflicts,
    /// <summary>チェック中に不明なエラーが発生した。</summary>
    UnknownError,
    /// <summary>コンフリクトなしでマージ可能。</summary>
    NoConflicts,
}

/// <summary>
/// ブランチ・コミット・タグを現在のブランチにマージするダイアログのViewModel。
/// マージモードの選択、マージメッセージの編集、マージ実行を管理する。
/// </summary>
public class Merge : Popup
{
    /// <summary>
    /// マージ元のオブジェクト（ブランチ、コミット、またはタグ）。
    /// </summary>
    public object Source
    {
        get;
    }

    /// <summary>
    /// マージ先のブランチ名。
    /// </summary>
    public string Into
    {
        get;
    }

    /// <summary>
    /// 選択されたマージモード（デフォルト、Fast-Forward、No Fast-Forward、Squash等）。
    /// モード変更時にメッセージ編集可否も連動して更新する。
    /// </summary>
    public Models.MergeMode Mode
    {
        get => _mode;
        set
        {
            if (SetProperty(ref _mode, value))
                // メッセージ編集はDefault、FastForward、NoFastForwardモードでのみ可能
                CanEditMessage = _mode == Models.MergeMode.Default ||
                    _mode == Models.MergeMode.FastForward ||
                    _mode == Models.MergeMode.NoFastForward;
        }
    }

    /// <summary>
    /// マージメッセージの編集が可能かどうか。マージモードに連動する。
    /// </summary>
    public bool CanEditMessage
    {
        get => _canEditMessage;
        set => SetProperty(ref _canEditMessage, value);
    }

    /// <summary>
    /// マージ後にコミットメッセージをエディタで編集するかどうか。
    /// </summary>
    public bool Edit
    {
        get;
        set;
    } = false;

    /// <summary>
    /// マージ事前チェックの進行状態。ポップアップ表示直後にバックグラウンドで
    /// `git merge-tree --write-tree` を実行し、コンフリクトの有無を判定する。
    /// </summary>
    public MergeTestingState TestingState
    {
        get => _testingState;
        private set => SetProperty(ref _testingState, value);
    }

    /// <summary>
    /// ブランチをマージ元として初期化するコンストラクタ。
    /// </summary>
    /// <param name="repo">対象リポジトリ</param>
    /// <param name="source">マージ元ブランチ</param>
    /// <param name="into">マージ先ブランチ名</param>
    /// <param name="forceFastForward">Fast-Forwardを強制するかどうか</param>
    public Merge(Repository repo, Models.Branch source, string into, bool forceFastForward)
    {
        _repo = repo;
        _sourceName = source.FriendlyName;

        Source = source;
        Into = into;
        // Fast-Forward強制の場合はFastForwardモード、それ以外は自動選択
        Mode = forceFastForward ? Models.MergeMode.FastForward : AutoSelectMergeMode();

        // Fast-Forward強制時はコンフリクトが起こり得ないため事前チェック不要
        if (!forceFastForward)
            Test();
    }

    /// <summary>
    /// コミットをマージ元として初期化するコンストラクタ。
    /// </summary>
    /// <param name="repo">対象リポジトリ</param>
    /// <param name="source">マージ元コミット</param>
    /// <param name="into">マージ先ブランチ名</param>
    public Merge(Repository repo, Models.Commit source, string into)
    {
        _repo = repo;
        _sourceName = source.SHA;

        Source = source;
        Into = into;
        Mode = AutoSelectMergeMode();

        Test();
    }

    /// <summary>
    /// タグをマージ元として初期化するコンストラクタ。
    /// </summary>
    /// <param name="repo">対象リポジトリ</param>
    /// <param name="source">マージ元タグ</param>
    /// <param name="into">マージ先ブランチ名</param>
    public Merge(Repository repo, Models.Tag source, string into)
    {
        _repo = repo;
        _sourceName = source.Name;

        Source = source;
        Into = into;
        Mode = AutoSelectMergeMode();

        Test();
    }

    /// <summary>
    /// マージを実行する。SquashモードではSQUASH_MSGをコミットメッセージに設定する。
    /// マージ成功時はサブモジュールの自動更新も行い、履歴ビューでHEADに移動する。
    /// </summary>
    public override async Task<bool> Sure()
    {
        // ファイルシステムウォッチャーをロックしてマージ中の変更検知を抑制
        using var lockWatcher = _repo.LockWatcher();
        _repo.ClearCommitMessage();
        ProgressDescription = App.Text("Progress.Merging", _sourceName, Into);

        var log = _repo.CreateLog($"Merging '{_sourceName}' into '{Into}'");
        Use(log);

        // git mergeコマンドを実行
        var succ = await new Commands.Merge(_repo.FullPath, _sourceName, Mode.Arg, _canEditMessage && Edit)
            .Use(log)
            .ExecAsync();

        if (succ)
        {
            // SquashモードではSQUASH_MSGファイルの内容をコミットメッセージに設定
            var squashMsgFile = Path.Combine(_repo.GitDir, "SQUASH_MSG");
            if (Mode == Models.MergeMode.Squash && File.Exists(squashMsgFile))
            {
                var msg = await File.ReadAllTextAsync(squashMsgFile);
                _repo.SetCommitMessage(msg);
            }

            // サブモジュールの自動更新を実行
            await _repo.AutoUpdateSubmodulesAsync(log);
        }

        log.Complete();

        // マージ成功かつ履歴ビュー表示中の場合、HEADコミットに移動
        if (succ && _repo.SelectedViewIndex == 0)
        {
            var head = await new Commands.QueryRevisionByRefName(_repo.FullPath, "HEAD").GetResultAsync();
            _repo.NavigateToCommit(head, true);
        }

        return true;
    }

    /// <summary>
    /// ブランチのgit設定またはリポジトリ設定に基づいてマージモードを自動選択する。
    /// </summary>
    private Models.MergeMode AutoSelectMergeMode()
    {
        // branch.<name>.mergeoptionsのgit設定を確認
        var config = new Commands.Config(_repo.FullPath).Get($"branch.{Into}.mergeoptions");
        var mode = config switch
        {
            "--ff-only" => Models.MergeMode.FastForward,
            "--no-ff" => Models.MergeMode.NoFastForward,
            "--squash" => Models.MergeMode.Squash,
            "--no-commit" or "--no-ff --no-commit" => Models.MergeMode.DontCommit,
            _ => null,
        };

        if (mode is not null)
            return mode;

        // git設定がない場合はリポジトリの優先マージモード設定を使用
        var preferredMergeModeIdx = _repo.Settings.PreferredMergeMode;
        if (preferredMergeModeIdx < 0 || preferredMergeModeIdx >= Models.MergeMode.Supported.Length)
            return Models.MergeMode.Default;

        return Models.MergeMode.Supported[preferredMergeModeIdx];
    }

    /// <summary>
    /// バックグラウンドでマージ事前チェックを実行する。
    /// マージベースを求めた上で `git merge-tree --write-tree` を実行し、
    /// 終了コードからコンフリクトの有無を判定してTestingStateへ反映する。
    /// </summary>
    private void Test()
    {
        TestingState = MergeTestingState.Testing;

        Task.Run(async () =>
        {
            var mergeBase = await new Commands.MergeBase(_repo.FullPath, _sourceName, Into)
                .GetResultAsync()
                .ConfigureAwait(false);

            if (string.IsNullOrEmpty(mergeBase))
            {
                Dispatcher.UIThread.Post(() => TestingState = MergeTestingState.Disabled);
                return;
            }

            var exitCode = await new Commands.MergeTree(_repo.FullPath, _sourceName, Into)
                .GetExitCodeAsync()
                .ConfigureAwait(false);

            Dispatcher.UIThread.Post(() => TestingState = exitCode switch
            {
                0 => MergeTestingState.NoConflicts,
                1 => MergeTestingState.WillCauseConflicts,
                _ => MergeTestingState.UnknownError,
            });
        });
    }

    /// <summary>対象リポジトリ</summary>
    private readonly Repository _repo = null;
    /// <summary>マージ元の名前（ブランチ名、SHA、タグ名のいずれか）</summary>
    private readonly string _sourceName;
    /// <summary>選択されたマージモード</summary>
    private Models.MergeMode _mode = Models.MergeMode.Default;
    /// <summary>メッセージ編集可否フラグ</summary>
    private bool _canEditMessage = true;
    /// <summary>マージ事前チェックの進行状態</summary>
    private MergeTestingState _testingState = MergeTestingState.Disabled;
}
