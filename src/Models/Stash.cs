using System.Collections.Generic;

namespace Komorebi.Models;

/// <summary>
/// Git stash（退避）のエントリ情報を保持するクラス
/// </summary>
public class Stash
{
    /// <summary>スタッシュの参照名（例: "stash@{0}"）</summary>
    public string Name { get; set; } = "";
    /// <summary>スタッシュコミットのSHAハッシュ</summary>
    public string SHA { get; set; } = "";
    /// <summary>親コミットのSHAリスト</summary>
    public List<string> Parents { get; set; } = [];
    /// <summary>作成日時（UNIXタイムスタンプ）</summary>
    public ulong Time { get; set; } = 0;
    /// <summary>スタッシュメッセージ（複数行の場合あり）</summary>
    public string Message { get; set; } = "";
    /// <summary>メッセージの最初の行（件名）</summary>
    public string Subject => (Message ?? "").Split('\n', 2)[0].Trim();
}
