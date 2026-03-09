namespace Komorebi.Models
{
    /// <summary>
    ///     変更一覧の表示モードを表す列挙型。
    /// </summary>
    public enum ChangeViewMode
    {
        /// <summary>
        ///     リスト形式で表示。
        /// </summary>
        List,

        /// <summary>
        ///     グリッド形式で表示。
        /// </summary>
        Grid,

        /// <summary>
        ///     ツリー形式で表示。
        /// </summary>
        Tree,
    }

    /// <summary>
    ///     ファイルの変更状態を表す列挙型。
    /// </summary>
    public enum ChangeState
    {
        /// <summary>
        ///     変更なし。
        /// </summary>
        None,

        /// <summary>
        ///     内容が変更された。
        /// </summary>
        Modified,

        /// <summary>
        ///     ファイルタイプが変更された。
        /// </summary>
        TypeChanged,

        /// <summary>
        ///     新規追加された。
        /// </summary>
        Added,

        /// <summary>
        ///     削除された。
        /// </summary>
        Deleted,

        /// <summary>
        ///     名前が変更された。
        /// </summary>
        Renamed,

        /// <summary>
        ///     コピーされた。
        /// </summary>
        Copied,

        /// <summary>
        ///     未追跡ファイル。
        /// </summary>
        Untracked,

        /// <summary>
        ///     コンフリクト状態。
        /// </summary>
        Conflicted,
    }

    /// <summary>
    ///     コンフリクトの理由を表す列挙型。
    /// </summary>
    public enum ConflictReason
    {
        /// <summary>
        ///     コンフリクトなし。
        /// </summary>
        None,

        /// <summary>
        ///     両側で削除された。
        /// </summary>
        BothDeleted,

        /// <summary>
        ///     自分側で追加された。
        /// </summary>
        AddedByUs,

        /// <summary>
        ///     相手側で削除された。
        /// </summary>
        DeletedByThem,

        /// <summary>
        ///     相手側で追加された。
        /// </summary>
        AddedByThem,

        /// <summary>
        ///     自分側で削除された。
        /// </summary>
        DeletedByUs,

        /// <summary>
        ///     両側で追加された。
        /// </summary>
        BothAdded,

        /// <summary>
        ///     両側で変更された。
        /// </summary>
        BothModified,
    }

    /// <summary>
    ///     amend（修正コミット）用の変更データを保持するクラス。
    /// </summary>
    public class ChangeDataForAmend
    {
        /// <summary>
        ///     ファイルモード（パーミッション）。
        /// </summary>
        public string FileMode { get; set; } = "";

        /// <summary>
        ///     オブジェクトのハッシュ値。
        /// </summary>
        public string ObjectHash { get; set; } = "";

        /// <summary>
        ///     親コミットのSHA。
        /// </summary>
        public string ParentSHA { get; set; } = "";
    }

    /// <summary>
    ///     ファイルの変更情報を表すクラス。
    ///     インデックスとワーキングツリーの両方の変更状態を保持する。
    /// </summary>
    public class Change
    {
        /// <summary>
        ///     インデックス（ステージング領域）での変更状態。
        /// </summary>
        public ChangeState Index { get; set; } = ChangeState.None;

        /// <summary>
        ///     ワーキングツリーでの変更状態。
        /// </summary>
        public ChangeState WorkTree { get; set; } = ChangeState.None;

        /// <summary>
        ///     変更されたファイルのパス。
        /// </summary>
        public string Path { get; set; } = "";

        /// <summary>
        ///     名前変更前の元のパス。
        /// </summary>
        public string OriginalPath { get; set; } = "";

        /// <summary>
        ///     amend用の追加データ。
        /// </summary>
        public ChangeDataForAmend DataForAmend { get; set; } = null;

        /// <summary>
        ///     コンフリクトの理由。
        /// </summary>
        public ConflictReason ConflictReason { get; set; } = ConflictReason.None;

        /// <summary>
        ///     コンフリクト状態かどうかを判定する。
        /// </summary>
        public bool IsConflicted => WorkTree == ChangeState.Conflicted;

        /// <summary>
        ///     コンフリクトの短縮マーカー文字列（例: "UU", "DD"）を取得する。
        /// </summary>
        public string ConflictMarker => CONFLICT_MARKERS[(int)ConflictReason];

        /// <summary>
        ///     コンフリクトの説明文を取得する。
        /// </summary>
        public string ConflictDesc => CONFLICT_DESCS[(int)ConflictReason];

        /// <summary>
        ///     ワーキングツリーの変更状態の説明文を取得する。
        /// </summary>
        public string WorkTreeDesc => TYPE_DESCS[(int)WorkTree];

        /// <summary>
        ///     インデックスの変更状態の説明文を取得する。
        /// </summary>
        public string IndexDesc => TYPE_DESCS[(int)Index];

        /// <summary>
        ///     変更状態を設定し、名前変更時のパス分割やクォート除去を行う。
        /// </summary>
        /// <param name="index">インデックスでの変更状態。</param>
        /// <param name="workTree">ワーキングツリーでの変更状態。</param>
        public void Set(ChangeState index, ChangeState workTree = ChangeState.None)
        {
            Index = index;
            WorkTree = workTree;

            // 名前変更の場合、パスを元パスと新パスに分割
            if (index == ChangeState.Renamed || workTree == ChangeState.Renamed)
            {
                var parts = Path.Split('\t', 2);
                if (parts.Length < 2)
                    parts = Path.Split(" -> ", 2);
                if (parts.Length == 2)
                {
                    OriginalPath = parts[0];
                    Path = parts[1];
                }
            }

            // ダブルクォートで囲まれたパスからクォートを除去
            if (Path.Length > 0 && Path[0] == '"')
                Path = Path.Substring(1, Path.Length - 2);

            if (!string.IsNullOrEmpty(OriginalPath) && OriginalPath[0] == '"')
                OriginalPath = OriginalPath.Substring(1, OriginalPath.Length - 2);
        }

        /// <summary>
        ///     変更状態の説明文配列。
        /// </summary>
        private static readonly string[] TYPE_DESCS =
        [
            "Unknown",
            "Modified",
            "Type Changed",
            "Added",
            "Deleted",
            "Renamed",
            "Copied",
            "Untracked",
            "Conflict"
        ];

        /// <summary>
        ///     コンフリクトマーカーの配列。
        /// </summary>
        private static readonly string[] CONFLICT_MARKERS =
        [
            string.Empty,
            "DD",
            "AU",
            "UD",
            "UA",
            "DU",
            "AA",
            "UU"
        ];

        /// <summary>
        ///     コンフリクト説明文の配列。
        /// </summary>
        private static readonly string[] CONFLICT_DESCS =
        [
            string.Empty,
            "Both deleted",
            "Added by us",
            "Deleted by them",
            "Added by them",
            "Deleted by us",
            "Both added",
            "Both modified"
        ];
    }
}
