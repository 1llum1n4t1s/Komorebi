// nullable 移行未実施。1 ファイルずつ null 注釈を入れてこの 2 行を削除していく。
#nullable disable warnings
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Avalonia.Collections;

namespace Komorebi.Models;

/// <summary>
/// リポジトリのUI状態を保持するクラス。
/// gitディレクトリ内の「komorebi.uistates」ファイルにシリアライズされる。
/// </summary>
public class RepositoryUIStates
{
    /// <summary>履歴表示フラグ</summary>
    public HistoryShowFlags HistoryShowFlags
    {
        get;
        set;
    } = HistoryShowFlags.None;

    /// <summary>履歴の作者列を表示するかどうか</summary>
    public bool IsAuthorColumnVisibleInHistory
    {
        get;
        set;
    } = true;

    /// <summary>
    /// 履歴の作者列の幅（ピクセル）。DataGrid内蔵リサイズではなくカスタムリサイザーで制御し、
    /// リポジトリ単位のUI状態として永続化する (upstream aa9290be)。
    /// </summary>
    public double AuthorColumnWidth
    {
        get;
        set;
    } = 120;

    /// <summary>履歴のSHA列を表示するかどうか</summary>
    public bool IsSHAColumnVisibleInHistory
    {
        get;
        set;
    } = true;

    /// <summary>履歴の作者日時列を表示するかどうか (upstream 87766dd6)</summary>
    public bool IsAuthorTimeColumnVisibleInHistory
    {
        get;
        set;
    } = false;

    /// <summary>履歴のコミット日時列を表示するかどうか (upstream 87766dd6)</summary>
    public bool IsCommitTimeColumnVisibleInHistory
    {
        get;
        set;
    } = true;

    /// <summary>履歴でトポロジカル順序を使用するかどうか</summary>
    public bool EnableTopoOrderInHistory
    {
        get;
        set;
    } = false;

    /// <summary>履歴で現在のブランチのみをハイライトするかどうか</summary>
    public bool OnlyHighlightCurrentBranchInHistory
    {
        get;
        set;
    } = false;

    /// <summary>ローカルブランチのソートモード</summary>
    public BranchSortMode LocalBranchSortMode
    {
        get;
        set;
    } = BranchSortMode.Name;

    /// <summary>リモートブランチのソートモード</summary>
    public BranchSortMode RemoteBranchSortMode
    {
        get;
        set;
    } = BranchSortMode.Name;

    /// <summary>タグをツリー表示するかどうか</summary>
    public bool ShowTagsAsTree
    {
        get;
        set;
    } = false;

    /// <summary>タグのソートモード</summary>
    public TagSortMode TagSortMode
    {
        get;
        set;
    } = TagSortMode.CreatorDate;

    /// <summary>サブモジュールをツリー表示するかどうか</summary>
    public bool ShowSubmodulesAsTree
    {
        get;
        set;
    } = false;

    /// <summary>ローカル変更に未追跡ファイルを含めるかどうか</summary>
    public bool IncludeUntrackedInLocalChanges
    {
        get;
        set;
    } = true;

    /// <summary>フェッチ時に強制オプションを有効にするかどうか</summary>
    public bool EnableForceOnFetch
    {
        get;
        set;
    } = false;

    /// <summary>全リモートをフェッチするかどうか</summary>
    public bool FetchAllRemotes
    {
        get;
        set;
    } = false;

    /// <summary>タグなしでフェッチするかどうか</summary>
    public bool FetchWithoutTags
    {
        get;
        set;
    } = false;

    /// <summary>マージの代わりにリベースを優先するかどうか</summary>
    public bool PreferRebaseInsteadOfMerge
    {
        get;
        set;
    } = true;

    /// <summary>プッシュ時にサブモジュールを確認するかどうか</summary>
    public bool CheckSubmodulesOnPush
    {
        get;
        set;
    } = true;

    /// <summary>全タグをプッシュするかどうか</summary>
    public bool PushAllTags
    {
        get;
        set;
    } = false;

    /// <summary>注釈付きタグを作成するかどうか</summary>
    public bool CreateAnnotatedTag
    {
        get;
        set;
    } = true;

    /// <summary>タグ作成時にリモートへプッシュするかどうか</summary>
    public bool PushToRemoteWhenCreateTag
    {
        get;
        set;
    } = true;

    /// <summary>タグ削除時にリモートへプッシュするかどうか</summary>
    public bool PushToRemoteWhenDeleteTag
    {
        get;
        set;
    } = false;

