using System.Collections.Generic;

namespace Komorebi.Models;

/// <summary>
/// 改行コード変換モード（core.autocrlf）を表すクラス。
/// CRLFとLFの自動変換設定を定義する。
/// </summary>
public class CRLFMode(string name, string value, string descKey)
{
    /// <summary>
    /// モードの表示名。
    /// </summary>
    public string Name { get; set; } = name;

    /// <summary>
    /// git設定に渡す値（"true", "input", "false"）。
    /// </summary>
    public string Value { get; set; } = value;

    /// <summary>
    /// モードの説明文（ローカライズ済み）。
    /// </summary>
    public string Desc => App.Text(descKey);

    /// <summary>
    /// サポートされている改行コード変換モードの一覧。
    /// </summary>
    public static readonly List<CRLFMode> Supported = new List<CRLFMode>() {
        new CRLFMode("TRUE", "true", "Preferences.Git.CRLF.True"),
        new CRLFMode("INPUT", "input", "Preferences.Git.CRLF.Input"),
        new CRLFMode("FALSE", "false", "Preferences.Git.CRLF.False"),
    };
}
