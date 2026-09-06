// nullable 移行未実施。1 ファイルずつ null 注釈を入れてこの 2 行を削除していく。
#nullable disable warnings
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>
/// コンフリクト解決ビューのViewModel。
/// コンフリクトが発生したファイルの解決操作（自分の変更を使用、相手の変更を使用、マージ）を提供する。
/// チェリーピック、リベース、リバート、マージなど各種操作中のコンフリクトに対応する。
/// </summary>
public class Conflict : ObservableObject
{
    /// <summary>
    /// コンフリクトマーカー（ファイル内のコンフリクト箇所を示す文字列）。
    /// </summary>
    public string Marker
    {
        get => _change.ConflictMarker;
    }

    /// <summary>
    /// コンフリクトの説明テキスト。
    /// </summary>
    public string Description
    {
        get => _change.ConflictDesc;
    }

    /// <summary>
    /// 相手側の変更元情報（マージ元、チェリーピック元など）。
    /// </summary>
    public object Theirs
    {
        get;
        private set;
    }

    /// <summary>
    /// 自分側の変更元情報（通常はHEADコミット）。
    /// </summary>
    public object Mine
    {
        get;
        private set;
    }

    /// <summary>
    /// コンフリクトが解決済みかどうかのフラグ。
    /// </summary>
    public bool IsResolved
    {
        get => _state == Models.ConflictFileState.Resolved;
    }

    /// <summary>
    /// マージツールで解決可能かどうかのフラグ。
    /// 両方で追加または両方で変更されたファイルのみマージ可能。
    /// </summary>
    public bool CanMerge
    {
        get => _state == Models.ConflictFileState.UnmergedText;
    }

    /// <summary>
    /// コンストラクタ。リポジトリ、ワーキングコピー、コンフリクトファイルを受け取って初期化する。
    /// 進行中の操作種別に応じてMine/Theirsを設定する。
    /// </summary>
    /// <param name="repo">対象のリポジトリViewModel</param>
    /// <param name="wc">ワーキングコピーViewModel</param>
    /// <param name="change">コンフリクトが発生した変更ファイル</param>
    public Conflict(Repository repo, WorkingCopy wc, Models.Change change)
    {
        _repo = repo;
        _wc = wc;
        _change = change;

        var canMerge = change.ConflictReason is Models.ConflictReason.BothAdded or Models.ConflictReason.BothModified &&
            !Directory.Exists(Path.Combine(repo.FullPath, change.Path));
        var progress = wc.InProgressContext;
        Task.Run(async () =>
        {
            var head = await new Commands.QuerySingleCommit(repo.FullPath, "HEAD").GetResultAsync().ConfigureAwait(false);
            var state = canMerge
                ? await new Commands.QueryConflictFileState(repo.FullPath, change).GetResultAsync().ConfigureAwait(false)
                : Models.ConflictFileState.Unknown;
            Dispatcher.UIThread.Post(() =>
            {
                _head = head;
                _state = state;
                (Mine, Theirs) = progress switch
                {
                    CherryPickInProgress cherryPick => (head, cherryPick.Head),
                    RebaseInProgress rebase => (rebase.Onto, rebase.StoppedAt),
                    RevertInProgress revert => (head, revert.Head),
                    MergeInProgress merge => (head, merge.Source),
                    _ => (head, (object)"Stash or Patch"),
                };
                OnPropertyChanged(nameof(Mine));
                OnPropertyChanged(nameof(Theirs));
                OnPropertyChanged(nameof(IsResolved));
                OnPropertyChanged(nameof(CanMerge));
            });
        });
    }

    /// <summary>
    /// 相手側の変更を採用してコンフリクトを解決する。
    /// </summary>
    public async Task UseTheirsAsync()
    {
        await _wc.UseTheirsAsync([_change]);
    }

    /// <summary>
    /// 自分側の変更を採用してコンフリクトを解決する。
    /// </summary>
    public async Task UseMineAsync()
    {
        await _wc.UseMineAsync([_change]);
    }

    /// <summary>
    /// 内蔵マージエディタでコンフリクトを解決する。
    /// 非モーダルウィンドウとして開き、他の操作をブロックしない (upstream 7779b91e)。
    /// </summary>
    public void Merge()
    {
        if (CanMerge)
            App.ShowWindow(new MergeConflictEditor(_repo, _head, _change.Path));
    }

    /// <summary>
    /// 外部マージツールでコンフリクトを解決する。
    /// </summary>
    public async Task MergeExternalAsync()
    {
        if (CanMerge)
            await _wc.UseExternalMergeToolAsync(_change);
    }

    /// <summary>対象リポジトリへの参照</summary>
    private Models.ConflictFileState _state;
    private Repository _repo = null;
    /// <summary>ワーキングコピーViewModelへの参照</summary>
    private WorkingCopy _wc = null;
    /// <summary>HEADコミット</summary>
    private Models.Commit _head = null;
    /// <summary>コンフリクトが発生した変更ファイル</summary>
    private Models.Change _change = null;
}
