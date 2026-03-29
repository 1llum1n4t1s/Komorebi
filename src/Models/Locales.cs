using System.Collections.Generic;

namespace Komorebi.Models;

/// <summary>
/// アプリケーションのロケール（言語設定）を表すクラス。
/// サポートされている言語の一覧を管理する。
/// </summary>
public class Locale
{
    /// <summary>
    /// 言語の表示名（例: "日本語", "English"）。
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// ロケールキー（例: "ja_JP", "en_US"）。
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// サポートされているロケールの一覧。
    /// </summary>
    public static readonly List<Locale> Supported = new List<Locale>() {
        new Locale("Deutsch", "de_DE"),
        new Locale("English", "en_US"),
        new Locale("Español", "es_ES"),
        new Locale("Français", "fr_FR"),
        new Locale("Bahasa Indonesia", "id_ID"),
        new Locale("Filipino (Tagalog)", "fil_PH"),
        new Locale("Italiano", "it_IT"),
        new Locale("Português (Brasil)", "pt_BR"),
        new Locale("Українська", "uk_UA"),
        new Locale("Русский", "ru_RU"),
        new Locale("简体中文", "zh_CN"),
        new Locale("繁體中文", "zh_TW"),
        new Locale("日本語", "ja_JP"),
        new Locale("தமிழ் (Tamil)", "ta_IN"),
        new Locale("한국어", "ko_KR"),
        new Locale("संस्कृतम् (Sanskrit)", "sa"),
        new Locale("Latina (Latin)", "la"),
    };

    /// <summary>
    /// Localeの新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="name">言語の表示名。</param>
    /// <param name="key">ロケールキー。</param>
    public Locale(string name, string key)
    {
        Name = name;
        Key = key;
    }
}
