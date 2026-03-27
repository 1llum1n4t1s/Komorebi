using System;
using System.Threading.Tasks;

using Avalonia.Collections;

using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>
///     ランチャーの個別タブページを表すViewModel。
///     リポジトリまたはWelcome画面のデータ、ポップアップ管理、通知、ダーティ状態を保持する。
/// </summary>
public class LauncherPage : ObservableObject
{
    /// <summary>タブに関連付けられたリポジトリノード。</summary>
    public RepositoryNode Node
    {
        get => _node;
        set => SetProperty(ref _node, value);
    }

    /// <summary>タブのデータ（RepositoryまたはWelcomeインスタンス）。</summary>
    public object Data
    {
        get => _data;
        set => SetProperty(ref _data, value);
    }

    /// <summary>タブのダーティ（未保存変更）状態。変更インジケーター表示に使用する。</summary>
    public Models.DirtyState DirtyState
    {
        get => _dirtyState;
        private set => SetProperty(ref _dirtyState, value);
    }

    /// <summary>現在表示中のポップアップダイアログ。nullの場合はダイアログなし。</summary>
    public Popup Popup
    {
        get => _popup;
        set => SetProperty(ref _popup, value);
    }

    /// <summary>このタブに対する通知メッセージの一覧。</summary>
    public AvaloniaList<Models.Notification> Notifications
    {
        get;
        set;
    } = new AvaloniaList<Models.Notification>();

    /// <summary>デフォルトコンストラクタ。Welcome画面を表示する空タブを作成する。</summary>
    public LauncherPage()
    {
        _node = new RepositoryNode() { Id = Guid.NewGuid().ToString() };
        _data = Welcome.Instance;

        // 新しいWelcomeページを開く前に検索フィルタをクリア
        Welcome.Instance.ClearSearchFilter();
    }

    /// <summary>リポジトリ用コンストラクタ。指定されたノードとリポジトリでタブを作成する。</summary>
    public LauncherPage(RepositoryNode node, Repository repo)
    {
        _node = node;
        _data = repo;
    }

    /// <summary>全ての通知をクリアする。</summary>
    public void ClearNotifications()
    {
        Notifications.Clear();
    }

    /// <summary>リポジトリのパスをクリップボードにコピーする。</summary>
    public async Task CopyPathAsync()
    {
        if (_node.IsRepository)
            await App.CopyTextAsync(_node.Id);
    }

    /// <summary>
    ///     ダーティ状態フラグを変更する。
    ///     removeがtrueの場合はフラグを除去し、falseの場合は追加する。
    /// </summary>
    public void ChangeDirtyState(Models.DirtyState flag, bool remove)
    {
        var state = _dirtyState;
        if (remove)
        {
            if (state.HasFlag(flag))
                state -= flag;
        }
        else
        {
            state |= flag;
        }

        DirtyState = state;
    }

    /// <summary>新しいポップアップを作成できるかどうかを返す。実行中のポップアップがなければtrue。</summary>
    public bool CanCreatePopup()
    {
        return _popup is not { InProgress: true };
    }

    /// <summary>
    ///     ポップアップの確認処理を実行する。バリデーション後にSure()を呼び、
    ///     成功すればポップアップを閉じる。例外はログに記録する。
    /// </summary>
    public async Task ProcessPopupAsync()
    {
        if (_popup is { InProgress: false } dump)
        {
            // バリデーションチェック
            if (!dump.Check())
                return;

            dump.InProgress = true;

            try
            {
                var finished = await dump.Sure();
                if (finished)
                {
                    dump.Cleanup();
                    Popup = null;
                }
            }
            catch (Exception e)
            {
                App.LogException(e);
            }

            dump.InProgress = false;
        }
    }

    /// <summary>ポップアップをキャンセルして閉じる。実行中の場合はキャンセルしない。</summary>
    public void CancelPopup()
    {
        if (_popup is null || _popup.InProgress)
            return;

        _popup?.Cleanup();
        Popup = null;
    }

    private RepositoryNode _node = null;                              // リポジトリノード
    private object _data = null;                                      // タブデータ（Repository or Welcome）
    private Models.DirtyState _dirtyState = Models.DirtyState.None;   // ダーティ状態フラグ
    private Popup _popup = null;                                      // 表示中のポップアップ
}
