using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// コマンドパレットに表示されるカスタムアクションのエントリ。
/// </summary>
public class ExecuteCustomActionCommandPaletteCmd
{
    /// <summary>カスタムアクションの定義。</summary>
    public Models.CustomAction Action { get; set; }
    /// <summary>グローバルアクションかどうか。</summary>
    public bool IsGlobal { get; set; }
    /// <summary>アクション名。</summary>
    public string Name { get => Action.Name; }

    /// <summary>コンストラクタ。アクション定義とグローバルフラグを指定する。</summary>
    public ExecuteCustomActionCommandPaletteCmd(Models.CustomAction action, bool isGlobal)
    {
        Action = action;
        IsGlobal = isGlobal;
    }
}

/// <summary>
/// カスタムアクション実行用のコマンドパレットViewModel。
/// フィルタ入力によるアクションの絞り込みと選択・実行を提供する。
/// </summary>
public class ExecuteCustomActionCommandPalette : ICommandPalette
{
    /// <summary>
    /// フィルタ適用後の表示アクションリスト。
    /// </summary>
    public List<ExecuteCustomActionCommandPaletteCmd> VisibleActions
    {
        get => _visibleActions;
        private set => SetProperty(ref _visibleActions, value);
    }

    /// <summary>
    /// 現在選択されているアクション。
    /// </summary>
    public ExecuteCustomActionCommandPaletteCmd Selected
    {
        get => _selected;
        set => SetProperty(ref _selected, value);
    }

    /// <summary>
    /// フィルタ文字列。変更時に表示アクションを更新する。
    /// </summary>
    public string Filter
    {
        get => _filter;
        set
        {
            if (SetProperty(ref _filter, value))
                UpdateVisibleActions();
        }
    }

    /// <summary>
    /// コンストラクタ。リポジトリスコープのカスタムアクションを取得してソートする。
    /// </summary>
    public ExecuteCustomActionCommandPalette(Repository repo)
    {
        _repo = repo;

        var actions = repo.GetCustomActions(Models.CustomActionScope.Repository);
        foreach (var (action, menu) in actions)
            _actions.Add(new(action, menu.IsGlobal));

        if (_actions.Count > 0)
        {
            _actions.Sort((l, r) =>
            {
                if (l.IsGlobal != r.IsGlobal)
                    return l.IsGlobal ? -1 : 1;

                return l.Name.CompareTo(r.Name, StringComparison.OrdinalIgnoreCase);
            });

            _visibleActions = _actions;
            _selected = _actions[0];
        }
    }

    /// <summary>
    /// フィルタをクリアする。
    /// </summary>
    public void ClearFilter()
    {
        Filter = string.Empty;
    }

    /// <summary>
    /// 選択されたカスタムアクションを実行する。
    /// </summary>
    public async Task ExecAsync()
    {
        _actions.Clear();
        _visibleActions.Clear();
        Close();

        if (_selected is not null)
            await _repo.ExecCustomActionAsync(_selected.Action, null);
    }

    /// <summary>
    /// フィルタ文字列に基づいて表示アクションリストを更新する。
    /// </summary>
    private void UpdateVisibleActions()
    {
        var filter = _filter?.Trim();
        if (string.IsNullOrEmpty(filter))
        {
            VisibleActions = _actions;
            return;
        }

        List<ExecuteCustomActionCommandPaletteCmd> visible = [];
        foreach (var act in _actions)
        {
            if (act.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                visible.Add(act);
        }

        var autoSelected = _selected;
        if (visible.Count == 0)
            autoSelected = null;
        else if (_selected is null || !visible.Contains(_selected))
            autoSelected = visible[0];

        VisibleActions = visible;
        Selected = autoSelected;
    }

    private Repository _repo; // 対象リポジトリ
    private List<ExecuteCustomActionCommandPaletteCmd> _actions = []; // 全アクションリスト
    private List<ExecuteCustomActionCommandPaletteCmd> _visibleActions = []; // フィルタ適用後のリスト
    private ExecuteCustomActionCommandPaletteCmd _selected = null; // 選択中のアクション
    private string _filter; // フィルタ文字列
}