    /// <summary>ブランチ作成時にチェックアウトするかどうか</summary>
    public bool CheckoutBranchOnCreateBranch
    {
        get;
        set;
    } = true;

    /// <summary>ブランチ作成時にリモートへプッシュするかどうか</summary>
    public bool PushToRemoteWhenCreateBranch
    {
        get;
        set;
    } = false;

    /// <summary>コミット時にSign-offを有効にするかどうか</summary>
    public bool EnableSignOffForCommit
    {
        get;
        set;
    } = false;

    /// <summary>コミット時にフックの検証をスキップするかどうか</summary>
    public bool NoVerifyOnCommit
    {
        get;
        set;
    } = false;

    /// <summary>スタッシュ時に未追跡ファイルを含めるかどうか</summary>
    public bool IncludeUntrackedWhenStash
    {
        get;
        set;
    } = true;

    /// <summary>スタッシュ時にステージング済みのみを対象にするかどうか</summary>
    public bool OnlyStagedWhenStash
    {
        get;
        set;
    } = false;

    /// <summary>スタッシュ後の変更の処理方法</summary>
    public int ChangesAfterStashing
    {
        get;
        set;
    } = 0;

    /// <summary>サイドバーでローカルブランチを展開するかどうか</summary>
    public bool IsLocalBranchesExpandedInSideBar
    {
        get;
        set;
    } = true;

    /// <summary>サイドバーでリモートを展開するかどうか</summary>
    public bool IsRemotesExpandedInSideBar
    {
        get;
        set;
    } = true;

    /// <summary>サイドバーでタグを展開するかどうか</summary>
    public bool IsTagsExpandedInSideBar
    {
        get;
        set;
    } = false;

    /// <summary>サイドバーでサブモジュールを展開するかどうか</summary>
    public bool IsSubmodulesExpandedInSideBar
    {
        get;
        set;
    } = true;

    /// <summary>サイドバーでワークツリーを展開するかどうか</summary>
    public bool IsWorktreeExpandedInSideBar
    {
        get;
        set;
    } = false;

    /// <summary>サイドバーで展開されているブランチノードのパスリスト</summary>
    public List<string> ExpandedBranchNodesInSideBar
    {
        get;
        set;
    } = [];

    /// <summary>最後に使用したコミットメッセージ</summary>
    public string LastCommitMessage
    {
        get;
        set;
    } = string.Empty;

    /// <summary>履歴フィルターのリスト</summary>
    public AvaloniaList<HistoryFilter> HistoryFilters
    {
        get;
        set;
    } = [];

    /// <summary>
    /// gitディレクトリからUI状態を読み込む
    /// </summary>
    /// <param name="gitDir">gitディレクトリのパス</param>
    /// <returns>読み込まれたUI状態</returns>
    public static RepositoryUIStates Load(string gitDir)
    {
        var fileInfo = new FileInfo(Path.Combine(gitDir, "komorebi.uistates"));
        var fullpath = fileInfo.FullName;

        RepositoryUIStates states;
        if (!File.Exists(fullpath))
        {
            states = new RepositoryUIStates();
        }
        else
        {
            try
            {
                using var stream = File.OpenRead(fullpath);
                states = JsonSerializer.Deserialize(stream, JsonCodeGen.Default.RepositoryUIStates);
            }
            catch
            {
                states = new RepositoryUIStates();
            }
        }

        // JsonSerializer.Deserialize は JSON が "null" や空の場合に例外を投げず null を返す。
        // その場合は新規インスタンスにフォールバックしないと、直後の states._file 代入で NRE が起きる。
        // RepositorySettings.Get() に同等のガードがあるのに対し、こちらに対称ガードが無かったため追加。
        states ??= new RepositoryUIStates();

        states._file = fullpath;
        return states;
    }

    /// <summary>
    /// UI状態をファイルに保存してアンロードする
    /// </summary>
    /// <param name="lastCommitMessage">最後のコミットメッセージ</param>
    public void Unload(string lastCommitMessage)
    {
        try
        {
            LastCommitMessage = lastCommitMessage;
            // upstream 50367b7e: アトミック書き込み (tmp に書いてから rename) で書き込み途中のクラッシュ・電源断で
            // UI 状態ファイルが破損するのを防ぐ。
            // 同一リポジトリ並行操作時の tmp ファイル衝突を避けるためランダム拡張子付き。
            var content = JsonSerializer.Serialize(this, JsonCodeGen.Default.RepositoryUIStates);
            var tmpfile = $"{_file}.tmp.{Guid.NewGuid():N}";
            File.WriteAllText(tmpfile, content);
            File.Move(tmpfile, _file, true);
        }
        catch
        {
            // Ignore save errors
        }
    }

