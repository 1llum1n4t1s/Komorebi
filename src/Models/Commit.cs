using System;
using System.Collections.Generic;

namespace Komorebi.Models
{
    /// <summary>
    ///     コミット検索の方法を表す列挙型。
    /// </summary>
    public enum CommitSearchMethod
    {
        /// <summary>
        ///     SHAハッシュで検索。
        /// </summary>
        BySHA = 0,

        /// <summary>
        ///     作者名で検索。
        /// </summary>
        ByAuthor,

        /// <summary>
        ///     コミッター名で検索。
        /// </summary>
        ByCommitter,

        /// <summary>
        ///     コミットメッセージで検索。
        /// </summary>
        ByMessage,

        /// <summary>
        ///     ファイルパスで検索。
        /// </summary>
        ByPath,

        /// <summary>
        ///     変更内容で検索。
        /// </summary>
        ByContent,
    }

    /// <summary>
    ///     Gitコミットの情報を表すクラス。
    /// </summary>
    public class Commit
    {
        /// <summary>
        ///     空ツリーのSHA1ハッシュ（初回コミットのdiff比較に使用）。
        /// </summary>
        public const string EmptyTreeSHA1 = "4b825dc642cb6eb9a060e54bf8d69288fbee4904";

        /// <summary>
        ///     コミットのSHAハッシュ。
        /// </summary>
        public string SHA { get; set; } = string.Empty;

        /// <summary>
        ///     コミットの作者。
        /// </summary>
        public User Author { get; set; } = User.Invalid;

        /// <summary>
        ///     作者の日時（Unixタイムスタンプ）。
        /// </summary>
        public ulong AuthorTime { get; set; } = 0;

        /// <summary>
        ///     コミッター（実際にコミットした人）。
        /// </summary>
        public User Committer { get; set; } = User.Invalid;

        /// <summary>
        ///     コミット日時（Unixタイムスタンプ）。
        /// </summary>
        public ulong CommitterTime { get; set; } = 0;

        /// <summary>
        ///     コミットメッセージの件名（1行目）。
        /// </summary>
        public string Subject { get; set; } = string.Empty;

        /// <summary>
        ///     親コミットのSHAリスト。
        /// </summary>
        public List<string> Parents { get; set; } = new();

        /// <summary>
        ///     このコミットに付けられたデコレーター（ブランチ、タグ等）のリスト。
        /// </summary>
        public List<Decorator> Decorators { get; set; } = new();

        /// <summary>
        ///     現在のブランチにマージ済みかどうか。
        /// </summary>
        public bool IsMerged { get; set; } = false;

        /// <summary>
        ///     コミットグラフでの表示色インデックス。
        /// </summary>
        public int Color { get; set; } = 0;

        /// <summary>
        ///     コミットグラフでの左マージン。
        /// </summary>
        public double LeftMargin { get; set; } = 0;

        /// <summary>
        ///     フォーマット済みの作者日時文字列。
        /// </summary>
        public string AuthorTimeStr => DateTime.UnixEpoch.AddSeconds(AuthorTime).ToLocalTime().ToString(DateTimeFormat.Active.DateTime);

        /// <summary>
        ///     フォーマット済みのコミッター日時文字列。
        /// </summary>
        public string CommitterTimeStr => DateTime.UnixEpoch.AddSeconds(CommitterTime).ToLocalTime().ToString(DateTimeFormat.Active.DateTime);

        /// <summary>
        ///     フォーマット済みの作者日付文字列（日付のみ）。
        /// </summary>
        public string AuthorTimeShortStr => DateTime.UnixEpoch.AddSeconds(AuthorTime).ToLocalTime().ToString(DateTimeFormat.Active.DateOnly);

        /// <summary>
        ///     フォーマット済みのコミッター日付文字列（日付のみ）。
        /// </summary>
        public string CommitterTimeShortStr => DateTime.UnixEpoch.AddSeconds(CommitterTime).ToLocalTime().ToString(DateTimeFormat.Active.DateOnly);

