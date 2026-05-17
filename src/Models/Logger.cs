using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;

using Microsoft.Extensions.Logging;

using SuperLightLogger;

using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Komorebi.Models;

/// <summary>
/// ログレベルを表す列挙型
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
}

/// <summary>
/// ログ初期化設定
/// </summary>
public sealed class LoggerConfig
{
    /// <summary>ログ出力ディレクトリ</summary>
    public required string LogDirectory { get; init; }

    /// <summary>ログファイル名のプレフィックス（例: "Komorebi"）</summary>
    public required string FilePrefix { get; init; }

    /// <summary>ローリングサイズ上限（MB）</summary>
    public int MaxSizeMB { get; init; } = 1;

    /// <summary>アーカイブファイルの最大保持数</summary>
    public int MaxArchiveFiles { get; init; } = 10;

    /// <summary>ログファイルの保持日数（0以下の場合は削除しない）</summary>
    public int RetentionDays { get; init; } = 7;
}

/// <summary>
/// SuperLightLoggerを使用した汎用ログ出力クラス
/// </summary>
public static partial class Logger
{
    private static ILog s_logger;
    private static bool s_isConfigured;

    /// <summary>
    /// 最小ログレベル（これ以上のレベルのログのみ出力）。
    /// リリースビルドは Info まで出す（以前は Warning のみで本番バグ調査に情報が足りなかった）。
    /// Info レベルには Fetch/Push/Pull 等の主要操作の開始・終了が含まれる想定。
    /// Debug レベルの詳細トレースはリリースでは引き続き抑制する。
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1802:Use literals where appropriate",
        Justification = "Conditional compilation makes const impossible")]
    private static readonly LogLevel s_minLogLevel =
#if DEBUG
        LogLevel.Debug;
#else
        LogLevel.Info;
