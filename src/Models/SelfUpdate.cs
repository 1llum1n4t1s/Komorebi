using System;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Velopack;

namespace Komorebi.Models;

/// <summary>
/// GitHubリリースのバージョン情報を保持するクラス。
/// 現在のアプリバージョンとの比較機能を提供する。
/// </summary>
public class Version
{
    /// <summary>リリース名</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>タグ名（例: "v1.0.5"）</summary>
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; }

    /// <summary>リリース公開日時</summary>
    [JsonPropertyName("published_at")]
    public DateTime PublishedAt { get; set; }

    /// <summary>リリースノート本文</summary>
    [JsonPropertyName("body")]
    public string Body { get; set; }

    /// <summary>実行中アプリケーションのバージョン</summary>
    [JsonIgnore]
    public System.Version CurrentVersion { get; }

    /// <summary>現在のバージョンの表示文字列（例: "v1.05"）</summary>
    [JsonIgnore]
    public string CurrentVersionStr => $"v{CurrentVersion.Major}.{CurrentVersion.Minor:D2}";

    /// <summary>リモートのタグがアプリバージョンより新しいかどうか</summary>
    [JsonIgnore]
    public bool IsNewVersion
    {
        get
        {
            if (string.IsNullOrEmpty(TagName) || !TagName.StartsWith('v'))
                return false;

            return System.Version.TryParse(TagName[1..], out var remote) &&
                   CurrentVersion.CompareTo(remote) < 0;
        }
    }

    /// <summary>リリース日時の表示文字列</summary>
    [JsonIgnore]
    public string ReleaseDateStr => DateTimeFormat.Format(PublishedAt, true);

    /// <summary>
    /// 実行中アセンブリからバージョンを取得して初期化する
    /// </summary>
    public Version()
    {
        var assembly = Assembly.GetExecutingAssembly().GetName();
        CurrentVersion = assembly.Version ?? new System.Version();
    }
}

/// <summary>
/// Velopack更新情報を保持するクラス
/// </summary>
public class VelopackUpdate
{
    /// <summary>リリースのタグ名（例: v1.0.5）</summary>
    public string TagName => $"v{_updateInfo.TargetFullRelease.Version}";
    /// <summary>バージョン文字列</summary>
    public string VersionString => _updateInfo.TargetFullRelease.Version.ToString();

    /// <summary>
    /// Velopack更新情報を初期化する
    /// </summary>
    /// <param name="manager">Velopack更新マネージャー</param>
    /// <param name="updateInfo">利用可能な更新情報</param>
    public VelopackUpdate(UpdateManager manager, UpdateInfo updateInfo)
    {
        _manager = manager;
        _updateInfo = updateInfo;
    }

    /// <summary>
    /// 更新パッケージを非同期でダウンロードする
    /// </summary>
    public async Task DownloadAsync(Action<int> onProgress, CancellationToken token)
    {
        await _manager.DownloadUpdatesAsync(_updateInfo, onProgress, cancelToken: token);
    }

    /// <summary>
    /// ダウンロード済みの更新を適用してアプリケーションを再起動する
    /// </summary>
    public void ApplyAndRestart()
    {
        _manager.ApplyUpdatesAndRestart(_updateInfo);
    }

    /// <summary>Velopack更新マネージャー</summary>
    private readonly UpdateManager _manager;
    /// <summary>利用可能な更新情報</summary>
    private readonly UpdateInfo _updateInfo;
}

/// <summary>
/// 更新チェックの結果が「最新版」であることを示すマーカークラス
/// </summary>
public class AlreadyUpToDate;

/// <summary>
/// 自動更新の失敗を表すクラス。失敗理由のメッセージを保持する。
/// </summary>
public class SelfUpdateFailed
{
    /// <summary>失敗理由のメッセージ</summary>
    public string Reason
    {
        get;
        private set;
    }

    /// <summary>
    /// 例外情報から失敗メッセージを生成する
    /// </summary>
    /// <param name="e">発生した例外</param>
    public SelfUpdateFailed(Exception e)
    {
        // 内部例外がある場合はそのメッセージを優先
        if (e.InnerException is { } inner)
            Reason = inner.Message;
        else
            Reason = e.Message;
    }
}
