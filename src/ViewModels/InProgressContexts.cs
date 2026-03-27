using System.IO;
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
///     進行中のGit操作（cherry-pick, rebase, revert, merge）の基底クラス。
///     continue/skip/abort操作を共通で提供する。
/// </summary>
public abstract class InProgressContext
{
    /// <summary>操作名（例: "Cherry-Pick", "Rebase"）。</summary>
    public string Name
    {
        get;
        protected set;
    }

    /// <summary>進捗テキスト（例: "(3/10)"）。サブクラスでオーバーライド可能。</summary>
    public virtual string ProgressText => string.Empty;
    /// <summary>進捗表示が有効かどうか。</summary>
    public virtual bool HasProgress => false;

    /// <summary>操作を続行する（git xxx --continue）。</summary>
    public async Task ContinueAsync(CommandLog log)
    {
        if (_continueCmd is not null)
            await _continueCmd.Use(log).ExecAsync();
    }

    /// <summary>現在のステップをスキップする（git xxx --skip）。</summary>
    public async Task SkipAsync(CommandLog log)
    {
        if (_skipCmd is not null)
            await _skipCmd.Use(log).ExecAsync();
    }

    /// <summary>操作を中断する（git xxx --abort）。</summary>
    public async Task AbortAsync(CommandLog log)
    {
        if (_abortCmd is not null)
            await _abortCmd.Use(log).ExecAsync();
    }

    /// <summary>続行コマンド。</summary>
    protected Commands.Command _continueCmd = null;
    /// <summary>スキップコマンド。</summary>
    protected Commands.Command _skipCmd = null;
    /// <summary>中断コマンド。</summary>
    protected Commands.Command _abortCmd = null;
}

/// <summary>
///     進行中のcherry-pick操作のコンテキスト。
///     CHERRY_PICK_HEADファイルからチェリーピック対象のコミット情報を取得する。
/// </summary>
public class CherryPickInProgress : InProgressContext
{
    /// <summary>チェリーピック対象のコミット。</summary>
    public Models.Commit Head
    {
        get;
    }

    /// <summary>チェリーピック対象コミットの表示名。</summary>
    public string HeadName
    {
        get;
    }

    /// <summary>
    ///     コンストラクタ。CHERRY_PICK_HEADからコミット情報を読み込み、
    ///     continue/skip/abortコマンドを構成する。
    /// </summary>
    public CherryPickInProgress(Repository repo)
    {
        Name = "Cherry-Pick";

        _continueCmd = new Commands.Command
        {
            WorkingDirectory = repo.FullPath,
            Context = repo.FullPath,
            Args = "cherry-pick --continue",
        };

        _skipCmd = new Commands.Command
        {
            WorkingDirectory = repo.FullPath,
            Context = repo.FullPath,
            Args = "cherry-pick --skip",
        };

        _abortCmd = new Commands.Command
        {
            WorkingDirectory = repo.FullPath,
            Context = repo.FullPath,
            Args = "cherry-pick --abort",
        };

        // CHERRY_PICK_HEADファイルからチェリーピック対象のSHAを取得
        var headSHA = File.ReadAllText(Path.Combine(repo.GitDir, "CHERRY_PICK_HEAD")).Trim();
        Head = new Commands.QuerySingleCommit(repo.FullPath, headSHA).GetResult() ?? new Models.Commit() { SHA = headSHA };
        HeadName = Head.GetFriendlyName();
    }
}

/// <summary>
///     進行中のrebase操作のコンテキスト。
///     rebase-merge/rebase-applyディレクトリから進捗情報を読み込む。
/// </summary>
public class RebaseInProgress : InProgressContext
{
    /// <summary>リベース元のブランチ名。</summary>
    public string HeadName
    {
        get;
    }

    /// <summary>リベース先（onto）のコミット表示名。</summary>
    public string BaseName
    {
        get;
    }

    /// <summary>リベースが停止したコミット。</summary>
    public Models.Commit StoppedAt
    {
        get;
    }

    /// <summary>リベース先（onto）のコミット。</summary>
    public Models.Commit Onto
    {
        get;
    }

    /// <summary>現在のリベースステップ番号。</summary>
    public int CurrentStep
    {
        get;
    }

    /// <summary>リベースの総ステップ数。</summary>
    public int TotalSteps
    {
        get;
    }

    /// <summary>進捗テキスト（例: "(3/10)"）。</summary>
    public override string ProgressText => TotalSteps > 0 ? $"({CurrentStep}/{TotalSteps})" : string.Empty;
    public override bool HasProgress => TotalSteps > 0;