#endif

    /// <summary>
    /// ロガーを初期化する
    /// </summary>
    /// <param name="config">ログ設定</param>
    public static void Initialize(LoggerConfig config)
    {
        if (s_isConfigured)
            return;

        if (!Directory.Exists(config.LogDirectory))
            Directory.CreateDirectory(config.LogDirectory);

        var activeFileName = Path.Combine(config.LogDirectory, config.FilePrefix + "_${date:format=yyyyMMdd}.log");
        var archiveFileName = Path.Combine(config.LogDirectory, config.FilePrefix + "_${date:format=yyyyMMdd}_{#}.log");

        SuperLightLogger.LogManager.Configure(builder =>
        {
            builder.SetMinimumLevel(MsLogLevel.Trace);
            builder.AddSuperLightFile(opt =>
            {
                opt.FileName = activeFileName;
                opt.ArchiveFileName = archiveFileName;
                opt.Layout = "${longdate} [${level:uppercase=true}] [${threadid}] ${message}${onexception:${newline}${exception:format=tostring}}";
                opt.ArchiveAboveSize = config.MaxSizeMB * 1024L * 1024L;
                opt.MaxArchiveFiles = config.MaxArchiveFiles;
                opt.ArchiveNumbering = ArchiveNumbering.Sequence;
                opt.CreateDirectories = true;
                opt.Async = true;
            });
        });

        s_logger = SuperLightLogger.LogManager.GetLogger(config.FilePrefix);
        s_isConfigured = true;

        Log("SuperLightLoggerロガーを初期化しました", LogLevel.Debug);
        CleanupOldLogFiles(config.LogDirectory, config.FilePrefix, config.RetentionDays);
    }

    /// <summary>
    /// ログを出力する
    /// </summary>
    /// <param name="message">ログメッセージ</param>
    /// <param name="level">ログレベル（デフォルト: Info）</param>
    public static void Log(string message, LogLevel level = LogLevel.Info)
    {
        if (level < s_minLogLevel)
            return;

        if (s_logger is null)
            return;

        // PII redaction を全ログ経路に適用
        message = Redact(message);

        switch (level)
        {
            case LogLevel.Debug:
                s_logger.Debug(message);
                break;
            case LogLevel.Info:
                s_logger.Info(message);
                break;
            case LogLevel.Warning:
                s_logger.Warn(message);
                break;
            case LogLevel.Error:
                s_logger.Error(message);
                break;
        }
    }

    /// <summary>
    /// 例外情報を含むログを出力する（常にErrorレベル）
    /// </summary>
    /// <param name="message">ログメッセージ</param>
    /// <param name="exception">例外オブジェクト</param>
    public static void LogException(string message, Exception exception)
    {
        // PII redaction を例外メッセージにも適用
        s_logger?.Error(Redact(message), exception);
    }

    /// <summary>
    /// 個人情報・秘密情報のマスキング処理。
    /// ログ・クラッシュレポート・Issue にコピペされる可能性のあるすべての出力に適用する。
    ///
    /// マスキング対象:
    /// - Windows ユーザーホーム: <c>C:\Users\&lt;name&gt;\...</c> → <c>C:\Users\***\...</c>
    /// - Unix ユーザーホーム: <c>/home/&lt;name&gt;/...</c> / <c>/Users/&lt;name&gt;/...</c> → <c>/home/***/...</c>
    /// - Git URL の credentials: <c>https://user:token@host</c> → <c>https://***:***@host</c>
    /// - Bearer/Authorization トークン: <c>Bearer xxx</c> → <c>Bearer ***</c>
    /// - OpenAI 形式 API キー: <c>sk-...</c>, <c>sk-proj-...</c> → <c>sk-***</c>
    /// - Email アドレス: <c>user@example.com</c> → <c>***@example.com</c>
    ///
    /// 注意: ファイル内容そのものや commit メッセージ本体はマスキングしない（誤検出すると
    /// 開発者が読める情報が壊れる）。あくまで「環境依存の識別子」のみを対象とする。
    /// </summary>
    public static string Redact(string message)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        message = GitCredsRegex().Replace(message, "$1***:***@");

        message = BearerRegex().Replace(message, "$1 ***");
        message = ApiKeyJsonRegex().Replace(message, "$1***");

        message = OpenAiKeyRegex().Replace(message, "sk-***");

        message = WinHomeRegex().Replace(message, @"C:\Users\***");
        message = UnixHomeRegex().Replace(message, "$1/***");

        message = EmailRegex().Replace(message, "***$1");

        return message;
    }

    [GeneratedRegex(@"(https?://|git\+ssh://|ssh://)[^/\s@:]+:[^/\s@]+@", RegexOptions.IgnoreCase)]
    private static partial Regex GitCredsRegex();

    [GeneratedRegex(@"(Bearer|Token)\s+[A-Za-z0-9\-._~+/]+=*", RegexOptions.IgnoreCase)]
    private static partial Regex BearerRegex();

    [GeneratedRegex(@"(""(?:api[_\-]?key|password|secret|token|x[_\-]api[_\-]key)""\s*[:=]\s*"")[^""]+", RegexOptions.IgnoreCase)]
    private static partial Regex ApiKeyJsonRegex();

    [GeneratedRegex(@"sk-(?:proj-)?[A-Za-z0-9_\-]{16,}")]
    private static partial Regex OpenAiKeyRegex();

    [GeneratedRegex(@"[A-Za-z]:\\Users\\[^\\\/\s""']+", RegexOptions.IgnoreCase)]
    private static partial Regex WinHomeRegex();

    [GeneratedRegex(@"(/home|/Users)/[^/\s""']+")]
    private static partial Regex UnixHomeRegex();

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+(@[A-Za-z0-9.\-]+\.[A-Za-z]{2,})")]
    private static partial Regex EmailRegex();

    /// <summary>
    /// 未処理例外の詳細情報をログに記録する（クラッシュレポート用）
    /// </summary>
    /// <param name="exception">例外オブジェクト</param>
    /// <param name="context">例外の発生コンテキスト（例: "AppDomain.UnhandledException"）</param>
    public static void LogCrash(Exception exception, string context = null)
    {
        if (exception is null)
            return;

        var process = Process.GetCurrentProcess();
        var version = Assembly.GetExecutingAssembly().GetName().Version;

        var message =
            $"""
            === クラッシュレポート ===
            例外: {exception.GetType().FullName}: {exception.Message}
            コンテキスト: {context ?? "不明"}
            バージョン: {version}
            OS: {Environment.OSVersion}
            Framework: {AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName}
            スレッド名: {Thread.CurrentThread.Name ?? "Unnamed"}
            スレッドID: {Environment.CurrentManagedThreadId}
            アプリ起動時刻: {process.StartTime}
            メモリ使用量: {process.PrivateMemorySize64 / 1024 / 1024} MB
            """;

        // クラッシュレポートは Issue にコピペされる可能性が高い経路のため
        // PII redaction を必ず適用する。
        s_logger?.Error(Redact(message), exception);
    }

    /// <summary>
    /// アプリケーション起動時のログを出力する
    /// </summary>
    public static void LogStartup()
    {
        if (LogLevel.Debug < s_minLogLevel)
            return;

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var startupMessage =
            $"""
            === Komorebi 起動ログ ===
            起動時刻: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}
            バージョン: {version}
            OS: {Environment.OSVersion}
            実行ファイルパス: {Environment.ProcessPath}
            """;
        // 実行ファイルパスにユーザー名が含まれるため Redact 必須
        s_logger?.Debug(Redact(startupMessage));
    }

    /// <summary>
    /// ロガーを終了する（非同期バッファをフラッシュする）。
    /// </summary>
    /// <remarks>
    /// 重要: <c>opt.Async = true</c> のため、<c>LogCrash</c> で書き込んだレコードは
    /// バックグラウンドスレッドでディスクに書かれる。未処理例外ハンドラなど、
    /// 呼び出し後ただちにプロセスが終了する経路ではこのメソッドを呼んでバッファを
    /// 強制的にフラッシュしないと、最後のクラッシュログが失われる可能性がある。
    /// 二重呼び出しされても安全（idempotent）。
    /// </remarks>
    public static void Dispose()
    {
        if (!s_isConfigured)
            return;

        s_isConfigured = false;

        try
        {
            SuperLightLogger.LogManager.Shutdown();
        }
        catch
        {
            // 終了時のフラッシュ失敗はこれ以上ログにも残せないので無視する
        }
    }

    /// <summary>
    /// 保持期間を超えた古いログファイルを削除する
    /// </summary>
    private static void CleanupOldLogFiles(string logDirectory, string filePrefix, int retentionDays)
    {
        if (retentionDays <= 0)
            return;

        try
        {
            var cutoffDate = DateTime.Now.Date.AddDays(-retentionDays);
            var logFiles = Directory.GetFiles(logDirectory, $"{filePrefix}_*.log");

            foreach (var file in logFiles)
            {
                try
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var parts = fileName.Split('_');
                    if (parts.Length >= 2 && parts[1].Length == 8 &&
                        DateTime.TryParseExact(parts[1], "yyyyMMdd", null,
                            DateTimeStyles.None, out var fileDate))
                    {
                        if (fileDate < cutoffDate)
                        {
                            File.Delete(file);
                            Log($"古いログファイルを削除しました: {Path.GetFileName(file)}", LogLevel.Debug);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"ログファイルの削除に失敗しました: {Path.GetFileName(file)} - {ex.Message}", LogLevel.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            Log($"ログファイルのクリーンアップ中にエラーが発生しました: {ex.Message}", LogLevel.Warning);
        }
    }
}
