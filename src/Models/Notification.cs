using System;

namespace Komorebi.Models;

/// <summary>
/// UIに表示する通知メッセージを保持するクラス
/// </summary>
public class Notification
{
    /// <summary>エラー通知かどうか</summary>
    public bool IsError { get; set; } = false;
    /// <summary>通知メッセージの内容</summary>
    public string Message { get; set; } = string.Empty;
    /// <summary>翻訳済みのヒント（対処法）。空文字列の場合はヒントなし。</summary>
    public string Hint { get; set; } = string.Empty;
    /// <summary>アクションボタンのテキスト。空文字列の場合はボタンなし。</summary>
    public string ActionLabel { get; set; } = string.Empty;
    /// <summary>アクションボタン押下時に実行するコールバック。</summary>
    public Action ActionCallback { get; set; }
}
