using System;
using System.Collections.Generic;

namespace Komorebi.Models;

/// <summary>
/// Git Flowのブランチ種別を表すenum
/// </summary>
public enum GitFlowBranchType
{
    /// <summary>種別なし</summary>
    None = 0,
    /// <summary>機能開発ブランチ</summary>
    Feature,
    /// <summary>リリース準備ブランチ</summary>
    Release,
    /// <summary>緊急修正ブランチ</summary>
    Hotfix,
}

/// <summary>
/// Git Flow設定を管理するクラス。
/// マスター/開発ブランチ名やプレフィックスを保持する。
/// </summary>
public class GitFlow
{
    /// <summary>マスターブランチ名（例: main, master）</summary>
    public string Master { get; set; } = string.Empty;
    /// <summary>開発ブランチ名（例: develop）</summary>
    public string Develop { get; set; } = string.Empty;
    /// <summary>featureブランチのプレフィックス（例: feature/）</summary>
    public string FeaturePrefix { get; set; } = string.Empty;
    /// <summary>releaseブランチのプレフィックス（例: release/）</summary>
    public string ReleasePrefix { get; set; } = string.Empty;
    /// <summary>hotfixブランチのプレフィックス（例: hotfix/）</summary>
    public string HotfixPrefix { get; set; } = string.Empty;

    /// <summary>
    /// Git Flow設定が有効かどうか（全フィールドが入力済み）
    /// </summary>
    public void Parse(Dictionary<string, string> config, bool isNext)
    {
        // 設定の再読み込みで古い値を残さない。
        Master = string.Empty;
        Develop = string.Empty;
        FeaturePrefix = string.Empty;
        ReleasePrefix = string.Empty;
        HotfixPrefix = string.Empty;

        // git-flow-next が利用可能な場合は新形式を優先する。
        if (isNext &&
            config.TryGetValue("gitflow.initialized", out var initialized) &&
            initialized.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var kv in config)
            {
                if (!kv.Key.StartsWith("gitflow.branch.", StringComparison.Ordinal))
                    continue;

                if (kv.Key.EndsWith(".type", StringComparison.Ordinal) && kv.Value.Equals("base", StringComparison.Ordinal))
                {
                    var b = kv.Key.Substring("gitflow.branch.".Length, kv.Key.Length - "gitflow.branch.".Length - ".type".Length);
                    if (config.ContainsKey($"gitflow.branch.{b}.parent"))
                        Develop = b;
                    else
                        Master = b;
                }
                else if (kv.Key.EndsWith(".prefix", StringComparison.Ordinal))
                {
                    var t = kv.Key.Substring("gitflow.branch.".Length, kv.Key.Length - "gitflow.branch.".Length - ".prefix".Length);
                    if (t.Equals("feature", StringComparison.Ordinal))
                        FeaturePrefix = kv.Value;
                    else if (t.Equals("release", StringComparison.Ordinal))
                        ReleasePrefix = kv.Value;
                    else if (t.Equals("hotfix", StringComparison.Ordinal))
                        HotfixPrefix = kv.Value;
                }
            }
        }

        // 不完全な新形式は破棄して従来形式を読み込む。
        if (!IsValid)
        {
            Master = Develop = FeaturePrefix = ReleasePrefix = HotfixPrefix = string.Empty;
            if (config.TryGetValue("gitflow.branch.master", out var masterName))
                Master = masterName;
            if (config.TryGetValue("gitflow.branch.develop", out var developName))
                Develop = developName;
            if (config.TryGetValue("gitflow.prefix.feature", out var featurePrefix))
                FeaturePrefix = featurePrefix;
            if (config.TryGetValue("gitflow.prefix.release", out var releasePrefix))
                ReleasePrefix = releasePrefix;
            if (config.TryGetValue("gitflow.prefix.hotfix", out var hotfixPrefix))
                HotfixPrefix = hotfixPrefix;
        }
    }

    public bool IsValid
    {
        get
        {
            return !string.IsNullOrEmpty(Master) &&
                !string.IsNullOrEmpty(Develop) &&
                !string.IsNullOrEmpty(FeaturePrefix) &&
                !string.IsNullOrEmpty(ReleasePrefix) &&
                !string.IsNullOrEmpty(HotfixPrefix);
        }
    }

    /// <summary>
    /// 指定されたブランチ種別に対応するプレフィックスを取得する
    /// </summary>
    /// <param name="type">ブランチ種別</param>
    /// <returns>対応するプレフィックス文字列</returns>
    public string GetPrefix(GitFlowBranchType type)
    {
        return type switch
        {
            GitFlowBranchType.Feature => FeaturePrefix,
            GitFlowBranchType.Release => ReleasePrefix,
            GitFlowBranchType.Hotfix => HotfixPrefix,
            _ => string.Empty,
        };
    }
}
