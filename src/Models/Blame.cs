using System;
using System.Collections.Generic;

namespace Komorebi.Models
{
    /// <summary>
    ///     Blameの各行に対応するコミット情報を保持するクラス。
    /// </summary>
    public class BlameLineInfo
    {
        /// <summary>
        ///     このコミットグループの最初の行かどうか。
        /// </summary>
        public bool IsFirstInGroup { get; set; } = false;

        /// <summary>
        ///     この行を最後に変更したコミットのSHAハッシュ。
        /// </summary>
        public string CommitSHA { get; set; } = string.Empty;

        /// <summary>
        ///     変更時のファイルパス。
        /// </summary>
        public string File { get; set; } = string.Empty;

        /// <summary>
        ///     変更の作者名。
        /// </summary>
        public string Author { get; set; } = string.Empty;

        /// <summary>
        ///     変更日時のUnixタイムスタンプ。
        /// </summary>
        public ulong Timestamp { get; set; } = 0;

        /// <summary>
        ///     フォーマット済みの変更日時文字列（日付のみ）。
        /// </summary>
        public string Time => DateTime.UnixEpoch.AddSeconds(Timestamp).ToLocalTime().ToString(DateTimeFormat.Active.DateOnly);
    }

    /// <summary>
    ///     Blame結果全体のデータを保持するクラス。
    /// </summary>
    public class BlameData
    {
        /// <summary>
        ///     対象ファイルがバイナリかどうか。
        /// </summary>
        public bool IsBinary { get; set; } = false;

        /// <summary>
        ///     ファイルの内容テキスト。
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        ///     各行に対応するBlame情報のリスト。
        /// </summary>
        public List<BlameLineInfo> LineInfos { get; set; } = [];
    }
}
