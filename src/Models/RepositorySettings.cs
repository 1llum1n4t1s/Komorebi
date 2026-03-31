using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Avalonia.Collections;

namespace Komorebi.Models;

/// <summary>
/// リポジトリ固有の設定を保持するクラス。
/// gitの共通ディレクトリ内の「komorebi.settings」ファイルにシリアライズされる。
/// </summary>
public class RepositorySettings
{
    /// <summary>デフォルトのリモート名</summary>
    public string DefaultRemote
    {
        get;
        set;
    } = string.Empty;

    /// <summary>優先するマージモード</summary>
    public int PreferredMergeMode
    {
        get;
        set;
    } = 0;

    /// <summary>Conventional Commitsのタイプ上書き設定</summary>
    public string ConventionalTypesOverride
    {
        get;
        set;
    } = string.Empty;

    /// <summary>自動フェッチを有効にするかどうか</summary>
    public bool EnableAutoFetch
    {
        get;
        set;
    } = false;

    /// <summary>自動フェッチの間隔（分）</summary>
    public int AutoFetchInterval
    {
        get;
        set;
    } = 10;

    /// <summary>サブモジュール自動更新前に確認するかどうか</summary>
    public bool AskBeforeAutoUpdatingSubmodules
    {
        get;
        set;
    } = false;

    /// <summary>優先するOpenAIサービス名</summary>
    public string PreferredOpenAIService
    {
        get;
        set;
    } = "---";

    /// <summary>コミットメッセージテンプレートのリスト</summary>
    public AvaloniaList<CommitTemplate> CommitTemplates
    {
        get;
        set;
    } = [];

    /// <summary>最近使用したコミットメッセージの履歴（最大10件）</summary>
    public AvaloniaList<string> CommitMessages
    {
        get;
        set;
    } = [];

    /// <summary>カスタムアクションのリスト</summary>
    public AvaloniaList<CustomAction> CustomActions
    {
        get;
        set;
    } = [];

    /// <summary>
    /// 指定されたgit共通ディレクトリからリポジトリ設定を取得する（キャッシュ付き）
    /// </summary>
    /// <param name="gitCommonDir">gitの共通ディレクトリパス</param>
    /// <returns>リポジトリ設定インスタンス</returns>
    public static RepositorySettings Get(string gitCommonDir)
    {
        var fileInfo = new FileInfo(Path.Combine(gitCommonDir, "komorebi.settings"));
        var fullpath = fileInfo.FullName;
        if (_cache.TryGetValue(fullpath, out var setting))
            return setting;

        if (!File.Exists(fullpath))
        {
            setting = new();
        }
        else
        {
            try
            {
                using var stream = File.OpenRead(fullpath);
                setting = JsonSerializer.Deserialize(stream, JsonCodeGen.Default.RepositorySettings);
            }
            catch
            {
                setting = new();
            }
        }

        // Serialize setting again to make sure there are no unnecessary whitespaces.
        Task.Run(() =>
        {
            var formatted = JsonSerializer.Serialize(setting, JsonCodeGen.Default.RepositorySettings);
            setting._orgHash = HashContent(formatted);
        });

        setting._file = fullpath;
        _cache.Add(fullpath, setting);
        return setting;
    }

    /// <summary>
    /// 設定をファイルに非同期で保存する。内容が変更されていない場合は保存をスキップする。
    /// </summary>
    public async Task SaveAsync()
    {
        try
        {
            var content = JsonSerializer.Serialize(this, JsonCodeGen.Default.RepositorySettings);
            var hash = HashContent(content);
            if (!hash.Equals(_orgHash, StringComparison.Ordinal))
            {
                await File.WriteAllTextAsync(_file, content);
                _orgHash = hash;
            }
        }
        catch
        {
            // Ignore save errors
        }
    }

    /// <summary>
    /// コミットメッセージ履歴に追加する。既存の場合は先頭に移動し、最大10件を保持する。
    /// </summary>
    /// <param name="message">追加するコミットメッセージ</param>
    public void PushCommitMessage(string message)
    {
        message = message.Trim().ReplaceLineEndings("\n");
        var existIdx = CommitMessages.IndexOf(message);
        if (existIdx == 0)
            return;

        if (existIdx > 0)
        {
            CommitMessages.Move(existIdx, 0);
            return;
        }

        if (CommitMessages.Count > 9)
            CommitMessages.RemoveRange(9, CommitMessages.Count - 9);

        CommitMessages.Insert(0, message);
    }

    /// <summary>新しいカスタムアクションを作成してリストに追加する</summary>
    public CustomAction AddNewCustomAction()
    {
        var act = new CustomAction() { Name = "Unnamed Action" };
        CustomActions.Add(act);
        return act;
    }

    /// <summary>指定されたカスタムアクションをリストから削除する</summary>
    public void RemoveCustomAction(CustomAction act)
    {
        if (act is not null)
            CustomActions.Remove(act);
    }

    /// <summary>カスタムアクションをリスト内で1つ上に移動する</summary>
    public void MoveCustomActionUp(CustomAction act)
    {
        var idx = CustomActions.IndexOf(act);
        if (idx > 0)
            CustomActions.Move(idx - 1, idx);
    }

    /// <summary>カスタムアクションをリスト内で1つ下に移動する</summary>
    public void MoveCustomActionDown(CustomAction act)
    {
        var idx = CustomActions.IndexOf(act);
        if (idx < CustomActions.Count - 1)
            CustomActions.Move(idx + 1, idx);
    }

    /// <summary>文字列のMD5ハッシュを計算して16進文字列で返す</summary>
    private static string HashContent(string source)
    {
        var hash = MD5.HashData(Encoding.Default.GetBytes(source));
        // パフォーマンス: ループ内のToString("x2")による16回の文字列割り当てを排除
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>設定ファイルのキャッシュ（フルパス→設定インスタンス）</summary>
    private static Dictionary<string, RepositorySettings> _cache = [];
    /// <summary>設定ファイルのフルパス</summary>
    private string _file = string.Empty;
    /// <summary>最後に保存した内容のMD5ハッシュ（変更検出用）</summary>
    private string _orgHash = string.Empty;
}
