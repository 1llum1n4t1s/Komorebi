using Avalonia.Media;

namespace Komorebi.Models;

/// <summary>
/// git resetコマンドのリセットモードを表すクラス。
/// ワーキングツリーとインデックスへの影響度合いを定義する。
/// </summary>
public class ResetMode(string n, string d, string a, string k, IBrush b)
{
    /// <summary>
    /// サポートされているリセットモードの一覧。
    /// </summary>
    public static readonly ResetMode[] Supported =
    [
        new ResetMode("Soft", "Keep all changes. Stage differences", "--soft", "S", Brushes.Green),
        new ResetMode("Mixed", "Keep all changes. Unstage differences", "--mixed", "M", Brushes.Orange),
        new ResetMode("Merge", "Reset while keeping unmerged changes", "--merge", "G", Brushes.Purple),
        new ResetMode("Keep", "Reset while keeping local modifications", "--keep", "K", Brushes.Purple),
        new ResetMode("Hard", "Discard all changes", "--hard", "H", Brushes.Red),
    ];

    /// <summary>
    /// モードの表示名。
    /// </summary>
    public string Name { get; set; } = n;

    /// <summary>
    /// モードの説明文。
    /// </summary>
    public string Desc { get; set; } = d;

    /// <summary>
    /// gitコマンドに渡す引数文字列。
    /// </summary>
    public string Arg { get; set; } = a;

    /// <summary>
    /// モードのキーボードショートカットキー。
    /// </summary>
    public string Key { get; set; } = k;

    /// <summary>
    /// UIで使用するモードの表示色。
    /// </summary>
    public IBrush Color { get; set; } = b;
}
