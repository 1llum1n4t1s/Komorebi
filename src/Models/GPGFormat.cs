using System.Collections.Generic;

namespace Komorebi.Models;

/// <summary>
/// GPG署名のフォーマット設定を表すクラス。
/// コミットやタグの署名に使用される暗号化方式を定義する。
/// </summary>
public class GPGFormat(string name, string value, string desc, string program, bool needFindProgram)
{
    /// <summary>
    /// フォーマットの表示名。
    /// </summary>
    public string Name { get; set; } = name;

    /// <summary>
    /// git設定に渡す値（"openpgp", "x509", "ssh"）。
    /// </summary>
    public string Value { get; set; } = value;

    /// <summary>
    /// フォーマットの説明文。
    /// </summary>
    public string Desc { get; set; } = desc;

    /// <summary>
    /// 署名に使用するプログラム名。
    /// </summary>
    public string Program { get; set; } = program;

    /// <summary>
    /// プログラムのパスを自動検索する必要があるかどうか。
    /// </summary>
    public bool NeedFindProgram { get; set; } = needFindProgram;

    /// <summary>
    /// サポートされているGPGフォーマットの一覧。
    /// </summary>
    public static readonly List<GPGFormat> Supported = [
        new GPGFormat("OPENPGP", "openpgp", "DEFAULT", "gpg", true),
        new GPGFormat("X.509", "x509", "", "gpgsm", true),
        new GPGFormat("SSH", "ssh", "Requires Git >= 2.34.0", "ssh-keygen", false),
    ];
}