        /// <summary>
        ///     コミッター情報を表示すべきかどうか（作者と異なる場合）。
        /// </summary>
        public bool IsCommitterVisible => !Author.Equals(Committer) || AuthorTime != CommitterTime;

        /// <summary>
        ///     現在のHEADコミットかどうか。
        /// </summary>
        public bool IsCurrentHead => Decorators.Find(x => x.Type is DecoratorType.CurrentBranchHead or DecoratorType.CurrentCommitHead) != null;

        /// <summary>
        ///     デコレーターが存在するかどうか。
        /// </summary>
        public bool HasDecorators => Decorators.Count > 0;

        /// <summary>
        ///     コミットの表示用名前を取得する（ブランチ名、タグ名、またはSHAの先頭10文字）。
        /// </summary>
        /// <returns>表示用の名前文字列。</returns>
        public string GetFriendlyName()
        {
            // ブランチデコレーターを優先
            var branchDecorator = Decorators.Find(x => x.Type is DecoratorType.LocalBranchHead or DecoratorType.RemoteBranchHead);
            if (branchDecorator != null)
                return branchDecorator.Name;

            // 次にタグデコレーターを使用
            var tagDecorator = Decorators.Find(x => x.Type is DecoratorType.Tag);
            if (tagDecorator != null)
                return tagDecorator.Name;

            // デコレーターがなければSHAの先頭10文字を返す
            return SHA[..10];
        }

        /// <summary>
        ///     親コミットのSHAデータを解析してParentsリストに追加する。
        /// </summary>
        /// <param name="data">スペース区切りの親コミットSHA文字列。</param>
        public void ParseParents(string data)
        {
            // 短すぎるデータは無視
            if (data.Length < 8)
                return;

            Parents.AddRange(data.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        /// <summary>
        ///     デコレーター文字列を解析してDecoratorsリストに追加する。
        /// </summary>
        /// <param name="data">カンマ区切りのデコレーター文字列。</param>
        public void ParseDecorators(string data)
        {
            // 短すぎるデータは無視
            if (data.Length < 3)
                return;

            var subs = data.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var sub in subs)
            {
                var d = sub.Trim();

                // リモートのHEAD参照はスキップ
                if (d.EndsWith("/HEAD", StringComparison.Ordinal))
                    continue;

                // タグ参照
                if (d.StartsWith("tag: refs/tags/", StringComparison.Ordinal))
                {
                    Decorators.Add(new Decorator()
                    {
                        Type = DecoratorType.Tag,
                        Name = d.Substring(15),
                    });
                }
                // 現在のブランチHEAD
                else if (d.StartsWith("HEAD -> refs/heads/", StringComparison.Ordinal))
                {
                    IsMerged = true;
                    Decorators.Add(new Decorator()
                    {
                        Type = DecoratorType.CurrentBranchHead,
                        Name = d.Substring(19),
                    });
                }
                // デタッチドHEAD
                else if (d.Equals("HEAD"))
                {
                    IsMerged = true;
                    Decorators.Add(new Decorator()
                    {
                        Type = DecoratorType.CurrentCommitHead,
                        Name = d,
                    });
                }
                // ローカルブランチ
                else if (d.StartsWith("refs/heads/", StringComparison.Ordinal))
                {
                    Decorators.Add(new Decorator()
                    {
                        Type = DecoratorType.LocalBranchHead,
                        Name = d.Substring(11),
                    });
                }
                // リモートブランチ
                else if (d.StartsWith("refs/remotes/", StringComparison.Ordinal))
                {
                    Decorators.Add(new Decorator()
                    {
                        Type = DecoratorType.RemoteBranchHead,
                        Name = d.Substring(13),
                    });
                }
            }

            // タイプ順、同タイプ内は名前の自然順でソート
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
    ///     コミットの完全なメッセージとインライン要素を保持するクラス。
    /// </summary>
    public class CommitFullMessage
    {
        /// <summary>
        ///     コミットメッセージ全文。
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        ///     メッセージ中のインライン要素（リンク等）のコレクション。
        /// </summary>
        public InlineElementCollector Inlines { get; set; } = new();
    }
}
