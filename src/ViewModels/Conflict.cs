using System.IO;
using System.Threading.Tasks;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     コンフリクト解決ビューのViewModel。
    ///     コンフリクトが発生したファイルの解決操作（自分の変更を使用、相手の変更を使用、マージ）を提供する。
    ///     チェリーピック、リベース、リバート、マージなど各種操作中のコンフリクトに対応する。
    /// </summary>
    public class Conflict
    {
        /// <summary>
        ///     コンフリクトマーカー（ファイル内のコンフリクト箇所を示す文字列）。
        /// </summary>
        public string Marker
        {
            get => _change.ConflictMarker;
        }

        /// <summary>
        ///     コンフリクトの説明テキスト。
        /// </summary>
        public string Description
        {
            get => _change.ConflictDesc;
        }

        /// <summary>
        ///     相手側の変更元情報（マージ元、チェリーピック元など）。
        /// </summary>
        public object Theirs
        {
            get;
            private set;
        }

        /// <summary>
        ///     自分側の変更元情報（通常はHEADコミット）。
        /// </summary>
        public object Mine
        {
            get;
            private set;
        }

        /// <summary>
        ///     コンフリクトが解決済みかどうかのフラグ。
        /// </summary>
        public bool IsResolved
        {
            get;
            private set;
        } = false;

        /// <summary>
        ///     マージツールで解決可能かどうかのフラグ。
        ///     両方で追加または両方で変更されたファイルのみマージ可能。
        /// </summary>
        public bool CanMerge
        {
            get;
            private set;
        } = false;

        /// <summary>
        ///     コンストラクタ。リポジトリ、ワーキングコピー、コンフリクトファイルを受け取って初期化する。
        ///     進行中の操作種別に応じてMine/Theirsを設定する。
        /// </summary>
        /// <param name="repo">対象のリポジトリViewModel</param>
        /// <param name="wc">ワーキングコピーViewModel</param>
        /// <param name="change">コンフリクトが発生した変更ファイル</param>
        public Conflict(Repository repo, WorkingCopy wc, Models.Change change)
        {
            _repo = repo;
            _wc = wc;
            _change = change;

            // 両方で追加または両方で変更されたファイルのみマージ可能とする
            CanMerge = _change.ConflictReason is Models.ConflictReason.BothAdded or Models.ConflictReason.BothModified;
            if (CanMerge)
                // ディレクトリ（サブモジュール）はマージ不可
                CanMerge = !Directory.Exists(Path.Combine(repo.FullPath, change.Path));

            // マージ可能な場合はコンフリクトが既に解決済みかチェックする
            if (CanMerge)
                IsResolved = new Commands.IsConflictResolved(repo.FullPath, change).GetResult();

            // HEADコミットを取得する
            _head = new Commands.QuerySingleCommit(repo.FullPath, "HEAD").GetResult();

            // 進行中の操作種別に応じてMine/Theirsを設定する
            (Mine, Theirs) = wc.InProgressContext switch
            {
                CherryPickInProgress cherryPick => (_head, cherryPick.Head),
                RebaseInProgress rebase => (rebase.Onto, rebase.StoppedAt),
                RevertInProgress revert => (_head, revert.Head),
                MergeInProgress merge => (_head, merge.Source),
                _ => (_head, (object)"Stash or Patch"),
            };
        }

        /// <summary>
        ///     相手側の変更を採用してコンフリクトを解決する。
        /// </summary>
        public async Task UseTheirsAsync()
        {
            await _wc.UseTheirsAsync([_change]);
        }

        /// <summary>
        ///     自分側の変更を採用してコンフリクトを解決する。
        /// </summary>
        public async Task UseMineAsync()
        {
            await _wc.UseMineAsync([_change]);
        }

        /// <summary>
        ///     内蔵マージエディタでコンフリクトを解決する。
        /// </summary>
        public async Task MergeAsync()
        {
            if (CanMerge)
                await App.ShowDialog(new MergeConflictEditor(_repo, _head, _change.Path));
        }

        /// <summary>
        ///     外部マージツールでコンフリクトを解決する。
        /// </summary>
        public async Task MergeExternalAsync()
        {
            if (CanMerge)
                await _wc.UseExternalMergeToolAsync(_change);
        }

        /// <summary>対象リポジトリへの参照</summary>
        private Repository _repo = null;
        /// <summary>ワーキングコピーViewModelへの参照</summary>
        private WorkingCopy _wc = null;
        /// <summary>HEADコミット</summary>
        private Models.Commit _head = null;
        /// <summary>コンフリクトが発生した変更ファイル</summary>
        private Models.Change _change = null;
    }
}
