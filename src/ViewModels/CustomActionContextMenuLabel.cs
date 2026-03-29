namespace Komorebi.ViewModels;

/// <summary>
/// カスタムアクションのコンテキストメニューラベル。
/// カスタムアクション名とグローバル/リポジトリローカルの区別を保持する。
/// </summary>
/// <param name="name">カスタムアクション名</param>
/// <param name="isGlobal">グローバルアクションかどうか</param>
public class CustomActionContextMenuLabel(string name, bool isGlobal)
{
    /// <summary>カスタムアクション名</summary>
    public string Name { get; set; } = name;
    /// <summary>グローバルアクションかどうか</summary>
    public bool IsGlobal { get; set; } = isGlobal;
}
