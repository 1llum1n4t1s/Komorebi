using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>
/// コマンドパレットの基底クラス。
/// ランチャーウィンドウにコマンドパレットを表示・非表示するための共通機能を提供する。
/// </summary>
public class ICommandPalette : ObservableObject
{
    /// <summary>
    /// コマンドパレットを開く。ランチャーのCommandPaletteプロパティにこのインスタンスを設定する。
    /// </summary>
    public void Open()
    {
        var host = App.GetLauncher();
        if (host is not null)
            host.CommandPalette = this;
    }

    /// <summary>
    /// コマンドパレットを閉じる。ランチャーのCommandPaletteプロパティをnullに設定する。
    /// </summary>
    public void Close()
    {
        var host = App.GetLauncher();
        if (host is not null)
            host.CommandPalette = null;
    }
}
