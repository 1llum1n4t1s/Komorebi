// nullable 移行未実施。1 ファイルずつ null 注釈を入れてこの 2 行を削除していく。
#nullable disable warnings
using System.Text.Json.Serialization;

namespace Komorebi.Models;

/// <summary>
/// Git LFSロックの所有者情報
/// </summary>
public class LFSLockOwner
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Git LFSロック情報。ファイルのロック状態を表す。
/// </summary>
public class LFSLock
{
    [JsonPropertyName("id")]
    public string ID { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("owner")]
    public LFSLockOwner Owner { get; set; } = null;
}
