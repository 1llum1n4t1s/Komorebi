using System;
using System.Collections.Generic;

namespace Komorebi.Models
{
    /// <summary>
    ///     日付・日時のフォーマットを管理するクラス。
    ///     アプリケーション内の日時表示形式を定義する。
    /// </summary>
    public class DateTimeFormat
    {
        /// <summary>
        ///     日付のみのフォーマット文字列（例: "yyyy/MM/dd"）
        /// </summary>
        public string DateOnly { get; set; }

        /// <summary>
        ///     日時のフォーマット文字列（例: "yyyy/MM/dd, HH:mm:ss"）
        /// </summary>
        public string DateTime { get; set; }

        /// <summary>
        ///     現在のフォーマットで表示されるサンプル文字列
        /// </summary>
        public string Example
        {
            get => _example.ToString(DateTime);
        }

        /// <summary>
        ///     コンストラクタ
        /// </summary>
        /// <param name="dateOnly">日付のみのフォーマット文字列</param>
        /// <param name="dateTime">日時のフォーマット文字列</param>
        public DateTimeFormat(string dateOnly, string dateTime)
        {
            DateOnly = dateOnly;
            DateTime = dateTime;
        }

        /// <summary>
        ///     現在選択されているフォーマットのインデックス
        /// </summary>
        public static int ActiveIndex
        {
            get;
            set;
        } = 0;

        /// <summary>
        ///     現在アクティブなフォーマットインスタンス
        /// </summary>
        public static DateTimeFormat Active
        {
            get => Supported[ActiveIndex];
        }

        /// <summary>
        ///     サポートされている日時フォーマットの一覧
        /// </summary>
        public static readonly List<DateTimeFormat> Supported = new List<DateTimeFormat>
        {
            new DateTimeFormat("yyyy/MM/dd", "yyyy/MM/dd, HH:mm:ss"),
            new DateTimeFormat("yyyy.MM.dd", "yyyy.MM.dd, HH:mm:ss"),
            new DateTimeFormat("yyyy-MM-dd", "yyyy-MM-dd, HH:mm:ss"),
            new DateTimeFormat("MM/dd/yyyy", "MM/dd/yyyy, HH:mm:ss"),
            new DateTimeFormat("MM.dd.yyyy", "MM.dd.yyyy, HH:mm:ss"),
            new DateTimeFormat("MM-dd-yyyy", "MM-dd-yyyy, HH:mm:ss"),
            new DateTimeFormat("dd/MM/yyyy", "dd/MM/yyyy, HH:mm:ss"),
            new DateTimeFormat("dd.MM.yyyy", "dd.MM.yyyy, HH:mm:ss"),
            new DateTimeFormat("dd-MM-yyyy", "dd-MM-yyyy, HH:mm:ss"),
            new DateTimeFormat("MMM d yyyy", "MMM d yyyy, HH:mm:ss"),
            new DateTimeFormat("d MMM yyyy", "d MMM yyyy, HH:mm:ss"),
        };

        /// <summary>表示例に使用する固定の日時サンプル</summary>
        private static readonly DateTime _example = new DateTime(2025, 1, 31, 8, 0, 0, DateTimeKind.Local);
    }
}
