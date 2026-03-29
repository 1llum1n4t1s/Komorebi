namespace Komorebi.Models;

/// <summary>
/// リポジトリスキャン対象ディレクトリの情報（パスと説明）を保持するレコード
/// </summary>
public record ScanDir(string path, string desc)
{
    /// <summary>ディレクトリのパス</summary>
    public string Path { get; set; } = path;
    /// <summary>ディレクトリの説明</summary>
    public string Desc { get; set; } = desc;
}
