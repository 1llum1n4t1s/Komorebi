using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
///     サブモジュールの管理操作を提供するgitコマンド。
///     git submodule のサブコマンドでサブモジュールの追加・更新・削除を行う。
/// </summary>
public class Submodule : Command
{
    /// <summary>
    ///     Submoduleコマンドを初期化する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    public Submodule(string repo)
    {
        WorkingDirectory = repo;
        Context = repo;
    }

    /// <summary>
    ///     サブモジュールを追加する。
    ///     git submodule add を実行し、必要に応じて再帰的に初期化する。
    /// </summary>
    /// <param name="url">サブモジュールのURL。</param>
    /// <param name="relativePath">サブモジュールの配置先相対パス。</param>
    /// <param name="recursive">再帰的にサブモジュールを初期化するかどうか。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> AddAsync(string url, string relativePath, bool recursive)
    {
        // git -c protocol.file.allow=always submodule add: ファイルプロトコルを許可してサブモジュールを追加する
        Args = $"-c protocol.file.allow=always submodule add {url.Quoted()} {relativePath.Quoted()}";

        var succ = await ExecAsync().ConfigureAwait(false);
        if (!succ)
            return false;

        // git submodule update --init: 追加したサブモジュールを初期化する
        // --recursive: ネストされたサブモジュールも再帰的に初期化する
        if (recursive)
            Args = $"submodule update --init --recursive -- {relativePath.Quoted()}";
        else
            Args = $"submodule update --init -- {relativePath.Quoted()}";
        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     サブモジュールのURLを変更する。
    ///     git submodule set-url を実行する。
    /// </summary>
    /// <param name="path">サブモジュールのパス。</param>
    /// <param name="url">新しいURL。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> SetURLAsync(string path, string url)
    {
        // git submodule set-url: サブモジュールのリモートURLを変更する
        Args = $"submodule set-url -- {path.Quoted()} {url.Quoted()}";
        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     サブモジュールの追跡ブランチを設定する。
    ///     git submodule set-branch を実行する。
    /// </summary>
    /// <param name="path">サブモジュールのパス。</param>
    /// <param name="branch">追跡するブランチ名。空の場合はデフォルトに戻す。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> SetBranchAsync(string path, string branch)
    {
        if (string.IsNullOrEmpty(branch))
            // git submodule set-branch -d: ブランチ設定をデフォルトに戻す
            Args = $"submodule set-branch -d -- {path.Quoted()}";
        else
            // git submodule set-branch -b: 追跡ブランチを指定する
            Args = $"submodule set-branch -b {branch.Quoted()} -- {path.Quoted()}";

        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     サブモジュールを更新する。
    ///     git submodule update --recursive を実行する。
    /// </summary>
    /// <param name="modules">更新対象のサブモジュールパスのリスト。空の場合は全て更新。</param>
    /// <param name="init">未初期化のサブモジュールも初期化するかどうか。</param>
    /// <param name="useRemote">リモートの最新コミットを使用するかどうか。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> UpdateAsync(List<string> modules, bool init = false, bool useRemote = false)
    {
        var builder = new StringBuilder();

        // git submodule update --recursive: サブモジュールを再帰的に更新する
        builder.Append("submodule update --recursive");

        // --init: 未初期化のサブモジュールも初期化する
        if (init)
            builder.Append(" --init");

        // --remote: リモートの最新コミットにチェックアウトする
        if (useRemote)
            builder.Append(" --remote");

        // 個別のサブモジュールパスを指定する
        if (modules.Count > 0)
        {
            builder.Append(" --");
            foreach (var module in modules)
                builder.Append(' ').Append(module.Quoted());
        }

        Args = builder.ToString();
        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     サブモジュールの登録を解除する。
    ///     git submodule deinit を実行する。
    /// </summary>
    /// <param name="module">登録解除するサブモジュールのパス。</param>
    /// <param name="force">強制的に登録解除するかどうか。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> DeinitAsync(string module, bool force)
    {
        // git submodule deinit [-f]: サブモジュールの登録を解除する
        Args = force ? $"submodule deinit -f -- {module.Quoted()}" : $"submodule deinit -- {module.Quoted()}";
        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     サブモジュールを完全に削除する。
    ///     git rm -rf を実行してサブモジュールのファイルを削除する。
    /// </summary>
    /// <param name="module">削除するサブモジュールのパス。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> DeleteAsync(string module)
    {
        // git rm -rf: サブモジュールのディレクトリとファイルを再帰的に強制削除する
        Args = $"rm -rf {module.Quoted()}";
        return await ExecAsync().ConfigureAwait(false);
    }
}
