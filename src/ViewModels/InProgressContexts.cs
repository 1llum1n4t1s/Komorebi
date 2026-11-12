// nullable 移行未実施。1 ファイルずつ null 注釈を入れてこの 2 行を削除していく。
#nullable disable warnings
using System;
using System.IO;
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// 進行中のGit操作（cherry-pick, rebase, revert, merge）の基底クラス。
/// continue/skip/abort操作を共通で提供する。
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
        // upstream 8e17ff08 は成否を見ずに常にクリーンアップするが、abort 失敗時
        // (index.lock 残存等、RaiseError=false で黙殺される) に rebase-merge を
        // 削除すると orig-head/head-name を失い detached HEAD に取り残されるため、
        // 成功時のみ OnAborted を実行する (意図的な upstream からの逸脱)
        var succ = true;
        if (_abortCmd is not null)
            succ = await _abortCmd.Use(log).ExecAsync();

        if (succ)
            OnAborted();
    }

    /// <summary>
    /// 中断完了後のクリーンアップ処理。サブクラスでオーバーライドして
    /// 操作固有の残存ファイルを削除する。
    /// </summary>
    protected virtual void OnAborted()
    {
    }

    /// <summary>
    /// 数値 1 つだけを含む状態ファイル（rebase-merge/msgnum・rebase-apply/next 等）を読む。
    /// 存在しない・数値として解釈できない場合は false を返し、value は 0。
    /// 呼び出し側が「読めなかったら既存値を上書きしない」書き方をできるよう Try 形式にしている。
    /// </summary>
    protected static bool TryReadIntFromFile(string file, out int value)
    {
        value = 0;
        if (!File.Exists(file))
            return false;

        return int.TryParse(File.ReadAllText(file).Trim(), out value);
    }

    /// <summary>
    /// 数値 1 つだけを含む状態ファイルを読む。存在しない・不正な場合は 0。
    /// </summary>
    protected static int ReadIntFromFile(string file)
    {
        return TryReadIntFromFile(file, out var value) ? value : 0;
    }

    /// <summary>続行コマンド。</summary>
    protected Commands.Command _continueCmd = null;
    /// <summary>スキップコマンド。</summary>
    protected Commands.Command _skipCmd = null;
    /// <summary>中断コマンド。</summary>
    protected Commands.Command _abortCmd = null;
}

