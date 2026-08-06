// nullable 移行未実施。1 ファイルずつ null 注釈を入れてこの 2 行を削除していく。
#nullable disable warnings
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace Komorebi.Models;

/// <summary>
/// 起動シーケンスの進行状況を表すステージ。
/// クラッシュ時に「どこまで到達していたか」を残すために使う。
/// </summary>
public enum StartupStage
{
    /// <summary>プロセス開始直後（Velopack フック実行前）</summary>
    ProcessStart,
    /// <summary>Velopack の自動更新フック実行中</summary>
    VelopackHook,
    /// <summary>アプリケーションデータディレクトリの初期化中</summary>
    DataDir,
    /// <summary>ロガーの初期化中</summary>
    LoggerInit,
    /// <summary>起動モード（リベースエディタ等）の判定中</summary>
    LaunchModeCheck,
    /// <summary>Avalonia のデスクトップライフタイム開始中</summary>
    AvaloniaStart,
    /// <summary>App.Initialize（AXAML ロード・ロケール・テーマ適用）中</summary>
    AppInitialize,
    /// <summary>OnFrameworkInitializationCompleted（ウィンドウ生成）中</summary>
    FrameworkInitialized,
    /// <summary>メインウィンドウ生成後、初回アイドル待ち</summary>
    WaitingFirstIdle,
    /// <summary>起動完了（UI スレッドが最初のアイドルに到達した）</summary>
    Completed,
}

/// <summary>
/// 起動時クラッシュを確実に記録するための診断機構。
///
/// <see cref="Logger"/> は「初期化後」かつ「非同期バッファ経由」でしか書けないため、
/// 次の 2 種類の起動時クラッシュを原理的に取りこぼす。本クラスがその穴を埋める。
///
/// <list type="number">
/// <item>
/// <b>ロガー初期化より前の失敗</b>（Velopack フック / DataDir 作成 / ロガー初期化自体）。
/// → <see cref="WriteFatal"/> が Logger に依存せず同期書き込みでクラッシュログを残す。
/// </item>
/// <item>
/// <b>プロセス内で何も書けない死に方</b>（Native AOT のアクセス違反、ランタイム abort、
/// 外部からの強制終了、電源断など）。
/// → 起動中は PID 単位のマーカーファイルを置き、UI が最初のアイドルに到達した時点で削除する。
/// 次回起動時に残留マーカーを見つけたら「前回の起動は完了しなかった」として到達ステージ付きで記録する。
/// </item>
/// </list>
///
/// 書き込み先は <see cref="Bind"/> 前は一時ディレクトリ、Bind 後は DataDir 配下の logs。
/// あらゆる失敗を飲み込む（診断機構自身が起動を止めてはならない）。
/// </summary>
public static class StartupDiagnostics
{
    /// <summary>ブートストラップクラッシュログのファイル名（Logger のローリング対象と混ざらない名前にする）</summary>
    private const string FatalLogFileName = "Komorebi_startup_crash.log";

    private static readonly object s_sync = new();

    /// <summary>マーカー・クラッシュログの出力先ディレクトリ（Bind 前は一時ディレクトリ）</summary>
    private static string s_directory;

    /// <summary>自プロセスのマーカーファイルパス（Bind 後のみ有効）</summary>
    private static string s_markerFile;

    private static StartupStage s_stage = StartupStage.ProcessStart;
    private static bool s_completed;

    /// <summary>現在の起動ステージ。クラッシュレポートに含める。</summary>
    public static StartupStage CurrentStage => s_stage;

    /// <summary>
    /// 前回の起動が完了しなかった場合の詳細（ロガー初期化後に Logger へ流すためのバッファ）。
    /// 検出されなかった場合は空リスト。
    /// </summary>
    public static IReadOnlyList<string> PreviousIncompleteStartups => s_previousIncomplete;
    private static readonly List<string> s_previousIncomplete = [];

    /// <summary>
    /// 出力先を DataDir 配下の logs に切り替え、自プロセスの起動マーカーを作成する。
    /// 同時に、前回起動の残留マーカーを検出して <see cref="PreviousIncompleteStartups"/> に積む。
    /// </summary>
    /// <param name="dataDir">アプリケーションデータディレクトリ（<c>Native.OS.DataDir</c>）</param>
    public static void Bind(string dataDir)
    {
        try
        {
            if (string.IsNullOrEmpty(dataDir))
                return;

            var dir = Path.Combine(dataDir, "logs");
            Directory.CreateDirectory(dir);

            lock (s_sync)
            {
                s_directory = dir;
                s_markerFile = Path.Combine(dir, $"startup-{Environment.ProcessId}.marker");
            }

            CollectAbandonedMarkers(dir);
            WriteMarker();
        }
        catch
        {
            // 診断機構の失敗で起動を止めない
        }
    }

    /// <summary>
    /// 現在の起動ステージを記録する。マーカーファイルへ同期的に書き出すため、
    /// この直後にプロセスが消えても到達ステージが残る。
    /// </summary>
    public static void MarkStage(StartupStage stage)
    {
        s_stage = stage;
        WriteMarker();
    }

    /// <summary>
    /// 起動が正常に完了したことを記録し、マーカーファイルを削除する。
    /// 二重呼び出しされても安全（idempotent）。
    /// </summary>
    public static void MarkCompleted()
    {
        lock (s_sync)
        {
            if (s_completed)
                return;

            s_completed = true;
            s_stage = StartupStage.Completed;

            try
            {
                if (!string.IsNullOrEmpty(s_markerFile) && File.Exists(s_markerFile))
                    File.Delete(s_markerFile);
            }
            catch
            {
                // マーカー削除失敗は次回起動で「未完了」と誤検出されるだけなので無視する
            }
        }
    }

