using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>
/// カスタムアクションコントロール設定のViewModel。
/// カスタムアクションに使用するUIコントロール（テキストボックス等）の追加・削除・並び替えを管理する。
/// </summary>
public class ConfigureCustomActionControls : ObservableObject
{
    /// <summary>
    /// カスタムアクションコントロールのリスト。
    /// </summary>
    public AvaloniaList<Models.CustomActionControl> Controls
    {
        get;
    }

    /// <summary>
    /// 現在編集中のカスタムアクションコントロール。
    /// </summary>
    public Models.CustomActionControl Edit
    {
        get => _edit;
        set => SetProperty(ref _edit, value);
    }

    /// <summary>
    /// コンストラクタ。カスタムアクションコントロールリストを受け取って初期化する。
    /// </summary>
    /// <param name="controls">カスタムアクションコントロールのリスト</param>
    public ConfigureCustomActionControls(AvaloniaList<Models.CustomActionControl> controls)
    {
        Controls = controls;
    }

    /// <summary>
    /// 新しいカスタムアクションコントロールを追加する。
    /// デフォルトで「Unnamed」という名前のテキストボックスを作成し、編集対象に設定する。
    /// </summary>
    public void Add()
    {
        // デフォルト設定で新しいコントロールを作成する
        var added = new Models.CustomActionControl()
        {
            Label = "Unnamed",
            Type = Models.CustomActionControlType.TextBox
        };

        // リストに追加して編集対象に設定する
        Controls.Add(added);
        Edit = added;
    }

    /// <summary>
    /// 現在編集中のカスタムアクションコントロールを削除する。
    /// </summary>
    public void Remove()
    {
        if (_edit is null)
            return;

        // リストから削除して編集対象をクリアする
        Controls.Remove(_edit);
        Edit = null;
    }

    /// <summary>
    /// 現在編集中のコントロールを1つ上に移動する。
    /// </summary>
    public void MoveUp()
    {
        if (_edit is null)
            return;

        var idx = Controls.IndexOf(_edit);
        // 先頭でない場合のみ上に移動する
        if (idx > 0)
            Controls.Move(idx - 1, idx);
    }

    /// <summary>
    /// 現在編集中のコントロールを1つ下に移動する。
    /// </summary>
    public void MoveDown()
    {
        if (_edit is null)
            return;

        var idx = Controls.IndexOf(_edit);
        // 末尾でない場合のみ下に移動する
        if (idx < Controls.Count - 1)
            Controls.Move(idx + 1, idx);
    }

    /// <summary>現在編集中のコントロール</summary>
    private Models.CustomActionControl _edit;
}
