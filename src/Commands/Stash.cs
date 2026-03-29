using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// スタッシュ（一時退避）操作を提供するgitコマンド。
/// git stash のサブコマンドで変更の退避・復元・削除を行う。
/// </summary>
public class Stash : Command
{
    /// <summary>
    /// Stashコマンドを初期化する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    public Stash(string repo)
    {
        WorkingDirectory = repo;
        Context = repo;
    }

    /// <summary>
    /// 全ての変更をスタッシュにプッシュする。
    /// git stash push を実行する。
    /// </summary>
    /// <param name="message">スタッシュのメッセージ。</param>
    /// <param name="includeUntracked">未追跡ファイルも含めるかどうか。</param>
    /// <param name="keepIndex">インデックスの内容を保持するかどうか。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> PushAsync(string message, bool includeUntracked = true, bool keepIndex = false)
    {
        var builder = new StringBuilder();

        // git stash push: 変更をスタッシュに退避する
        builder.Append("stash push ");

        // --include-untracked: 未追跡ファイルも退避する
        if (includeUntracked)
            builder.Append("--include-untracked ");

        // --keep-index: ステージ済みの内容をワーキングツリーに残す
        if (keepIndex)
            builder.Append("--keep-index ");

        // -m: スタッシュにメッセージを付ける
        if (!string.IsNullOrEmpty(message))
            builder.Append("-m ").Append(message.Quoted());

        Args = builder.ToString();
        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 指定した変更ファイルのみをスタッシュにプッシュする。
    /// git stash push -- &lt;paths&gt; を実行する。
    /// </summary>
    /// <param name="message">スタッシュのメッセージ。</param>
    /// <param name="changes">退避する変更ファイルのリスト。</param>
    /// <param name="keepIndex">インデックスの内容を保持するかどうか。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> PushAsync(string message, List<Models.Change> changes, bool keepIndex)
    {
        var builder = new StringBuilder();

        // git stash push --include-untracked: 未追跡ファイルも含めて退避する
        builder.Append("stash push --include-untracked ");

        // --keep-index: ステージ済みの内容をワーキングツリーに残す
        if (keepIndex)
            builder.Append("--keep-index ");

        // -m: スタッシュにメッセージを付ける
        if (!string.IsNullOrEmpty(message))
            builder.Append("-m ").Append(message.Quoted()).Append(' ');

        // -- <paths>: 退避する個別ファイルパスを指定する
        builder.Append("-- ");
        foreach (var c in changes)
            builder.Append(c.Path.Quoted()).Append(' ');

        Args = builder.ToString();
        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// pathspec-from-fileを使用して変更をスタッシュにプッシュする。
    /// git stash push --pathspec-from-file を実行する。
    /// </summary>
    /// <param name="message">スタッシュのメッセージ。</param>
    /// <param name="pathspecFromFile">対象ファイルのパスが列挙されたファイルのパス。</param>
    /// <param name="keepIndex">インデックスの内容を保持するかどうか。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> PushAsync(string message, string pathspecFromFile, bool keepIndex)
    {
        var builder = new StringBuilder();

        // git stash push --pathspec-from-file: ファイルからパスリストを読んで退避する
        builder.Append("stash push --include-untracked --pathspec-from-file=").Append(pathspecFromFile.Quoted()).Append(" ");

        // --keep-index: ステージ済みの内容をワーキングツリーに残す
        if (keepIndex)
            builder.Append("--keep-index ");

        // -m: スタッシュにメッセージを付ける
        if (!string.IsNullOrEmpty(message))
            builder.Append("-m ").Append(message.Quoted());

        Args = builder.ToString();
        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// ステージ済みの変更のみをスタッシュにプッシュする。
    /// git stash push --staged を実行する。
    /// </summary>
    /// <param name="message">スタッシュのメッセージ。</param>
    /// <param name="keepIndex">インデックスの内容を保持するかどうか。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> PushOnlyStagedAsync(string message, bool keepIndex)
    {
        var builder = new StringBuilder();

        // git stash push --staged: ステージ済みの変更のみを退避する
        builder.Append("stash push --staged ");

        // --keep-index: ステージ済みの内容をワーキングツリーに残す
        if (keepIndex)
            builder.Append("--keep-index ");

        // -m: スタッシュにメッセージを付ける
        if (!string.IsNullOrEmpty(message))
            builder.Append("-m ").Append(message.Quoted());
        Args = builder.ToString();
        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// スタッシュを適用する（スタッシュはリストに残る）。
    /// git stash apply を実行する。
    /// </summary>
    /// <param name="name">適用するスタッシュ名（例: stash@{0}）。</param>
    /// <param name="restoreIndex">インデックスの状態も復元するかどうか。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> ApplyAsync(string name, bool restoreIndex)
    {
        // --index: スタッシュ時のインデックス状態も復元する
        var opts = restoreIndex ? "--index" : string.Empty;

        // git stash apply -q: 静かにスタッシュを適用する
        Args = $"stash apply -q {opts} {name.Quoted()}";
        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// スタッシュをポップする（適用してリストから削除）。
    /// git stash pop を実行する。
    /// </summary>
    /// <param name="name">ポップするスタッシュ名。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> PopAsync(string name)
    {
        // git stash pop -q --index: インデックス復元付きでスタッシュを適用して削除する
        Args = $"stash pop -q --index {name.Quoted()}";
        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// スタッシュを個別に削除する。
    /// git stash drop を実行する。
    /// </summary>
    /// <param name="name">削除するスタッシュ名。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> DropAsync(string name)
    {
        // git stash drop -q: 指定スタッシュを静かに削除する
        Args = $"stash drop -q {name.Quoted()}";
        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 全てのスタッシュを削除する。
    /// git stash clear を実行する。
    /// </summary>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> ClearAsync()
    {
        // git stash clear: スタッシュリストを全てクリアする
        Args = "stash clear";
        return await ExecAsync().ConfigureAwait(false);
    }
}
