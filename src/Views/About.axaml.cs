using System;
using System.Reflection;

using Avalonia.Interactivity;

namespace Komorebi.Views;

/// <summary>
/// アプリケーションの情報画面（バージョン、リリース日、著作権などを表示する）。
/// </summary>
public partial class About : ChromelessWindow
{
    public About()
    {
        CloseOnESC = true;
        InitializeComponent();

        var assembly = Assembly.GetExecutingAssembly();

        // Directory.Build.props の Version をアセンブリ情報から取得して表示する。
        // InformationalVersion にはビルド時に "+コミットハッシュ" が付与される場合があるため除去する。
        var info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (info is not null)
        {
            var version = info.InformationalVersion;
            var plusIndex = version.IndexOf('+');
            if (plusIndex >= 0)
                version = version[..plusIndex];
            TxtVersion.Text = version;
        }

        // アセンブリメタデータからビルド日時を取得して表示する
        var meta = assembly.GetCustomAttributes<AssemblyMetadataAttribute>();
        foreach (var attr in meta)
        {
            if (attr.Key.Equals("BuildDate", StringComparison.OrdinalIgnoreCase) && DateTime.TryParse(attr.Value, out var date))
            {
                // Preferences の DateTimeFormat 設定を反映する（"MMM d yyyy" 固定ではなくユーザー選択形式）。
                // `Models.DateTimeFormat.Format(DateTime, bool)` は引数をローカル時刻と仮定して内部変換しないため、
                // BuildDate が UTC としてパースされた場合に備えて呼び出し側で `ToLocalTime()` しておく。
                // （upstream b81c67c9 は `ToLocalTime()` を誤って削除しており、それだと UTC 表示になる回帰を防止）
                TxtReleaseDate.Text = App.Text("About.ReleaseDate", Models.DateTimeFormat.Format(date.ToLocalTime(), true));
                break;
            }
        }

        // 著作権情報を表示する
        var copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>();
        if (copyright is not null)
            TxtCopyright.Text = copyright.Copyright;
    }

    /// <summary>
    /// 現在のバージョンに対応するGitHubリリースノートをブラウザで開く。
    /// </summary>
    private void OnVisitReleaseNotes(object _, RoutedEventArgs e)
    {
        Native.OS.OpenBrowser($"https://github.com/1llum1n4t1s/Komorebi/releases/tag/v{TxtVersion.Text}");
        e.Handled = true;
    }

    /// <summary>
    /// プロジェクトのWebサイトをブラウザで開く。
    /// </summary>
    private void OnVisitWebsite(object _, RoutedEventArgs e)
    {
        Native.OS.OpenBrowser("https://github.com/1llum1n4t1s/Komorebi");
        e.Handled = true;
    }

    /// <summary>
    /// ソースコードリポジトリをブラウザで開く。
    /// </summary>
    private void OnVisitSourceCode(object _, RoutedEventArgs e)
    {
        Native.OS.OpenBrowser("https://github.com/1llum1n4t1s/Komorebi");
        e.Handled = true;
    }
}
