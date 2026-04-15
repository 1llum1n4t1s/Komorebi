using System.Text;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// Git Flowワークフローの各種操作を提供する静的クラス。
/// git flow の初期化、ブランチの開始・完了を実行する。
/// </summary>
public static class GitFlow
{
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
    public static async Task<bool> StartAsync(string repo, Models.GitFlowBranchType type, string name, Models.ICommandLog log)
    {
        var start = new Command();
        start.WorkingDirectory = repo;
        start.Context = repo;

        // ブランチ種別に応じたgit flowサブコマンドを設定する
        switch (type)
        {
            case Models.GitFlowBranchType.Feature:
                start.Args = $"flow feature start {name}";
                break;
            case Models.GitFlowBranchType.Release:
                start.Args = $"flow release start {name}";
                break;
            case Models.GitFlowBranchType.Hotfix:
                start.Args = $"flow hotfix start {name}";
                break;
            default:
                App.RaiseException(repo, App.Text("Error.BadGitFlowBranchType"));
                return false;
        }

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
    public static async Task<bool> FinishAsync(string repo, Models.GitFlowBranchType type, string name, bool squash, bool keepBranch, Models.ICommandLog log)
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

        // --squash: コミットを1つにまとめる
        if (squash)
            builder.Append("--squash ");

        // -k: ブランチを削除せず保持する
        if (keepBranch)
            builder.Append("-k ");

        // 完了するブランチ名を追加する
        builder.Append(name);

        var finish = new Command();
        finish.WorkingDirectory = repo;
        finish.Context = repo;
        finish.Args = builder.ToString();
        return await finish.Use(log).ExecAsync().ConfigureAwait(false);
    }
}
