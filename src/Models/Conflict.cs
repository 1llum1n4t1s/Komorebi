using System.Collections.Generic;

namespace Komorebi.Models
{
    /// <summary>
    ///     コンフリクト解決画面のパネルタイプを表す列挙型。
    /// </summary>
    public enum ConflictPanelType
    {
        /// <summary>
        ///     自分側（ours）のパネル。
        /// </summary>
        Ours,

        /// <summary>
        ///     相手側（theirs）のパネル。
        /// </summary>
        Theirs,

        /// <summary>
        ///     解決結果のパネル。
        /// </summary>
        Result
    }

    /// <summary>
    ///     コンフリクトの解決方法を表す列挙型。
    /// </summary>
    public enum ConflictResolution
    {
        /// <summary>
        ///     未解決。
        /// </summary>
        None,

        /// <summary>
        ///     自分側の変更を使用。
        /// </summary>
        UseOurs,

        /// <summary>
        ///     相手側の変更を使用。
        /// </summary>
        UseTheirs,

        /// <summary>
        ///     両方を使用（自分側を先に配置）。
        /// </summary>
        UseBothMineFirst,

        /// <summary>
        ///     両方を使用（相手側を先に配置）。
        /// </summary>
        UseBothTheirsFirst,
    }

    /// <summary>
    ///     コンフリクト行の種類を表す列挙型。
    /// </summary>
    public enum ConflictLineType
    {
        /// <summary>
        ///     種類なし。
        /// </summary>
        None,

        /// <summary>
        ///     共通行（コンフリクトなし）。
        /// </summary>
        Common,

        /// <summary>
        ///     コンフリクトマーカー行。
        /// </summary>
        Marker,

        /// <summary>
        ///     自分側の行。
        /// </summary>
        Ours,

        /// <summary>
        ///     相手側の行。
        /// </summary>
        Theirs,
    }

    /// <summary>
    ///     コンフリクト行の状態を表す列挙型。
    /// </summary>
    public enum ConflictLineState
    {
        /// <summary>
        ///     通常状態。
        /// </summary>
        Normal,

        /// <summary>
        ///     コンフリクトブロックの開始行。
        /// </summary>
        ConflictBlockStart,

        /// <summary>
        ///     コンフリクトブロック内の行。
        /// </summary>
        ConflictBlock,

        /// <summary>
        ///     コンフリクトブロックの終了行。
        /// </summary>
        ConflictBlockEnd,

        /// <summary>
        ///     解決済みブロックの開始行。
        /// </summary>
        ResolvedBlockStart,

        /// <summary>
        ///     解決済みブロック内の行。
        /// </summary>
        ResolvedBlock,

        /// <summary>
        ///     解決済みブロックの終了行。
        /// </summary>
        ResolvedBlockEnd,
    }

    /// <summary>
    ///     コンフリクトファイルの1行を表すクラス。
    /// </summary>
    public class ConflictLine
    {
        /// <summary>
        ///     行の種類。
        /// </summary>
        public ConflictLineType Type { get; set; } = ConflictLineType.None;

        /// <summary>
        ///     行の内容テキスト。
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        ///     行番号の文字列表現。
        /// </summary>
        public string LineNumber { get; set; } = string.Empty;

        /// <summary>
        ///     デフォルトコンストラクタ。
        /// </summary>
        public ConflictLine()
        {
        }

        /// <summary>
        ///     種類と内容を指定してインスタンスを初期化する。
        /// </summary>
        /// <param name="type">行の種類。</param>
        /// <param name="content">行の内容。</param>
        public ConflictLine(ConflictLineType type, string content)
        {
            Type = type;
            Content = content;
        }

        /// <summary>
        ///     種類、内容、行番号を指定してインスタンスを初期化する。
        /// </summary>
        /// <param name="type">行の種類。</param>
        /// <param name="content">行の内容。</param>
        /// <param name="lineNumber">行番号。</param>
        public ConflictLine(ConflictLineType type, string content, int lineNumber)
        {
            Type = type;
            Content = content;
            LineNumber = lineNumber.ToString();
        }
    }

    /// <summary>
    ///     選択されたコンフリクトチャンクの位置・状態情報を保持するレコード。
    /// </summary>
    /// <param name="Y">チャンクのY座標。</param>
    /// <param name="Height">チャンクの高さ。</param>
    /// <param name="ConflictIndex">コンフリクト領域のインデックス。</param>
    /// <param name="Panel">所属パネルの種類。</param>
    /// <param name="IsResolved">解決済みかどうか。</param>
    public record ConflictSelectedChunk(
        double Y,
        double Height,
        int ConflictIndex,
        ConflictPanelType Panel,
        bool IsResolved
    );

    /// <summary>
    ///     ファイル内の1つのコンフリクト領域を表すクラス。
    /// </summary>
    public class ConflictRegion
    {
        /// <summary>
        ///     元ファイルでのコンフリクト開始行番号。
        /// </summary>
        public int StartLineInOriginal { get; set; }

        /// <summary>
        ///     元ファイルでのコンフリクト終了行番号。
        /// </summary>
        public int EndLineInOriginal { get; set; }

        /// <summary>
        ///     コンフリクト開始マーカー（デフォルト: "&lt;&lt;&lt;&lt;&lt;&lt;&lt;"）。
        /// </summary>
        public string StartMarker { get; set; } = "<<<<<<<";

        /// <summary>
        ///     コンフリクト区切りマーカー（デフォルト: "======="）。
        /// </summary>
        public string SeparatorMarker { get; set; } = "=======";

        /// <summary>
        ///     コンフリクト終了マーカー（デフォルト: "&gt;&gt;&gt;&gt;&gt;&gt;&gt;"）。
        /// </summary>
        public string EndMarker { get; set; } = ">>>>>>>";

        /// <summary>
        ///     自分側のコンフリクト内容。
        /// </summary>
        public List<string> OursContent { get; set; } = new();

        /// <summary>
        ///     相手側のコンフリクト内容。
        /// </summary>
        public List<string> TheirsContent { get; set; } = new();

        /// <summary>
        ///     コンフリクトが解決済みかどうか。
        /// </summary>
        public bool IsResolved { get; set; } = false;

        /// <summary>
        ///     採用された解決方法。
        /// </summary>
        public ConflictResolution ResolutionType { get; set; } = ConflictResolution.None;
    }
}