    /// <summary>
    /// 指定パターンの履歴フィルターモードを取得する
    /// </summary>
    /// <param name="pattern">フィルターパターン（nullの場合は最初のフィルターのモードを返す）</param>
    /// <returns>フィルターモード</returns>
    public FilterMode GetHistoryFilterMode(string pattern = null)
    {
        if (string.IsNullOrEmpty(pattern))
            return HistoryFilters.Count == 0 ? FilterMode.None : HistoryFilters[0].Mode;

        foreach (var filter in HistoryFilters)
        {
            if (filter.Pattern.Equals(pattern, StringComparison.Ordinal))
                return filter.Mode;
        }

        return FilterMode.None;
    }

    /// <summary>履歴フィルターをパターンとモードのマップとして取得する</summary>
    public Dictionary<string, FilterMode> GetHistoryFiltersMap()
    {
        Dictionary<string, FilterMode> map = [];
        foreach (var filter in HistoryFilters)
            map.Add(filter.Pattern, filter.Mode);
        return map;
    }

    /// <summary>
    /// 履歴フィルターを更新する。異なるモードのフィルターがある場合は全フィルターをクリアする。
    /// </summary>
    /// <param name="pattern">フィルターパターン</param>
    /// <param name="type">フィルタータイプ</param>
    /// <param name="mode">フィルターモード</param>
    /// <returns>フィルターが変更された場合true</returns>
    public bool UpdateHistoryFilters(string pattern, FilterType type, FilterMode mode)
    {
        // モードが異なるフィルターがある場合は全フィルターをクリア
        if (mode != FilterMode.None)
        {
            var clear = false;
            foreach (var filter in HistoryFilters)
            {
                if (filter.Mode != mode)
                {
                    clear = true;
                    break;
                }
            }

            if (clear)
            {
                HistoryFilters.Clear();
                HistoryFilters.Add(new HistoryFilter(pattern, type, mode));
                return true;
            }
        }
        else
        {
            for (int i = 0; i < HistoryFilters.Count; i++)
            {
                var filter = HistoryFilters[i];
                if (filter.Type == type && filter.Pattern.Equals(pattern, StringComparison.Ordinal))
                {
                    HistoryFilters.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        foreach (var filter in HistoryFilters)
        {
            if (filter.Type != type)
                continue;

            if (filter.Pattern.Equals(pattern, StringComparison.Ordinal))
                return false;
        }

        HistoryFilters.Add(new HistoryFilter(pattern, type, mode));
        return true;
    }

    /// <summary>指定パターンとタイプの履歴フィルターを削除する</summary>
    public void RemoveHistoryFilter(string pattern, FilterType type)
    {
        foreach (var filter in HistoryFilters)
        {
            if (filter.Type == type && filter.Pattern.Equals(pattern, StringComparison.Ordinal))
            {
                HistoryFilters.Remove(filter);
                break;
            }
        }
    }

    /// <summary>ローカルブランチフィルターのブランチ名を変更する</summary>
    public void RenameBranchFilter(string oldName, string newName)
    {
        foreach (var filter in HistoryFilters)
        {
            if (filter.Type == FilterType.LocalBranch &&
                filter.Pattern.Equals(oldName, StringComparison.Ordinal))
            {
                filter.Pattern = $"refs/heads/{newName}";
                break;
            }
        }
    }

    /// <summary>指定プレフィックスに一致するブランチフィルターを一括削除する</summary>
    public void RemoveBranchFiltersByPrefix(string pattern)
    {
        List<HistoryFilter> dirty = [];
        var prefix = $"{pattern}/";

        foreach (var filter in HistoryFilters)
        {
            if (filter.Type == FilterType.Tag)
                continue;

            if (filter.Pattern.StartsWith(prefix, StringComparison.Ordinal))
                dirty.Add(filter);
        }

        foreach (var filter in dirty)
            HistoryFilters.Remove(filter);
    }

    /// <summary>
    /// 現在のフィルター設定からgit logコマンドのパラメータ文字列を構築する
    /// </summary>
    /// <param name="gitDir">.gitディレクトリのパス（除外フィルタのみの場合、進行中操作の判定に使用する）</param>
    /// <returns>git logのパラメータ文字列</returns>
    public string BuildHistoryParams(string gitDir)
    {
        var builder = new StringBuilder();

        if (EnableTopoOrderInHistory)
            builder.Append("--topo-order ");
        else
            builder.Append("--date-order ");

        if (HistoryShowFlags.HasFlag(HistoryShowFlags.Reflog))
            builder.Append("--reflog ");

        if (HistoryShowFlags.HasFlag(HistoryShowFlags.FirstParentOnly))
            builder.Append("--first-parent ");

        if (HistoryShowFlags.HasFlag(HistoryShowFlags.SimplifyByDecoration))
            builder.Append("--simplify-by-decoration ");

        var mode = GetHistoryFilterMode();
        if (mode == FilterMode.None)
            builder.Append("--branches --remotes --tags HEAD");
        else if (mode == FilterMode.Included)
            BuildHistoryParamsForIncluded(builder);
        else
            BuildHistoryParamsForExcluded(builder, gitDir);

        return builder.ToString();
    }

    /// <summary>
    /// 「含める」モードのフィルターから対象refの列挙パラメータを構築する
    /// </summary>
    private void BuildHistoryParamsForIncluded(StringBuilder builder)
    {
        foreach (var filter in HistoryFilters)
        {
            if (filter.Type == FilterType.LocalBranch)
                builder.Append(filter.Pattern).Append(' ');
            else if (filter.Type == FilterType.LocalBranchFolder)
                builder.Append($"--branches={filter.Pattern.AsSpan(11)}/* ");
            else if (filter.Type == FilterType.RemoteBranch)
                builder.Append(filter.Pattern).Append(' ');
            else if (filter.Type == FilterType.RemoteBranchFolder)
                builder.Append($"--remotes={filter.Pattern.AsSpan(13)}/* ");
            else if (filter.Type == FilterType.Tag)
                builder.Append($"refs/tags/{filter.Pattern} ");
        }
    }

    /// <summary>
    /// 「除外する」モードのフィルターから除外パラメータを構築する。
    /// チェリーピック/リベース/マージ/リバートの進行中は、除外フィルタのみだとHEADが
    /// グラフから消えてしまうため、明示的にHEADを含める (upstream d5824534)。
    /// </summary>
    private void BuildHistoryParamsForExcluded(StringBuilder builder, string gitDir)
    {
        List<string> excludedBranches = [];
        List<string> excludedRemotes = [];
        List<string> excludedTags = [];
        foreach (var filter in HistoryFilters)
        {
            if (filter.Type == FilterType.LocalBranch)
                excludedBranches.Add($"--exclude=\"{filter.Pattern.Substring(11).Escaped()}\" --decorate-refs-exclude=\"{filter.Pattern.Escaped()}\" ");
            else if (filter.Type == FilterType.LocalBranchFolder)
                excludedBranches.Add($"--exclude=\"{filter.Pattern.Substring(11).Escaped()}/*\" --decorate-refs-exclude=\"{filter.Pattern.Escaped()}/*\" ");
            else if (filter.Type == FilterType.RemoteBranch)
                excludedRemotes.Add($"--exclude=\"{filter.Pattern.Substring(13).Escaped()}\" --decorate-refs-exclude=\"{filter.Pattern.Escaped()}\" ");
            else if (filter.Type == FilterType.RemoteBranchFolder)
                excludedRemotes.Add($"--exclude=\"{filter.Pattern.Substring(13).Escaped()}/*\" --decorate-refs-exclude=\"{filter.Pattern.Escaped()}/*\" ");
            else if (filter.Type == FilterType.Tag)
                excludedTags.Add($"--exclude=\"{filter.Pattern.Escaped()}\" --decorate-refs-exclude=\"refs/tags/{filter.Pattern.Escaped()}\" ");
        }

        foreach (var b in excludedBranches)
            builder.Append(b);

        builder.Append("--branches ");

        var isInProgress = File.Exists(Path.Combine(gitDir, "CHERRY_PICK_HEAD")) ||
            Directory.Exists(Path.Combine(gitDir, "rebase-merge")) ||
            Directory.Exists(Path.Combine(gitDir, "rebase-apply")) ||
            File.Exists(Path.Combine(gitDir, "REVERT_HEAD")) ||
            File.Exists(Path.Combine(gitDir, "MERGE_HEAD"));
        if (isInProgress)
            builder.Append("HEAD ");

        foreach (var r in excludedRemotes)
            builder.Append(r);

        builder.Append("--exclude=origin/HEAD --remotes ");

        foreach (var t in excludedTags)
            builder.Append(t);

        builder.Append("--tags ");
    }

    /// <summary>UI状態ファイルのフルパス</summary>
    private string _file = string.Empty;
}