    /// <summary>
    ///     コンストラクタ。rebase-merge/rebase-applyディレクトリから
    ///     進捗・ブランチ名・停止位置・onto情報を読み込む。
    /// </summary>
    public RebaseInProgress(Repository repo)
    {
        Name = "Rebase";

        _continueCmd = new Commands.Command
        {
            WorkingDirectory = repo.FullPath,
            Context = repo.FullPath,
            Editor = Commands.Command.EditorType.RebaseEditor,
            Args = "rebase --continue",
        };

        _skipCmd = new Commands.Command
        {
            WorkingDirectory = repo.FullPath,
            Context = repo.FullPath,
            Args = "rebase --skip",
        };

        _abortCmd = new Commands.Command
        {
            WorkingDirectory = repo.FullPath,
            Context = repo.FullPath,
            Args = "rebase --abort",
        };

        // rebase-mergeまたはrebase-applyディレクトリから進捗情報を取得
        var rebaseMergeDir = Path.Combine(repo.GitDir, "rebase-merge");
        var rebaseApplyDir = Path.Combine(repo.GitDir, "rebase-apply");

        if (Directory.Exists(rebaseMergeDir))
        {
            var msgnumPath = Path.Combine(rebaseMergeDir, "msgnum");
            var endPath = Path.Combine(rebaseMergeDir, "end");

            if (File.Exists(msgnumPath) && int.TryParse(File.ReadAllText(msgnumPath).Trim(), out var msgnum))
                CurrentStep = msgnum;
            if (File.Exists(endPath) && int.TryParse(File.ReadAllText(endPath).Trim(), out var end))
                TotalSteps = end;
        }
        else if (Directory.Exists(rebaseApplyDir))
        {
            var nextPath = Path.Combine(rebaseApplyDir, "next");
            var lastPath = Path.Combine(rebaseApplyDir, "last");

            if (File.Exists(nextPath) && int.TryParse(File.ReadAllText(nextPath).Trim(), out var next))
                CurrentStep = next;
            if (File.Exists(lastPath) && int.TryParse(File.ReadAllText(lastPath).Trim(), out var last))
                TotalSteps = last;
        }

        // head-nameファイルからブランチ名を取得し、refs/heads/等のプレフィックスを除去
        HeadName = File.ReadAllText(Path.Combine(repo.GitDir, "rebase-merge", "head-name")).Trim();
        if (HeadName.StartsWith("refs/heads/"))
            HeadName = HeadName.Substring("refs/heads/".Length);
        else if (HeadName.StartsWith("refs/tags/"))
            HeadName = HeadName.Substring("refs/tags/".Length);

        var stoppedSHAPath = Path.Combine(repo.GitDir, "rebase-merge", "stopped-sha");
        var stoppedSHA = File.Exists(stoppedSHAPath)
            ? File.ReadAllText(stoppedSHAPath).Trim()
            : new Commands.QueryRevisionByRefName(repo.FullPath, HeadName).GetResult();

        if (!string.IsNullOrEmpty(stoppedSHA))
            StoppedAt = new Commands.QuerySingleCommit(repo.FullPath, stoppedSHA).GetResult() ?? new Models.Commit() { SHA = stoppedSHA };

        var ontoSHA = File.ReadAllText(Path.Combine(repo.GitDir, "rebase-merge", "onto")).Trim();
        Onto = new Commands.QuerySingleCommit(repo.FullPath, ontoSHA).GetResult() ?? new Models.Commit() { SHA = ontoSHA };
        BaseName = Onto.GetFriendlyName();
    }
}

/// <summary>
///     進行中のrevert操作のコンテキスト。
///     REVERT_HEADファイルからリバート対象のコミット情報を取得する。
/// </summary>
public class RevertInProgress : InProgressContext
{
    /// <summary>リバート対象のコミット。</summary>
    public Models.Commit Head
    {
        get;
    }

    /// <summary>
    ///     コンストラクタ。REVERT_HEADからコミット情報を読み込み、
    ///     continue/skip/abortコマンドを構成する。
    /// </summary>
    public RevertInProgress(Repository repo)
    {
        Name = "Revert";

        _continueCmd = new Commands.Command
        {
            WorkingDirectory = repo.FullPath,
            Context = repo.FullPath,
            Args = "revert --continue",
        };

        _skipCmd = new Commands.Command
        {
            WorkingDirectory = repo.FullPath,
            Context = repo.FullPath,
            Args = "revert --skip",
        };

        _abortCmd = new Commands.Command
        {
            WorkingDirectory = repo.FullPath,
            Context = repo.FullPath,
            Args = "revert --abort",
        };

        var headSHA = File.ReadAllText(Path.Combine(repo.GitDir, "REVERT_HEAD")).Trim();
        Head = new Commands.QuerySingleCommit(repo.FullPath, headSHA).GetResult() ?? new Models.Commit() { SHA = headSHA };
    }
}

/// <summary>
///     進行中のmerge操作のコンテキスト。
///     MERGE_HEADファイルからマージ元のコミット情報を取得する。
/// </summary>
public class MergeInProgress : InProgressContext
{
    /// <summary>現在のブランチ名（マージ先）。</summary>
    public string Current
    {
        get;
    }

    /// <summary>マージ元のコミット。</summary>
    public Models.Commit Source
    {
        get;
    }

    /// <summary>マージ元コミットの表示名。</summary>
    public string SourceName
    {
        get;
    }

    /// <summary>
    ///     コンストラクタ。MERGE_HEADからマージ元コミット情報を読み込み、
    ///     continue/abortコマンドを構成する（マージにはskipがない）。
    /// </summary>
    public MergeInProgress(Repository repo)
    {
        Name = "Merge";

        _continueCmd = new Commands.Command
        {
            WorkingDirectory = repo.FullPath,
            Context = repo.FullPath,
            Args = "merge --continue",
        };

        _abortCmd = new Commands.Command
        {
            WorkingDirectory = repo.FullPath,
            Context = repo.FullPath,
            Args = "merge --abort",
        };

        Current = new Commands.QueryCurrentBranch(repo.FullPath).GetResult();

        var sourceSHA = File.ReadAllText(Path.Combine(repo.GitDir, "MERGE_HEAD")).Trim();
        Source = new Commands.QuerySingleCommit(repo.FullPath, sourceSHA).GetResult() ?? new Models.Commit() { SHA = sourceSHA };
        SourceName = Source.GetFriendlyName();
    }
}
