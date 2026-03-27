using System;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>
///     ワークスペース設定のViewModel。
///     ワークスペースの追加・削除・並び替えを管理する。
///     アクティブなワークスペースは削除不可。
/// </summary>
public class ConfigureWorkspace : ObservableObject
{
    /// <summary>
    ///     ワークスペースの一覧。
    /// </summary>
    public AvaloniaList<Workspace> Workspaces
    {
        get;
    }

    /// <summary>
    ///     選択中のワークスペース。変更時に削除可能フラグを更新する。
    /// </summary>
    public Workspace Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
                // アクティブでないワークスペースのみ削除可能とする
                CanDeleteSelected = value is { IsActive: false };
        }
    }

    /// <summary>
    ///     選択中のワークスペースが削除可能かどうか。
    /// </summary>
    public bool CanDeleteSelected
    {
        get => _canDeleteSelected;
        private set => SetProperty(ref _canDeleteSelected, value);
    }

    /// <summary>
    ///     コンストラクタ。設定からワークスペース一覧を読み込んで初期化する。
    /// </summary>
    public ConfigureWorkspace()
    {
        Workspaces = new(Preferences.Instance.Workspaces);
    }

    /// <summary>
    ///     新しいワークスペースを追加する。現在日時をデフォルト名として使用する。
    /// </summary>
    public void Add()
    {
        // 現在日時をデフォルト名として新しいワークスペースを作成する
        var workspace = new Workspace() { Name = $"Unnamed {DateTime.Now:yyyy-MM-dd HH:mm:ss}" };
        // 設定と表示リストの両方に追加する
        Preferences.Instance.Workspaces.Add(workspace);
        Workspaces.Add(workspace);
        Selected = workspace;
    }

    /// <summary>
    ///     選択中のワークスペースを削除する。アクティブなワークスペースは削除不可。
    /// </summary>
    public void Delete()
    {
        if (_selected is null || _selected.IsActive)
            return;

        // 設定と表示リストの両方から削除する
        Preferences.Instance.Workspaces.Remove(_selected);
        Workspaces.Remove(_selected);
    }

    /// <summary>
    ///     選択中のワークスペースを1つ上に移動する。
    /// </summary>
    public void MoveSelectedUp()
    {
        if (_selected is null)
            return;

        var idx = Workspaces.IndexOf(_selected);
        if (idx == 0)
            return;

        // 表示リストと設定の両方で順序を入れ替える
        Workspaces.Move(idx - 1, idx);

        Preferences.Instance.Workspaces.RemoveAt(idx);
        Preferences.Instance.Workspaces.Insert(idx - 1, _selected);
    }

    /// <summary>
    ///     選択中のワークスペースを1つ下に移動する。
    /// </summary>
    public void MoveSelectedDown()
    {
        if (_selected is null)
            return;

        var idx = Workspaces.IndexOf(_selected);
        if (idx == Workspaces.Count - 1)
            return;

        // 表示リストと設定の両方で順序を入れ替える
        Workspaces.Move(idx + 1, idx);

        Preferences.Instance.Workspaces.RemoveAt(idx);
        Preferences.Instance.Workspaces.Insert(idx + 1, _selected);
    }

    /// <summary>選択中のワークスペース</summary>
    private Workspace _selected = null;
    /// <summary>選択中のワークスペースが削除可能かどうか</summary>
    private bool _canDeleteSelected = false;
}
