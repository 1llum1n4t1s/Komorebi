using System.Collections.Generic;

namespace Komorebi.Models;

/// <summary>
/// git blameの各行に対応するコミット情報を保持するクラス。
/// </summary>
public class BlameLineInfo
{
    /// <summary>
    /// 同一コミットグループの先頭行かどうか。
    /// </summary>
    public bool IsFirstInGroup { get; set; } = false;

    /// <summary>
    /// この行を最後に変更したコミットのSHA。
    /// </summary>
    public string CommitSHA { get; set; } = string.Empty;

    /// <summary>
    /// この行が属するファイルパス。
    /// </summary>
    public string File { get; set; } = string.Empty;

    /// <summary>
    /// この行を最後に変更した著者名。
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// 変更日時のUnixタイムスタンプ。
    /// </summary>
    public ulong Timestamp { get; set; } = 0;
}

/// <summary>
/// git blameの解析結果データを保持するクラス。
/// </summary>
public class BlameData
{
    /// <summary>
    /// 対象ファイルがバイナリかどうか。
    /// </summary>
    public bool IsBinary { get; set; } = false;

    /// <summary>
    /// ファイルの全文内容。
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 各行に対応するblame情報のリスト。
    /// </summary>
    public List<BlameLineInfo> LineInfos { get; set; } = [];
}
