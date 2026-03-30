using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// ブランチの切り替え、コミットへのデタッチ、コンフリクト解決、ファイル復元を行うgitコマンド群。
/// git checkout の各種オプションを実行する。
/// </summary>
public class Checkout : Command
{
    /// <summary>
    /// Checkoutコマンドを初期化する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    public Checkout(string repo)
    {
        WorkingDirectory = repo;
        Context = repo;
    }

    /// <summary>
    /// 既存のブランチに切り替える。
    /// git checkout [--force] &lt;branch&gt; を実行する。
    /// </summary>
    /// <param name="branch">切り替え先のブランチ名。</param>
    /// <param name="force">未コミットの変更を破棄して強制切り替えするかどうか。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> BranchAsync(string branch, bool force)
    {
        var builder = new StringBuilder();

        // git checkout --progress: 進捗表示付きでブランチを切り替える
        builder.Append("checkout --progress ");

        // --force: 未コミットの変更を破棄して強制的に切り替える
        if (force)
            builder.Append("--force ");

        builder.Append(branch);

        Args = builder.ToString();
        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 新しいブランチを作成して切り替える。
    /// git checkout [-b|-B] &lt;branch&gt; &lt;basedOn&gt; を実行する。
    /// </summary>
    /// <param name="branch">作成するブランチ名。</param>
    /// <param name="basedOn">ブランチの基点となるリビジョン。</param>
    /// <param name="force">未コミットの変更を破棄して強制切り替えするかどうか。</param>
    /// <param name="allowOverwrite">同名ブランチが存在する場合に上書きを許可するかどうか。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> BranchAsync(string branch, string basedOn, bool force, bool allowOverwrite)
    {
        var builder = new StringBuilder();

        // git checkout --progress: 進捗表示付きでブランチを切り替える
        builder.Append("checkout --progress ");
        if (force)
            builder.Append("--force ");

        // -B: 同名ブランチの上書きを許可、-b: 新規作成のみ
        builder.Append(allowOverwrite ? "-B " : "-b ");

        // 新しいブランチ名と基点リビジョンを指定する
        builder.Append(branch);
        builder.Append(" ");
        builder.Append(basedOn);

        Args = builder.ToString();
        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 特定のコミットにデタッチドHEADとして切り替える。
    /// git checkout --detach &lt;commitId&gt; を実行する。
    /// </summary>
    /// <param name="commitId">切り替え先のコミットSHA。</param>
    /// <param name="force">未コミットの変更を破棄して強制切り替えするかどうか。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> CommitAsync(string commitId, bool force)
    {
        var option = force ? "--force" : string.Empty;

        // git checkout --detach: HEADをブランチから切り離して特定コミットに移動する
        Args = $"checkout {option} --detach --progress {commitId}";
        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// コンフリクト時に相手側（theirs）の変更を採用する。
    /// git checkout --theirs -- &lt;files&gt; を実行する。
    /// </summary>
    /// <param name="files">対象ファイルのリスト。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> UseTheirsAsync(List<string> files)
    {
        var builder = new StringBuilder();

        // git checkout --theirs: マージ時に相手側の変更を採用する
        builder.Append("checkout --theirs --");
        foreach (var f in files)
            builder.Append(' ').Append(f.Quoted());
        Args = builder.ToString();
        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// コンフリクト時に自分側（ours）の変更を採用する。
    /// git checkout --ours -- &lt;files&gt; を実行する。
    /// </summary>
    /// <param name="files">対象ファイルのリスト。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> UseMineAsync(List<string> files)
    {
        var builder = new StringBuilder();

        // git checkout --ours: マージ時に自分側の変更を採用する
        builder.Append("checkout --ours --");
        foreach (var f in files)
            builder.Append(' ').Append(f.Quoted());

        Args = builder.ToString();
        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 特定リビジョンのファイルを作業ツリーに復元する。
    /// git checkout --no-overlay &lt;revision&gt; -- &lt;file&gt; を実行する。
    /// </summary>
    /// <param name="file">復元するファイルのパス。</param>
    /// <param name="revision">復元元のリビジョン。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> FileWithRevisionAsync(string file, string revision)
    {
        // git checkout --no-overlay: リビジョンに存在しないファイルを削除する
        Args = $"checkout --no-overlay {revision} -- {file.Quoted()}";
        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 特定リビジョンの複数ファイルを作業ツリーに復元する。
    /// git checkout --no-overlay &lt;revision&gt; -- &lt;files...&gt; を実行する。
    /// </summary>
    /// <param name="files">復元するファイルのリスト。</param>
    /// <param name="revision">復元元のリビジョン。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> MultipleFilesWithRevisionAsync(List<string> files, string revision)
    {
        var builder = new StringBuilder();

        // git checkout --no-overlay: リビジョンに存在しないファイルを削除する
        builder
            .Append("checkout --no-overlay ")
            .Append(revision)
            .Append(" --");

        // 復元対象の各ファイルパスを引数に追加する
        foreach (var f in files)
            builder.Append(' ').Append(f.Quoted());

        Args = builder.ToString();
        return await ExecAsync().ConfigureAwait(false);
    }
}
