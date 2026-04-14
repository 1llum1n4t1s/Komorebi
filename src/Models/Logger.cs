using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
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
public static class Logger
{
    private static ILog s_logger;
    private static bool s_isConfigured;

    /// <summary>
    /// 最小ログレベル（これ以上のレベルのログのみ出力）
    /// </summary>
#pragma warning disable CA1802
    private static readonly LogLevel s_minLogLevel =
#if DEBUG
        LogLevel.Debug;
#else
        LogLevel.Warning;
#endif
#pragma warning restore CA1802

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
        s_logger?.Error(message, exception);
    }

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

        s_logger?.Error(message, exception);
    }

    /// <summary>
    /// アプリケーション起動時のログを出力する
    /// </summary>
    public static void LogStartup()
    {
        if (LogLevel.Debug < s_minLogLevel)
            return;

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        s_logger?.Debug(
            $"""
            === Komorebi 起動ログ ===
            起動時刻: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}
            バージョン: {version}
            OS: {Environment.OSVersion}
            実行ファイルパス: {Environment.ProcessPath}
            """);
    }

    /// <summary>
    /// ロガーを終了する（バッファのフラッシュ等）
    /// </summary>
    public static void Dispose()
    {
        SuperLightLogger.LogManager.Shutdown();
        s_isConfigured = false;
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
