using System;
using System.Collections.Generic;

namespace Komorebi.Models;

/// <summary>
/// コミット検索の方法を表す列挙型。
/// </summary>
public enum CommitSearchMethod
{
    /// <summary>SHAハッシュで検索。</summary>
    BySHA = 0,
    /// <summary>著者名で検索。</summary>
    ByAuthor,
    /// <summary>コミッター名で検索。</summary>
    ByCommitter,
    /// <summary>コミットメッセージで検索。</summary>
    ByMessage,
    /// <summary>ファイルパスで検索。</summary>
    ByPath,
    /// <summary>変更内容（差分）で検索。</summary>
    ByContent,
}

/// <summary>
/// Gitコミットの情報を表すクラス。
/// SHA、著者、コミッター、親コミット、デコレーター等を保持する。
/// </summary>
public class Commit
{
    /// <summary>
    /// 空ツリーのSHA1ハッシュ。初回コミットのdiff表示に使用する。
    /// </summary>
    public const string EmptyTreeSHA1 = "4b825dc642cb6eb9a060e54bf8d69288fbee4904";

    /// <summary>コミットのSHAハッシュ。</summary>
    public string SHA { get; set; } = string.Empty;
    /// <summary>著者情報。</summary>
    public User Author { get; set; } = User.Invalid;
    /// <summary>著者の日時（Unixタイムスタンプ）。</summary>
    public ulong AuthorTime { get; set; } = 0;
    /// <summary>コミッター情報。</summary>
    public User Committer { get; set; } = User.Invalid;
    /// <summary>コミッターの日時（Unixタイムスタンプ）。</summary>
    public ulong CommitterTime { get; set; } = 0;
    /// <summary>コミットメッセージの1行目（件名）。</summary>
    public string Subject { get; set; } = string.Empty;
    /// <summary>親コミットのSHAリスト。</summary>
    public List<string> Parents { get; set; } = new();
    /// <summary>デコレーター（ブランチ名、タグ名等）のリスト。</summary>
    public List<Decorator> Decorators { get; set; } = new();

    /// <summary>現在のブランチにマージ済みかどうか（グラフ描画用）。</summary>
    public bool IsMerged { get; set; } = false;
    /// <summary>グラフ描画時の色インデックス。</summary>
    public int Color { get; set; } = 0;
    /// <summary>グラフ描画時の左マージン。</summary>
    public double LeftMargin { get; set; } = 0;

    /// <summary>著者とコミッターが異なる場合にコミッター情報を表示するかどうか。</summary>
    public bool IsCommitterVisible => !Author.Equals(Committer) || AuthorTime != CommitterTime;
    /// <summary>現在のHEADを指しているコミットかどうか。</summary>
    public bool IsCurrentHead => Decorators.Find(x => x.Type is DecoratorType.CurrentBranchHead or DecoratorType.CurrentCommitHead) is not null;
    /// <summary>デコレーターが存在するかどうか。</summary>
    public bool HasDecorators => Decorators.Count > 0;

    /// <summary>
    /// コミットの表示用フレンドリー名を取得する。
    /// ブランチ名 → タグ名 → SHA先頭10文字の優先順で返す。
    /// </summary>
    /// <returns>表示用のコミット名。</returns>
    public string GetFriendlyName()
    {
        // ブランチ名があればそれを返す
        var branchDecorator = Decorators.Find(x => x.Type is DecoratorType.LocalBranchHead or DecoratorType.RemoteBranchHead);
        if (branchDecorator is not null)
            return branchDecorator.Name;

        // タグ名があればそれを返す
        var tagDecorator = Decorators.Find(x => x.Type is DecoratorType.Tag);
        if (tagDecorator is not null)
            return tagDecorator.Name;

        // どちらもなければSHAの先頭10文字
        return SHA[..10];
    }

    /// <summary>
    /// 親コミットのSHAをスペース区切りの文字列から解析して設定する。
    /// </summary>
    /// <param name="data">スペース区切りの親コミットSHA文字列。</param>
    public void ParseParents(string data)
    {
        // 最短SHAハッシュ長未満のデータは無視
        if (data.Length < 8)
            return;

        Parents.AddRange(data.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// デコレーター（ブランチ、タグ等の参照）をカンマ区切りの文字列から解析する。
    /// git logの--decorate=full出力形式に対応する。
    /// </summary>
    /// <param name="data">カンマ区切りのデコレーター文字列。</param>
    public void ParseDecorators(string data)
    {
        if (data.Length < 3)
            return;

        var subs = data.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var sub in subs)
        {
            var d = sub.Trim();
            // リモートのHEAD参照（例: origin/HEAD）はスキップ
            if (d.EndsWith("/HEAD", StringComparison.Ordinal))
                continue;

            if (d.StartsWith("tag: refs/tags/", StringComparison.Ordinal))
            {
                // タグ参照
                Decorators.Add(new Decorator()
                {
                    Type = DecoratorType.Tag,
                    Name = d[15..],
                });
            }
            else if (d.StartsWith("HEAD -> refs/heads/", StringComparison.Ordinal))
            {
                // 現在チェックアウト中のブランチ
                IsMerged = true;
                Decorators.Add(new Decorator()
                {
                    Type = DecoratorType.CurrentBranchHead,
                    Name = d[19..],
                });
            }
            else if (d.Equals("HEAD"))
            {
                // デタッチドHEAD状態
                IsMerged = true;
                Decorators.Add(new Decorator()
                {
                    Type = DecoratorType.CurrentCommitHead,
                    Name = d,
                });
            }
            else if (d.StartsWith("refs/heads/", StringComparison.Ordinal))
            {
                // ローカルブランチ
                Decorators.Add(new Decorator()
                {
                    Type = DecoratorType.LocalBranchHead,
                    Name = d[11..],
                });
            }
            else if (d.StartsWith("refs/remotes/", StringComparison.Ordinal))
            {
                // リモートブランチ
                Decorators.Add(new Decorator()
                {
                    Type = DecoratorType.RemoteBranchHead,
                    Name = d[13..],
                });
            }
        }

        // デコレーターをタイプ順→名前順でソート
        Decorators.Sort((l, r) =>
        {
            var delta = (int)l.Type - (int)r.Type;
            if (delta != 0)
                return delta;
            return NumericSort.Compare(l.Name, r.Name);
        });
    }
}

/// <summary>
/// コミットの完全なメッセージとインライン要素（リンク等）を保持するクラス。
/// </summary>
public class CommitFullMessage
{
    /// <summary>コミットメッセージの全文。</summary>
    public string Message { get; set; } = string.Empty;
    /// <summary>メッセージ内のインライン要素（URLリンク、Issue参照等）。</summary>
    public InlineElementCollector Inlines { get; set; } = new();
}
