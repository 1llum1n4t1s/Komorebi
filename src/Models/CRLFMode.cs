using System.Collections.Generic;

namespace Komorebi.Models
{
    /// <summary>
    ///     改行コード変換モード（core.autocrlf）を表すクラス。
    ///     CRLFとLFの自動変換設定を定義する。
    /// </summary>
    public class CRLFMode(string name, string value, string desc)
    {
        /// <summary>
        ///     モードの表示名。
        /// </summary>
        public string Name { get; set; } = name;

        /// <summary>
        ///     git設定に渡す値（"true", "input", "false"）。
        /// </summary>
        public string Value { get; set; } = value;

        /// <summary>
        ///     モードの説明文。
        /// </summary>
        public string Desc { get; set; } = desc;

        /// <summary>
        ///     サポートされている改行コード変換モードの一覧。
        /// </summary>
        public static readonly List<CRLFMode> Supported = new List<CRLFMode>() {
            new CRLFMode("TRUE", "true", "Commit as LF, checkout as CRLF"),
            new CRLFMode("INPUT", "input", "Only convert for commit"),
            new CRLFMode("FALSE", "false", "Do NOT convert"),
        };
    }
}
