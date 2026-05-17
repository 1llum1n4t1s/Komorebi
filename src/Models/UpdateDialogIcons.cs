using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using VelopackUpdateDialog;

namespace Komorebi.Models;

/// <summary>
/// VelopackUpdateDialog.Avalonia のベクタアイコンを Komorebi の Icons.axaml リソースへ接続する。
/// 既存のメニュー / ツールバー アイコンと統一感を持たせるため、ライブラリのデフォルトではなくホスト側のアイコンを使う。
/// </summary>
public sealed class UpdateDialogIcons : IUpdateDialogIcons
{
    /// <summary>シングルトン インスタンス。</summary>
    public static readonly UpdateDialogIcons Instance = new();

    /// <inheritdoc />
    public Geometry SoftwareUpdate => GetGeometry("Icons.SoftwareUpdate");

    /// <inheritdoc />
    public Geometry Info => GetGeometry("Icons.Info");

    /// <inheritdoc />
    public Geometry Download => GetGeometry("Icons.Pull");

    /// <inheritdoc />
    public Geometry Ignore => GetGeometry("Icons.File.Ignore");

    /// <inheritdoc />
    public Geometry Error => GetGeometry("Icons.Error");

    /// <summary>
    /// リソースキーから <see cref="Geometry"/>（実体は StreamGeometry）を取得する。
    /// 未登録キーの場合は空の Geometry を返してダイアログ全体の描画失敗を避ける。
    /// </summary>
    private static Geometry GetGeometry(string key)
    {
        if (Application.Current?.FindResource(key) is Geometry geo)
            return geo;

        return s_empty;
    }

    private static readonly Geometry s_empty = Geometry.Parse("M0,0");
}
