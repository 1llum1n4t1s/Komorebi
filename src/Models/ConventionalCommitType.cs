using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Komorebi.Models;

/// <summary>
/// Conventional Commitsの型定義を表すクラス。
/// コミットメッセージのプレフィックス（feat, fix等）とその説明を保持する。
/// </summary>
public class ConventionalCommitType
{
    /// <summary>
    /// 型の表示名（例: "Features", "Bug Fixes"）。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// コミットメッセージに使用される型プレフィックス（例: "feat", "fix"）。
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// この型の説明文。
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 短い説明の事前入力テキスト。
    /// </summary>
    public string PrefillShortDesc { get; set; } = string.Empty;

    /// <summary>
    /// ConventionalCommitTypeの新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="name">型の表示名。</param>
    /// <param name="type">コミットメッセージ用の型プレフィックス。</param>
    /// <param name="description">型の説明文。</param>
    public ConventionalCommitType(string name, string type, string description)
    {
        Name = name;
        Type = type;
        Description = description;
    }

    /// <summary>
    /// カスタム定義ファイルまたはデフォルトからConventional Commit型リストを読み込む。
    /// </summary>
    /// <param name="storageFile">カスタム定義のJSONファイルパス。</param>
    /// <returns>Conventional Commit型のリスト。</returns>
    public static List<ConventionalCommitType> Load(string storageFile)
    {
        try
        {
            // カスタム定義ファイルが存在する場合はそこから読み込み
            if (!string.IsNullOrEmpty(storageFile) && File.Exists(storageFile))
                return JsonSerializer.Deserialize(File.ReadAllText(storageFile), JsonCodeGen.Default.ListConventionalCommitType) ?? [];
        }
        catch
        {
            // Ignore errors.
        }

        // デフォルトのConventional Commit型リストを返す
        return new List<ConventionalCommitType> {
            new("Features", "feat", "Adding a new feature"),
            new("Bug Fixes", "fix", "Fixing a bug"),
            new("Work In Progress", "wip", "Still being developed and not yet complete"),
            new("Reverts", "revert", "Undoing a previous commit"),
            new("Code Refactoring", "refactor", "Restructuring code without changing its external behavior"),
            new("Performance Improvements", "perf", "Improves performance"),
            new("Builds", "build", "Changes that affect the build system or external dependencies"),
            new("Continuous Integrations", "ci", "Changes to CI configuration files and scripts"),
            new("Documentations", "docs", "Updating documentation"),
            new("Styles", "style", "Elements or code styles without changing the code logic"),
            new("Tests", "test", "Adding or updating tests"),
            new("Chores", "chore", "Other changes that don't modify src or test files"),
        };
    }
}
