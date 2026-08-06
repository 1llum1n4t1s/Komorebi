// nullable 移行未実施。1 ファイルずつ null 注釈を入れてこの 2 行を削除していく。
#nullable disable warnings
namespace Komorebi.Models;

/// <summary>
/// Gitオブジェクトの種類を表す列挙型。
/// </summary>
public enum ObjectType
{
    /// <summary>
    /// 種類不明。
    /// </summary>
    None,

    /// <summary>
    /// ファイルの内容を格納するblobオブジェクト。
    /// </summary>
    Blob,

    /// <summary>
    /// ディレクトリ構造を表すtreeオブジェクト。
    /// </summary>
    Tree,

    /// <summary>
    /// 注釈付きタグオブジェクト。
    /// </summary>
    Tag,

    /// <summary>
    /// コミットオブジェクト。
    /// </summary>
    Commit,
}

/// <summary>
/// Gitオブジェクトを表すクラス。
/// SHA、種類、パス情報を保持する。
/// </summary>
public class Object
{
    /// <summary>
    /// オブジェクトのSHAハッシュ。
    /// </summary>
    public string SHA { get; set; }

    /// <summary>
    /// オブジェクトの種類。
    /// </summary>
    public ObjectType Type { get; set; }

    /// <summary>
    /// オブジェクトのファイルパス。
    /// </summary>
    public string Path { get; set; }
}
