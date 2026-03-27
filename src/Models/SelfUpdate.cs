using System;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Velopack;

namespace Komorebi.Models;

public class Version
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("tag_name")]
    public string TagName { get; set; }

    [JsonPropertyName("published_at")]
    public DateTime PublishedAt { get; set; }

    [JsonPropertyName("body")]
    public string Body { get; set; }

    [JsonIgnore]
    public System.Version CurrentVersion { get; }

    [JsonIgnore]
    public string CurrentVersionStr => $"v{CurrentVersion.Major}.{CurrentVersion.Minor:D2}";

    [JsonIgnore]
    public bool IsNewVersion
    {
        get
        {
            if (string.IsNullOrEmpty(TagName) || !TagName.StartsWith('v'))
                return false;

            return System.Version.TryParse(TagName.Substring(1), out var remote) &&
                   CurrentVersion.CompareTo(remote) < 0;
        }
    }

    [JsonIgnore]
    public string ReleaseDateStr => DateTimeFormat.Format(PublishedAt, true);

    public Version()
    {
        var assembly = Assembly.GetExecutingAssembly().GetName();
        CurrentVersion = assembly.Version ?? new System.Version();
    }
}

/// <summary>
///     Velopack更新情報を保持するクラス
/// </summary>
public class VelopackUpdate
{
    /// <summary>リリースのタグ名（例: v1.0.5）</summary>
    public string TagName => $"v{_updateInfo.TargetFullRelease.Version}";
    /// <summary>バージョン文字列</summary>
    public string VersionString => _updateInfo.TargetFullRelease.Version.ToString();

    public VelopackUpdate(UpdateManager manager, UpdateInfo updateInfo)
    {
        _manager = manager;
        _updateInfo = updateInfo;
    }

    /// <summary>
    ///     更新パッケージを非同期でダウンロードする
    /// </summary>
    public async Task DownloadAsync(Action<int> onProgress, CancellationToken token)
    {
        await _manager.DownloadUpdatesAsync(_updateInfo, onProgress, cancelToken: token);
    }

    /// <summary>
    ///     ダウンロード済みの更新を適用してアプリケーションを再起動する
    /// </summary>
    public void ApplyAndRestart()
    {
        _manager.ApplyUpdatesAndRestart(_updateInfo);
    }

    private readonly UpdateManager _manager;
    private readonly UpdateInfo _updateInfo;
}

public class AlreadyUpToDate;

public class SelfUpdateFailed
{
    public string Reason
    {
        get;
        private set;
    }

    public SelfUpdateFailed(Exception e)
    {
        if (e.InnerException is { } inner)
            Reason = inner.Message;
        else
            Reason = e.Message;
    }
}