/// <summary>
/// 進行中のcherry-pick操作のコンテキスト。
/// CHERRY_PICK_HEADファイルからチェリーピック対象のコミット情報を取得する。
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
    /// コンストラクタ。CHERRY_PICK_HEADからコミット情報を読み込み、
    /// continue/skip/abortコマンドを構成する。
    /// </summary>
    public CherryPickInProgress(Repository repo)
    {
        Name = "Cherry-Pick";

        _continueCmd = new Commands.Command
        {
            WorkingDirectory = repo.FullPath,
            Context = repo.FullPath,
            Editor = Commands.Command.EditorType.None,
            Args = "-c core.commentChar=\"^\" -c core.commentString=\"±\" cherry-pick --continue",
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
/// 進行中のrebase操作のコンテキスト。
/// rebase-merge/rebase-applyディレクトリから進捗情報を読み込む。
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
    /// コンストラクタ。rebase-merge/rebase-applyディレクトリから
    /// 進捗・ブランチ名・停止位置・onto情報を読み込む。
    /// </summary>
    public RebaseInProgress(Repository repo)
    {
        _gitDir = repo.GitDir;
        Name = "Rebase";

        _continueCmd = new Commands.Command
        {
            WorkingDirectory = repo.FullPath,
            Context = repo.FullPath,
            Editor = Commands.Command.EditorType.RebaseEditor,
            Args = "-c core.commentChar=\"^\" -c core.commentString=\"±\" rebase --continue",
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
            RaiseError = false,
        };

        // rebase-mergeまたはrebase-applyディレクトリから進捗情報を取得
        var rebaseMergeDir = Path.Combine(repo.GitDir, "rebase-merge");
        var rebaseApplyDir = Path.Combine(repo.GitDir, "rebase-apply");

        if (Directory.Exists(rebaseMergeDir))
        {
            var msgnumPath = Path.Combine(rebaseMergeDir, "msgnum");
            var endPath = Path.Combine(rebaseMergeDir, "end");

            // 読めない場合は既定値（0）のまま上書きしない（AmInProgress と共通の基底ヘルパー）
            if (TryReadIntFromFile(msgnumPath, out var msgnum))
                CurrentStep = msgnum;
            if (TryReadIntFromFile(endPath, out var end))
                TotalSteps = end;
        }
        else if (Directory.Exists(rebaseApplyDir))
        {
            var nextPath = Path.Combine(rebaseApplyDir, "next");
            var lastPath = Path.Combine(rebaseApplyDir, "last");

            if (TryReadIntFromFile(nextPath, out var next))
                CurrentStep = next;
            if (TryReadIntFromFile(lastPath, out var last))
                TotalSteps = last;
        }

        // Komorebi 独自修正: apply backend の rebase（git rebase --apply）は rebase-merge を作らず、
        // head-name・onto・orig-head を rebase-apply に置く。rebase-merge 固定で読むと例外になるため、
        // 実在する状態ディレクトリから読む。
        var stateDir = Directory.Exists(rebaseMergeDir) ? rebaseMergeDir : rebaseApplyDir;

        // head-nameファイルからブランチ名を取得し、refs/heads/等のプレフィックスを除去
        var headNamePath = Path.Combine(stateDir, "head-name");
        HeadName = File.Exists(headNamePath) ? File.ReadAllText(headNamePath).Trim() : string.Empty;
        if (HeadName.StartsWith("refs/heads/"))
            HeadName = HeadName["refs/heads/".Length..];
        else if (HeadName.StartsWith("refs/tags/"))
            HeadName = HeadName["refs/tags/".Length..];

        var stoppedSHAPath = Path.Combine(stateDir, "stopped-sha");
        var originalHeadPath = Path.Combine(stateDir, "orig-head");
        string stoppedSHA;
        if (File.Exists(stoppedSHAPath))
        {
            stoppedSHA = File.ReadAllText(stoppedSHAPath).Trim();
        }
        else if (stateDir == rebaseApplyDir)
        {
            // apply backend は stopped-sha を作らず、再適用中のコミットを original-commit に記録する。
            // orig-head はリベース前のブランチ先端で停止位置ではないため、ここでは使わない（実 Git で確認済み）。
            var originalCommitPath = Path.Combine(stateDir, "original-commit");
            stoppedSHA = File.Exists(originalCommitPath) ? File.ReadAllText(originalCommitPath).Trim() : string.Empty;
        }
        else if (File.Exists(originalHeadPath))
        {
            // Komorebi 独自修正: detached rebase の head-name は "detached HEAD" なので、
            // ブランチ ref の代わりに Git が記録した元の HEAD を使う。
            stoppedSHA = File.ReadAllText(originalHeadPath).Trim();
        }
        else
        {
            stoppedSHA = new Commands.QueryRevisionByRefName(repo.FullPath, HeadName).GetResult();
        }

        var ontoPath = Path.Combine(stateDir, "onto");
        var ontoSHA = File.Exists(ontoPath) ? File.ReadAllText(ontoPath).Trim() : string.Empty;

        if (!string.IsNullOrEmpty(stoppedSHA))
        {
            // StoppedAtとOntoの取得を並列実行する（コンストラクタ内のため同期待機）
            try
            {
                var stoppedTask = Task.Run(() => new Commands.QuerySingleCommit(repo.FullPath, stoppedSHA).GetResult());
                var ontoTask = Task.Run(() => new Commands.QuerySingleCommit(repo.FullPath, ontoSHA).GetResult());
                Task.WaitAll(stoppedTask, ontoTask);

                StoppedAt = stoppedTask.Result ?? new Models.Commit() { SHA = stoppedSHA };
                Onto = ontoTask.Result ?? new Models.Commit() { SHA = ontoSHA };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to query rebase info: {ex.Message}");
                StoppedAt = new Models.Commit() { SHA = stoppedSHA };
                Onto = new Models.Commit() { SHA = ontoSHA };
            }
        }
        else
        {
            Onto = new Commands.QuerySingleCommit(repo.FullPath, ontoSHA).GetResult() ?? new Models.Commit() { SHA = ontoSHA };
        }

        BaseName = Onto.GetFriendlyName();
    }

    /// <summary>
    /// リベース中断後に、rebase-merge/rebase-applyディレクトリの残骸と
    /// 対話的リベース用のジョブファイルが残っていれば削除する。
    /// </summary>
    protected override void OnAborted()
    {
        var rebaseMergeDir = Path.Combine(_gitDir, "rebase-merge");
        if (Directory.Exists(rebaseMergeDir))
            Directory.Delete(rebaseMergeDir, true);

        var rebaseApplyDir = Path.Combine(_gitDir, "rebase-apply");
        if (Directory.Exists(rebaseApplyDir))
            Directory.Delete(rebaseApplyDir, true);

        var jobFile = Path.Combine(_gitDir, "komorebi.interactive_rebase");
        if (File.Exists(jobFile))
            File.Delete(jobFile);
    }

    /// <summary>リポジトリの.gitディレクトリパス。</summary>
    private readonly string _gitDir;
}

/// <summary>
/// 進行中の git am（メール形式パッチの適用）操作のコンテキスト。
/// upstream ef017624: rebase-apply/applying がある場合に rebase と区別して扱う。
/// </summary>
public class AmInProgress : InProgressContext
{
    /// <summary>現在のパッチ番号（rebase-apply/next）。</summary>
    public int CurrentStep
    {
        get;
    }

    /// <summary>パッチの総数（rebase-apply/last）。</summary>
    public int TotalSteps
    {
        get;
    }

    /// <summary>
    /// 現在のパッチ以外にも残りがあり、スキップで次へ進めるかどうか。
    /// 上流と同じく最後のパッチでは Skip を出さない（中止または続行で終える）。
    /// </summary>
    public bool CanSkip => CurrentStep < TotalSteps;

    /// <summary>進捗テキスト。上流と同じく「適用済み件数/総数」で表示する。</summary>
    public override string ProgressText => TotalSteps > 0 ? $"({CurrentStep - 1}/{TotalSteps})" : string.Empty;
    public override bool HasProgress => TotalSteps > 0;

    /// <summary>
    /// コンストラクタ。rebase-apply の進捗を読み込み、continue/skip/abort コマンドを構成する。
    /// </summary>
    public AmInProgress(Repository repo)
    {
        Name = "Apply Patch";

        _continueCmd = new Commands.Command
        {
            WorkingDirectory = repo.FullPath,
            Context = repo.FullPath,
            Editor = Commands.Command.EditorType.None,
            Args = "am --continue",
        };

        _skipCmd = new Commands.Command
        {
            WorkingDirectory = repo.FullPath,
            Context = repo.FullPath,
            Args = "am --skip",
        };

        _abortCmd = new Commands.Command
        {
            WorkingDirectory = repo.FullPath,
            Context = repo.FullPath,
            Args = "am --abort",
        };

        var rebaseApplyDir = Path.Combine(repo.GitDir, "rebase-apply");
        CurrentStep = ReadIntFromFile(Path.Combine(rebaseApplyDir, "next"));
        TotalSteps = ReadIntFromFile(Path.Combine(rebaseApplyDir, "last"));
    }
}

/// <summary>
/// 進行中のrevert操作のコンテキスト。
/// REVERT_HEADファイルからリバート対象のコミット情報を取得する。
/// </summary>
public class RevertInProgress : InProgressContext
{
    /// <summary>リバート対象のコミット。</summary>
    public Models.Commit Head
    {
        get;
    }

    /// <summary>
    /// コンストラクタ。REVERT_HEADからコミット情報を読み込み、
    /// continue/skip/abortコマンドを構成する。
    /// </summary>
    public RevertInProgress(Repository repo)
    {
        Name = "Revert";

        _continueCmd = new Commands.Command
        {
            WorkingDirectory = repo.FullPath,
            Context = repo.FullPath,
            Editor = Commands.Command.EditorType.None,
            Args = "-c core.commentChar=\"^\" -c core.commentString=\"±\" revert --continue",
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
/// 進行中のmerge操作のコンテキスト。
/// MERGE_HEADファイルからマージ元のコミット情報を取得する。
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
    /// コンストラクタ。MERGE_HEADからマージ元コミット情報を読み込み、
    /// continue/abortコマンドを構成する（マージにはskipがない）。
    /// </summary>
    public MergeInProgress(Repository repo)
    {
        Name = "Merge";

        _continueCmd = new Commands.Command
        {
            WorkingDirectory = repo.FullPath,
            Context = repo.FullPath,
            Editor = Commands.Command.EditorType.None,
            Args = "-c core.commentChar=\"^\" -c core.commentString=\"±\" merge --continue",
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
