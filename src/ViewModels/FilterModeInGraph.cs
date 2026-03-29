using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>
/// コミットグラフのフィルタモードを管理するViewModel。
/// ブランチまたはタグに対して「含める」「除外する」「なし」のフィルタ状態を切り替える。
/// </summary>
public class FilterModeInGraph : ObservableObject
{
    /// <summary>
    /// フィルタに含まれているかどうか。trueで「含める」モード、falseで「なし」モードに切り替える。
    /// </summary>
    public bool IsFiltered
    {
        get => _mode == Models.FilterMode.Included;
        set => SetFilterMode(value ? Models.FilterMode.Included : Models.FilterMode.None);
    }

    /// <summary>
    /// フィルタから除外されているかどうか。trueで「除外」モード、falseで「なし」モードに切り替える。
    /// </summary>
    public bool IsExcluded
    {
        get => _mode == Models.FilterMode.Excluded;
        set => SetFilterMode(value ? Models.FilterMode.Excluded : Models.FilterMode.None);
    }

    /// <summary>
    /// コンストラクタ。リポジトリとフィルタ対象（ブランチまたはタグ）を指定する。
    /// 現在のフィルタモードをUI状態から取得する。
    /// </summary>
    public FilterModeInGraph(Repository repo, object target)
    {
        _repo = repo;
        _target = target;

        if (_target is Models.Branch b)
            _mode = _repo.UIStates.GetHistoryFilterMode(b.FullName);
        else if (_target is Models.Tag t)
            _mode = _repo.UIStates.GetHistoryFilterMode(t.Name);
    }

    /// <summary>
    /// フィルタモードを設定し、ブランチまたはタグに応じてリポジトリのフィルタを更新する。
    /// </summary>
    private void SetFilterMode(Models.FilterMode mode)
    {
        if (_mode != mode)
        {
            _mode = mode;

            // 対象の種類に応じてリポジトリのフィルタ設定を更新
            if (_target is Models.Branch branch)
                _repo.SetBranchFilterMode(branch, _mode, false, true);
            else if (_target is Models.Tag tag)
                _repo.SetTagFilterMode(tag, _mode);

            // 両プロパティの変更通知を発行
            OnPropertyChanged(nameof(IsFiltered));
            OnPropertyChanged(nameof(IsExcluded));
        }
    }

    private Repository _repo = null; // 対象リポジトリ
    private object _target = null; // フィルタ対象（ブランチまたはタグ）
    private Models.FilterMode _mode = Models.FilterMode.None; // 現在のフィルタモード
}
