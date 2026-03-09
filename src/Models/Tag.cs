using System;

namespace Komorebi.Models
{
    /// <summary>
    ///     タグのソート方法を表す列挙型。
    /// </summary>
    public enum TagSortMode
    {
        /// <summary>
        ///     作成日時でソートする。
        /// </summary>
        CreatorDate = 0,

        /// <summary>
        ///     タグ名でソートする。
        /// </summary>
        Name,
    }

    /// <summary>
    ///     Gitタグを表すクラス。
    ///     軽量タグと注釈付きタグの両方を表現できる。
    /// </summary>
    public class Tag
    {
        /// <summary>
        ///     タグ名。
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        ///     注釈付きタグかどうか。
        /// </summary>
        public bool IsAnnotated { get; set; } = false;

        /// <summary>
        ///     タグが指すコミットのSHAハッシュ。
        /// </summary>
        public string SHA { get; set; } = string.Empty;

        /// <summary>
        ///     タグの作成者。
        /// </summary>
        public User Creator { get; set; } = null;

        /// <summary>
        ///     タグの作成日時（Unix時間）。
        /// </summary>
        public ulong CreatorDate { get; set; } = 0;

        /// <summary>
        ///     タグのメッセージ（注釈付きタグの場合）。
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        ///     ローカルタイムゾーンでフォーマットされた作成日時文字列を取得する。
        /// </summary>
        public string CreatorDateStr
        {
            get => DateTime.UnixEpoch.AddSeconds(CreatorDate).ToLocalTime().ToString(DateTimeFormat.Active.DateTime);
        }
    }
}
