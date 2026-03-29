using System.Collections.Generic;

namespace Komorebi.Models;

/// <summary>
/// ブランチの並び替えモードを表す列挙型。
/// </summary>
public enum BranchSortMode
{
    /// <summary>
    /// 名前順で並び替え。
    /// </summary>
    Name = 0,

    /// <summary>
    /// コミッター日付順で並び替え。
    /// </summary>
    CommitterDate,
}

/// <summary>
/// Gitブランチの情報を表すクラス。
/// </summary>
public class Branch
{
    /// <summary>
    /// ブランチ名（短縮名）。
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// ブランチのフルパス名（refs/heads/... 等）。
    /// </summary>
    public string FullName { get; set; }

    /// <summary>
    /// 最新コミッターの日付（Unixタイムスタンプ）。
    /// </summary>
    public ulong CommitterDate { get; set; }

    /// <summary>
    /// ブランチが指すコミットのSHA。
    /// </summary>
    public string Head { get; set; }

    /// <summary>
    /// ローカルブランチかどうか。
    /// </summary>
    public bool IsLocal { get; set; }

    /// <summary>
    /// 現在チェックアウトされているブランチかどうか。
    /// </summary>
    public bool IsCurrent { get; set; }

    /// <summary>
    /// デタッチドHEAD状態かどうか。
    /// </summary>
    public bool IsDetachedHead { get; set; }

    /// <summary>
    /// 上流ブランチ名。
    /// </summary>
    public string Upstream { get; set; }

    /// <summary>
    /// 上流ブランチより先行しているコミットのSHAリスト。
    /// </summary>
    public List<string> Ahead { get; set; } = [];

    /// <summary>
    /// 上流ブランチに対して遅れているコミットのSHAリスト。
    /// </summary>
    public List<string> Behind { get; set; } = [];

    /// <summary>
    /// リモート名。
    /// </summary>
    public string Remote { get; set; }

    /// <summary>
    /// 上流ブランチが削除されたかどうか。
    /// </summary>
    public bool IsUpstreamGone { get; set; }

    /// <summary>
    /// 関連するワークツリーのパス。
    /// </summary>
    public string WorktreePath { get; set; }

    /// <summary>
    /// ワークツリーが存在するかどうか（現在のブランチでなく、パスが設定されている場合）。
    /// </summary>
    public bool HasWorktree => !IsCurrent && !string.IsNullOrEmpty(WorktreePath);

    /// <summary>
    /// 表示用のブランチ名（ローカルなら名前、リモートなら「リモート名/名前」）。
    /// </summary>
    public string FriendlyName => IsLocal ? Name : $"{Remote}/{Name}";

    /// <summary>
    /// トラッキングステータスを表示すべきかどうか。
    /// </summary>
    public bool IsTrackStatusVisible => (Ahead?.Count ?? 0) > 0 || (Behind?.Count ?? 0) > 0;

    /// <summary>
    /// トラッキングステータスの説明文字列（例: "3↑ 2↓"）を取得する。
    /// </summary>
    public string TrackStatusDescription
    {
        get
        {
            var ahead = Ahead?.Count ?? 0;
            var behind = Behind?.Count ?? 0;

            // 先行・遅延の状況に応じた文字列を生成
            if (ahead > 0)
                return behind > 0 ? $"{ahead}↑ {behind}↓" : $"{ahead}↑";

            return behind > 0 ? $"{behind}↓" : string.Empty;
        }
    }
}