    /// <summary>
    /// Logger を経由せずクラッシュ情報を同期書き込みする。
    /// ロガー初期化前の失敗、および非同期バッファのフラッシュが保証されない経路で使う。
    /// 出力内容には <see cref="Logger.Redact"/> を適用する。
    /// </summary>
    /// <param name="context">発生コンテキスト（例: "Main"）</param>
    /// <param name="exception">例外（無い場合は null）</param>
    /// <param name="extra">追記したい補足情報</param>
    public static void WriteFatal(string context, Exception exception, string extra = null)
    {
        try
        {
            var builder = new StringBuilder();
            builder.AppendLine("=== 起動時クラッシュ ===");
            builder.Append("発生時刻: ").AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
            builder.Append("コンテキスト: ").AppendLine(context ?? "不明");
            builder.Append("ステージ: ").AppendLine(s_stage.ToString());
            builder.Append("バージョン: ").AppendLine(Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "不明");
            builder.Append("OS: ").AppendLine(Environment.OSVersion.ToString());
            builder.Append("PID: ").AppendLine(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));

            if (!string.IsNullOrEmpty(extra))
                builder.AppendLine(extra);

            if (exception is not null)
                builder.AppendLine(exception.ToString());

            builder.AppendLine();

            Append(Logger.Redact(builder.ToString()));
        }
        catch
        {
            // ここで失敗したらもう記録手段が無いので黙って諦める
        }
    }

    /// <summary>
    /// 残留マーカー（前回の起動が完了しなかった痕跡）を検出して記録し、マーカーを削除する。
    /// PID が生存していて開始時刻も一致するマーカーは、並行して起動中の別プロセスとみなして残す。
    /// </summary>
    private static void CollectAbandonedMarkers(string dir)
    {
        string[] markers;
        try
        {
            markers = Directory.GetFiles(dir, "startup-*.marker");
        }
        catch
        {
            return;
        }

        foreach (var marker in markers)
        {
            try
            {
                if (string.Equals(marker, s_markerFile, StringComparison.OrdinalIgnoreCase))
                    continue;

                var fields = ParseMarker(marker);
                if (IsStillRunning(fields))
                    continue;

                var detail =
                    $"前回の起動が完了しませんでした（プロセスがクラッシュまたは強制終了された可能性があります）: " +
                    $"pid={Get(fields, "pid")}, 起動時刻={Get(fields, "started")}, " +
                    $"到達ステージ={Get(fields, "stage")}, ステージ時刻={Get(fields, "stageAt")}, " +
                    $"バージョン={Get(fields, "version")}";

                s_previousIncomplete.Add(detail);

                // Logger 初期化前に消える可能性もあるため、ブートストラップログにも必ず残す
                WriteFatal("PreviousStartupIncomplete", null, detail);

                File.Delete(marker);
            }
            catch
            {
                // 個別マーカーの処理失敗は他のマーカー処理を止めない
            }
        }
    }

    /// <summary>
    /// マーカーに記録された PID / 開始時刻が現在も生存しているプロセスと一致するか判定する。
    /// 判定できない場合は false（= 未完了として記録する）を返す。
    /// ログの取りこぼしを避けるため、疑わしい場合は記録側に倒す。
    /// </summary>
    private static bool IsStillRunning(Dictionary<string, string> fields)
    {
        if (!int.TryParse(Get(fields, "pid"), CultureInfo.InvariantCulture, out var pid))
            return false;

        if (pid == Environment.ProcessId)
            return true;

        try
        {
            using var process = Process.GetProcessById(pid);
            if (process.HasExited)
                return false;

            // PID 再利用の誤判定を避けるため開始時刻も突き合わせる
            if (!long.TryParse(Get(fields, "startedTicks"), CultureInfo.InvariantCulture, out var ticks))
                return false;

            return process.StartTime.Ticks == ticks;
        }
        catch
        {
            return false;
        }
    }

    private static Dictionary<string, string> ParseMarker(string path)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadAllLines(path))
        {
            var idx = line.IndexOf('=');
            if (idx > 0)
                fields[line[..idx]] = line[(idx + 1)..];
        }
        return fields;
    }

    private static string Get(Dictionary<string, string> fields, string key)
    {
        return fields.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) ? value : "不明";
    }

    /// <summary>
    /// 自プロセスのマーカーファイルを現在のステージで上書きする。
    /// Bind 前・起動完了後は何もしない。
    /// </summary>
    private static void WriteMarker()
    {
        lock (s_sync)
        {
            if (s_completed || string.IsNullOrEmpty(s_markerFile))
                return;

            try
            {
                var now = DateTime.Now;
                var start = s_processStart ??= ResolveProcessStart();
                var content =
                    $"""
                    pid={Environment.ProcessId.ToString(CultureInfo.InvariantCulture)}
                    started={start.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)}
                    startedTicks={start.Ticks.ToString(CultureInfo.InvariantCulture)}
                    version={Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown"}
                    stage={s_stage}
                    stageAt={now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)}

                    """;

                File.WriteAllText(s_markerFile, content);
            }
            catch
            {
                // マーカー書き込み失敗は起動を妨げない
            }
        }
    }

    private static DateTime? s_processStart;

    private static DateTime ResolveProcessStart()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return process.StartTime;
        }
        catch
        {
            return DateTime.Now;
        }
    }

    /// <summary>
    /// ブートストラップクラッシュログへ追記する。Bind 前は一時ディレクトリへ出力する。
    /// </summary>
    private static void Append(string text)
    {
        string dir;
        lock (s_sync)
            dir = s_directory ?? Path.Combine(Path.GetTempPath(), "Komorebi");

        Directory.CreateDirectory(dir);
        File.AppendAllText(Path.Combine(dir, FatalLogFileName), text);
    }
}
