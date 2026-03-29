using System.Text.RegularExpressions;

namespace Komorebi.Models;

/// <summary>
/// Git LFSオブジェクトの情報（OIDとサイズ）を保持するクラス
/// </summary>
public partial class LFSObject
{
    /// <summary>LFSポインタファイルのフォーマットを検証する正規表現</summary>
    [GeneratedRegex(@"^version https://git-lfs.github.com/spec/v\d+\r?\noid sha256:([0-9a-f]+)\r?\nsize (\d+)[\r\n]*$")]
    private static partial Regex REG_FORMAT();

    /// <summary>SHA256オブジェクトID</summary>
    public string Oid { get; set; } = string.Empty;

    /// <summary>オブジェクトのバイトサイズ</summary>
    public long Size { get; set; } = 0;

    /// <summary>
    /// LFSポインタファイルの内容をパースしてLFSObjectを生成する
    /// </summary>
    /// <param name="content">LFSポインタファイルの内容</param>
    /// <returns>パース成功時はLFSObject、失敗時はnull</returns>
    public static LFSObject Parse(string content)
    {
        var match = REG_FORMAT().Match(content);
        if (match.Success)
            return new() { Oid = match.Groups[1].Value, Size = long.Parse(match.Groups[2].Value) };

        return null;
    }
}
