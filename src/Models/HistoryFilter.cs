using System.Linq;

using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.Models;

/// <summary>
/// 履歴フィルターの対象種別
/// </summary>
public enum FilterType
{
    /// <summary>ローカルブランチ</summary>
    LocalBranch = 0,
    /// <summary>ローカルブランチフォルダ</summary>
    LocalBranchFolder,
    /// <summary>リモートブランチ</summary>
    RemoteBranch,
    /// <summary>リモートブランチフォルダ</summary>
    RemoteBranchFolder,
    /// <summary>タグ</summary>
    Tag,
}

/// <summary>
/// フィルターモード（含む/除外/なし）
/// </summary>
public enum FilterMode
{
    /// <summary>フィルターなし</summary>
    None = 0,
    /// <summary>指定パターンを含む</summary>
    Included,
    /// <summary>指定パターンを除外する</summary>
    Excluded,
}

/// <summary>
/// コミット履歴のフィルター条件を保持するクラス
/// </summary>
public class HistoryFilter : ObservableObject
{
    /// <summary>
    /// フィルター対象のパターン文字列（ブランチ名、タグ名等）。
    /// JSON deserialize 経由で悪意ある Pattern が入ると
    /// BuildHistoryParams で git CLI 引数文字列に補間されて引数注入される脆弱性があるため、
    /// setter で `"`, `\r`, `\n`, NUL 文字を弾く。これにより `.git/komorebi.uistates` 経由の
    /// `--output=...` などの引数追加攻撃を遮断。
    /// </summary>
    public string Pattern
    {
        get => _pattern;
        set => SetProperty(ref _pattern, SanitizePattern(value));
    }

    /// <summary>
    /// Pattern 値のサニタイズ: 引数境界破壊文字 (`"`, `\r`, `\n`, NUL) を除去 + 長さ制限。
    /// + P1#25 由来。
    ///
    /// 長さ制限の理由 (P1#25): JSON deserialize 経由で巨大 Pattern (MB 級) が入ると、
    /// BuildHistoryParams で git CLI 引数組み立て時の文字列補間が CPU/メモリを消耗する
    /// (.NET の string concatenation で O(n²) 的にコピーが発生)。Git ref 名は仕様上
    /// 255 byte 程度が現実的な上限なので、1024 文字でクランプしても通常利用には影響なし。
    /// </summary>
    private const int MaxPatternLength = 1024;

    private static string SanitizePattern(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        // P1#25: 長さ制限を先に適用 (LINQ Where で全走査する前に切り詰める)
        if (value.Length > MaxPatternLength)
            value = value[..MaxPatternLength];

        // 引数境界破壊文字を除去する。Pattern は git ref 文法 (英数+/-.) しか想定していないので
        // これらを除いても正常利用には影響しない。
        if (value.IndexOfAny(['"', '\r', '\n', '\0']) < 0)
            return value;
        return new string([.. value.Where(c => c != '"' && c != '\r' && c != '\n' && c != '\0')]);
    }

    /// <summary>
    /// フィルター対象の種別（ブランチ/タグ等）
    /// </summary>
    public FilterType Type
    {
        get;
        set;
    } = FilterType.LocalBranch;

    /// <summary>
    /// フィルターモード（含む/除外/なし）
    /// </summary>
    public FilterMode Mode
    {
        get => _mode;
        set => SetProperty(ref _mode, value);
    }

    /// <summary>
    /// ブランチフィルターかどうか（タグでなければtrue）
    /// </summary>
    public bool IsBranch
    {
        get => Type != FilterType.Tag;
    }

    /// <summary>デフォルトコンストラクタ</summary>
    public HistoryFilter()
    {
    }

    /// <summary>
    /// パターン、種別、モードを指定して初期化する
    /// </summary>
    /// <param name="pattern">フィルターパターン</param>
    /// <param name="type">フィルター対象の種別</param>
    /// <param name="mode">フィルターモード</param>
    public HistoryFilter(string pattern, FilterType type, FilterMode mode)
    {
        _pattern = pattern;
        _mode = mode;
        Type = type;
    }

    /// <summary>フィルターパターンのバッキングフィールド</summary>
    private string _pattern = string.Empty;
    /// <summary>フィルターモードのバッキングフィールド</summary>
    private FilterMode _mode = FilterMode.None;
}
