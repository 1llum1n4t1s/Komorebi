using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.Models;

/// <summary>
///     履歴フィルターの対象種別
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
///     フィルターモード（含む/除外/なし）
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
///     コミット履歴のフィルター条件を保持するクラス
/// </summary>
public class HistoryFilter : ObservableObject
{
    public string Pattern
    {
        get => _pattern;
        set => SetProperty(ref _pattern, value);
    }

    public FilterType Type
    {
        get;
        set;
    } = FilterType.LocalBranch;

    public FilterMode Mode
    {
        get => _mode;
        set => SetProperty(ref _mode, value);
    }

    public bool IsBranch
    {
        get => Type != FilterType.Tag;
    }

    public HistoryFilter()
    {
    }

    public HistoryFilter(string pattern, FilterType type, FilterMode mode)
    {
        _pattern = pattern;
        _mode = mode;
        Type = type;
    }

    private string _pattern = string.Empty;
    private FilterMode _mode = FilterMode.None;
}
