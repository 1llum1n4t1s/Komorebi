using System;
using System.Text;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// Git Flowワークフローの各種操作を提供する静的クラス。
/// git flow の初期化、ブランチの開始・完了を実行する。
/// </summary>
public static class GitFlow
{
    public static async Task<bool> IsNextAsync(string repo)
    {
        // 上流との差分: 起動時のバックグラウンド検出との競合を避け、利用時に完了を待つ。
        return await new VersionQuery { WorkingDirectory = repo, Args = "flow version", RaiseError = false }
            .IsNextAsync().ConfigureAwait(false);
    }

    private sealed class VersionQuery : Command
    {
        public async Task<bool> IsNextAsync()
        {
            var result = await ReadToEndAsync().ConfigureAwait(false);
            return result.IsSuccess && result.StdOut.Contains("git-flow-next", StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Git Flowを初期化する。
    /// ブランチ名とプレフィックスをgit configに設定し、git flow init -d を実行する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    /// <param name="master">メインブランチ名（例: master, main）。</param>
    /// <param name="develop">開発ブランチ名（例: develop）。</param>
    /// <param name="feature">featureブランチのプレフィックス（例: feature/）。</param>
    /// <param name="release">releaseブランチのプレフィックス（例: release/）。</param>
    /// <param name="hotfix">hotfixブランチのプレフィックス（例: hotfix/）。</param>
    /// <param name="version">バージョンタグのプレフィックス。</param>
    /// <param name="log">コマンドログの出力先。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public static async Task<bool> InitAsync(string repo, string master, string develop, string feature, string release, string hotfix, string version, Models.ICommandLog log)
    {
        if (await IsNextAsync(repo).ConfigureAwait(false))
        {
            var next = new Command
            {
                WorkingDirectory = repo,
                Context = repo,
                Args = $"flow init --preset=classic --main={master.Quoted()} --develop={develop.Quoted()} --feature={feature.Quoted()} --bugfix=bugfix/ --release={release.Quoted()} --hotfix={hotfix.Quoted()} --support=support/",
            };
            if (!string.IsNullOrEmpty(version))
                next.Args += $" --tag={version.Quoted()}";
            return await next.Use(log).ExecAsync().ConfigureAwait(false);
        }

        // Git Flowのブランチ名とプレフィックスをgit configに設定する
        var config = new Config(repo);
        await config.SetAsync("gitflow.branch.master", master).ConfigureAwait(false);
        await config.SetAsync("gitflow.branch.develop", develop).ConfigureAwait(false);
        await config.SetAsync("gitflow.prefix.feature", feature).ConfigureAwait(false);
        await config.SetAsync("gitflow.prefix.bugfix", "bugfix/").ConfigureAwait(false);
        await config.SetAsync("gitflow.prefix.release", release).ConfigureAwait(false);
        await config.SetAsync("gitflow.prefix.hotfix", hotfix).ConfigureAwait(false);
        await config.SetAsync("gitflow.prefix.support", "support/").ConfigureAwait(false);
        await config.SetAsync("gitflow.prefix.versiontag", version, true).ConfigureAwait(false);

        // git flow init -d: デフォルト設定でGit Flowを初期化する
        var init = new Command();
        init.WorkingDirectory = repo;
        init.Context = repo;
        init.Args = "flow init -d";
        return await init.Use(log).ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Git Flowブランチを新規に開始する。
    /// git flow &lt;type&gt; start &lt;name&gt; を実行する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    /// <param name="type">ブランチの種類（Feature, Release, Hotfix）。</param>
    /// <param name="name">ブランチ名。</param>
    /// <param name="log">コマンドログの出力先。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    /// <param name="based">開始元ブランチ。未指定なら Git Flow の既定値。</param>
    public static async Task<bool> StartAsync(string repo, Models.GitFlowBranchType type, string name, Models.ICommandLog log, Models.Branch? based = null)
    {
        var start = new Command();
        start.WorkingDirectory = repo;
        start.Context = repo;

        // ブランチ種別に応じたgit flowサブコマンドを設定する
        switch (type)
        {
            case Models.GitFlowBranchType.Feature:
                start.Args = $"flow feature start {name.Quoted()}";
                break;
            case Models.GitFlowBranchType.Release:
                start.Args = $"flow release start {name.Quoted()}";
                break;
            case Models.GitFlowBranchType.Hotfix:
                start.Args = $"flow hotfix start {name.Quoted()}";
                break;
            default:
                App.RaiseException(repo, App.Text("Error.BadGitFlowBranchType"));
                return false;
        }

        if (based is not null)
            start.Args += $" {based.Name.Quoted()}";

        return await start.Use(log).ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Git Flowブランチを完了（マージ）する。
    /// git flow &lt;type&gt; finish [options] &lt;name&gt; を実行する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    /// <param name="type">ブランチの種類（Feature, Release, Hotfix）。</param>
    /// <param name="name">完了するブランチ名。</param>
    /// <param name="squash">コミットをスカッシュ（圧縮）するかどうか。</param>
    /// <param name="keepBranch">完了後もブランチを保持するかどうか。</param>
    /// <param name="log">コマンドログの出力先。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    /// <param name="rebase">マージ前にリベースするかどうか。</param>
    public static async Task<bool> FinishAsync(string repo, Models.GitFlowBranchType type, string name, bool squash, bool keepBranch, Models.ICommandLog log, bool rebase = false)
    {
        var builder = new StringBuilder();
        builder.Append("flow ");

        // ブランチ種別に応じたgit flowサブコマンドを設定する
        switch (type)
        {
            case Models.GitFlowBranchType.Feature:
                builder.Append("feature");
                break;
            case Models.GitFlowBranchType.Release:
                builder.Append("release");
                break;
            case Models.GitFlowBranchType.Hotfix:
                builder.Append("hotfix");
                break;
            default:
                App.RaiseException(repo, App.Text("Error.BadGitFlowBranchType"));
                return false;
        }

        builder.Append(" finish ");

        if (rebase)
            builder.Append("--rebase ");

        // --squash: コミットを1つにまとめる
        if (squash)
            builder.Append("--squash ");

        // --keep: 従来版と Next の両方でブランチを保持する
        if (keepBranch)
            builder.Append("--keep ");

        // 完了するブランチ名を追加する
        builder.Append(name.Quoted());

        var finish = new Command();
        finish.WorkingDirectory = repo;
        finish.Context = repo;
        finish.Args = builder.ToString();
        return await finish.Use(log).ExecAsync().ConfigureAwait(false);
    }
}
