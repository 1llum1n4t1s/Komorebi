using System;
using System.Collections.Generic;

namespace Komorebi.Models
{
    /// <summary>
    ///     git stashエントリを表すクラス。
    ///     スタッシュの名前、SHA、タイムスタンプ、メッセージを保持する。
    /// </summary>
    public class Stash
    {
        /// <summary>
        ///     スタッシュの参照名（例: "stash@{0}"）。
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        ///     スタッシュコミットのSHAハッシュ。
        /// </summary>
        public string SHA { get; set; } = "";

        /// <summary>
        ///     親コミットのSHAリスト。
        /// </summary>
        public List<string> Parents { get; set; } = [];

        /// <summary>
        ///     スタッシュ作成時刻（Unix時間）。
        /// </summary>
        public ulong Time { get; set; } = 0;

        /// <summary>
        ///     スタッシュのメッセージ全文。
        /// </summary>
        public string Message { get; set; } = "";

        /// <summary>
        ///     メッセージの1行目（件名）を取得する。
        /// </summary>
        public string Subject
        {
            get
            {
                // 最初の改行で分割し、1行目のみ返す
                return Message.Split('\n', 2)[0].Trim();
            }
        }

        /// <summary>
        ///     ローカルタイムゾーンでフォーマットされた日時文字列を取得する。
        /// </summary>
        public string TimeStr
        {
            get
            {
                // Unix時間からローカル日時に変換してフォーマット
                return DateTime.UnixEpoch
                    .AddSeconds(Time)
                    .ToLocalTime()
                    .ToString(DateTimeFormat.Active.DateTime);
            }
        }
    }
}
