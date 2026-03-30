using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Komorebi.Models;

/// <summary>
/// 対話的リベースのアクション種別
/// </summary>
public enum InteractiveRebaseAction
{
    /// <summary>コミットをそのまま適用</summary>
    Pick,
    /// <summary>コミットを適用後、編集のために一時停止</summary>
    Edit,
    /// <summary>コミットメッセージを修正</summary>
    Reword,
    /// <summary>前のコミットに統合（メッセージも結合）</summary>
    Squash,
    /// <summary>前のコミットに統合（メッセージは破棄）</summary>
    Fixup,
    /// <summary>コミットを削除</summary>
    Drop,
}

/// <summary>
/// 対話的リベースのコミット状態種別
/// </summary>
public enum InteractiveRebasePendingType
{
    /// <summary>状態なし</summary>
    None = 0,
    /// <summary>リベース対象</summary>
    Target,
    /// <summary>処理待ち</summary>
    Pending,
    /// <summary>無視</summary>
    Ignore,
    /// <summary>最後のコミット</summary>
    Last,
}

/// <summary>
/// 対話的リベース中のコミット情報
/// </summary>
public class InteractiveCommit
{
    public Commit Commit { get; set; } = new Commit();
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 対話的リベースの個別ジョブ（1コミット分の操作定義）
/// </summary>
public class InteractiveRebaseJob
{
    public string SHA { get; set; } = string.Empty;
    public InteractiveRebaseAction Action { get; set; } = InteractiveRebaseAction.Pick;
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 対話的リベースのジョブコレクション。ORIG_HEADとOnto情報も保持する。
/// </summary>
public class InteractiveRebaseJobCollection
{
    public string OrigHead { get; set; } = string.Empty;
    public string Onto { get; set; } = string.Empty;
    public List<InteractiveRebaseJob> Jobs { get; set; } = [];
}
