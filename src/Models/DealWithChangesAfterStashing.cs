using System.Collections.Generic;

namespace Komorebi.Models;

/// <summary>
/// スタッシュ後の変更の取り扱い方法を表すクラス。
/// スタッシュ作成後にワーキングツリーの変更をどう処理するかを定義する。
/// </summary>
public class DealWithChangesAfterStashing(string label, string desc)
{
    /// <summary>
    /// 処理方法のラベル名。
    /// </summary>
    public string Label { get; set; } = label;

    /// <summary>
    /// 処理方法の説明文。
    /// </summary>
    public string Desc { get; set; } = desc;

    /// <summary>
    /// サポートされているスタッシュ後の変更取り扱い方法の一覧。
    /// </summary>
    public static readonly List<DealWithChangesAfterStashing> Supported = [
        new ("Discard", "All (or selected) changes will be discarded"),
        new ("Keep Index", "Staged changes are left intact"),
        new ("Keep All", "All (or selected) changes are left intact"),
    ];
}
