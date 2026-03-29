namespace Komorebi.Models;

/// <summary>
/// 変更ファイル一覧の表示モードを表す列挙型。
/// </summary>
public enum ChangeViewMode
{
    /// <summary>リスト表示。</summary>
    List,
    /// <summary>グリッド表示。</summary>
    Grid,
    /// <summary>ツリー表示。</summary>
    Tree,
}

/// <summary>
/// ファイルの変更状態を表す列挙型。git statusの出力に対応する。
/// </summary>
public enum ChangeState
{
    /// <summary>変更なし。</summary>
    None,
    /// <summary>内容が変更された。</summary>
    Modified,
    /// <summary>ファイルタイプが変更された。</summary>
    TypeChanged,
    /// <summary>新規追加された。</summary>
    Added,
    /// <summary>削除された。</summary>
    Deleted,
    /// <summary>リネームされた。</summary>
    Renamed,
    /// <summary>コピーされた。</summary>
    Copied,
    /// <summary>未追跡ファイル。</summary>
    Untracked,
    /// <summary>コンフリクト状態。</summary>
    Conflicted,
}

/// <summary>
/// コンフリクトの原因を表す列挙型。
/// </summary>
public enum ConflictReason
{
    /// <summary>コンフリクトなし。</summary>
    None,
    /// <summary>双方で削除された。</summary>
    BothDeleted,
    /// <summary>自分側で追加された。</summary>
    AddedByUs,
    /// <summary>相手側で削除された。</summary>
    DeletedByThem,
    /// <summary>相手側で追加された。</summary>
    AddedByThem,
    /// <summary>自分側で削除された。</summary>
    DeletedByUs,
    /// <summary>双方で追加された。</summary>
    BothAdded,
    /// <summary>双方で変更された。</summary>
    BothModified,
}

/// <summary>
/// amend時に必要なファイルのメタデータを保持するクラス。
/// </summary>
public class ChangeDataForAmend
{
    /// <summary>ファイルモード（パーミッション）。</summary>
    public string FileMode { get; set; } = "";
    /// <summary>オブジェクトのハッシュ値。</summary>
    public string ObjectHash { get; set; } = "";
    /// <summary>親コミットのSHA。</summary>
    public string ParentSHA { get; set; } = "";
}

/// <summary>
/// git statusで検出されたファイル変更を表すクラス。
/// インデックスとワークツリーの両方の状態を保持する。
/// </summary>
public class Change
{
    /// <summary>インデックス（ステージングエリア）上の変更状態。</summary>
    public ChangeState Index { get; set; } = ChangeState.None;
    /// <summary>ワークツリー上の変更状態。</summary>
    public ChangeState WorkTree { get; set; } = ChangeState.None;
    /// <summary>ファイルパス。</summary>
    public string Path { get; set; } = "";
    /// <summary>リネーム/コピー前の元パス。</summary>
    public string OriginalPath { get; set; } = "";
    /// <summary>amend用のメタデータ。</summary>
    public ChangeDataForAmend DataForAmend { get; set; } = null;
    /// <summary>コンフリクトの原因。</summary>
    public ConflictReason ConflictReason { get; set; } = ConflictReason.None;

    /// <summary>ワークツリーでコンフリクト状態かどうか。</summary>
    public bool IsConflicted => WorkTree == ChangeState.Conflicted;
    /// <summary>コンフリクトの短縮マーカー文字列（例: "UU", "AA"）。</summary>
    public string ConflictMarker => CONFLICT_MARKERS[(int)ConflictReason];
    /// <summary>コンフリクトの説明文。</summary>
    public string ConflictDesc => CONFLICT_DESCS[(int)ConflictReason];

    /// <summary>ワークツリー状態の説明文。</summary>
    public string WorkTreeDesc => TYPE_DESCS[(int)WorkTree];
    /// <summary>インデックス状態の説明文。</summary>
    public string IndexDesc => TYPE_DESCS[(int)Index];

    /// <summary>
    /// インデックスとワークツリーの変更状態を設定し、リネーム/コピー時のパス解析を行う。
    /// </summary>
    /// <param name="index">インデックスの変更状態。</param>
    /// <param name="workTree">ワークツリーの変更状態。</param>
    public void Set(ChangeState index, ChangeState workTree = ChangeState.None)
    {
        Index = index;
        WorkTree = workTree;

        // リネーム/コピーの場合、パスを元パスと新パスに分割する
        if (index == ChangeState.Renamed || index == ChangeState.Copied || workTree == ChangeState.Renamed)
        {
            // タブ区切り（git diff --name-status形式）を試行
            var parts = Path.Split('\t', 2);
            if (parts.Length < 2)
                // " -> "区切り（git status形式）を試行
                parts = Path.Split(" -> ", 2);
            if (parts.Length == 2)
            {
                OriginalPath = parts[0];
                Path = parts[1];
            }
        }

        // ダブルクォートで囲まれたパスからクォートを除去
        if (Path.Length > 1 && Path[0] == '"' && Path[^1] == '"')
            Path = Path[1..^1];

        if (OriginalPath.Length > 1 && OriginalPath[0] == '"' && OriginalPath[^1] == '"')
            OriginalPath = OriginalPath[1..^1];
    }

    /// <summary>変更状態の説明文配列。ChangeStateのインデックスに対応する。</summary>
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
    /// <summary>コンフリクト理由の短縮マーカー配列。ConflictReasonのインデックスに対応する。</summary>
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
    /// <summary>コンフリクト理由の説明文配列。ConflictReasonのインデックスに対応する。</summary>
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
