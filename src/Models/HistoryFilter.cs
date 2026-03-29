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
    /// フィルター対象のパターン文字列（ブランチ名、タグ名等）
    /// </summary>
    public string Pattern
    {
        get => _pattern;
        set => SetProperty(ref _pattern, value);
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
