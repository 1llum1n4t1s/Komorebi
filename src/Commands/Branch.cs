using System.Text;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// ブランチの作成、リネーム、上流設定、削除を行うgitコマンド群。
/// git branch の各種サブコマンドを実行する。
/// </summary>
public class Branch : Command
{
    /// <summary>
    /// Branchコマンドを初期化する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    /// <param name="name">操作対象のブランチ名。</param>
    public Branch(string repo, string name)
    {
        WorkingDirectory = repo;
        Context = repo;
        _name = name;
    }

    /// <summary>
    /// 新しいブランチを作成する。
    /// git branch [-f] &lt;name&gt; &lt;basedOn&gt; を実行する。
    /// </summary>
    /// <param name="basedOn">ブランチの基点となるリビジョン。</param>
    /// <param name="force">同名ブランチが存在する場合に強制的に再作成するかどうか。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> CreateAsync(string basedOn, bool force)
    {
        var builder = new StringBuilder();
        builder.Append("branch ");

        // -f: 同名ブランチが存在する場合でも強制的に作成する
        if (force)
            builder.Append("-f ");

        // ブランチ名と基点リビジョンを指定する（Quoted() で引数境界を守る）
        builder.Append(_name.Quoted());
        builder.Append(" ");
        builder.Append(basedOn.Quoted());

        Args = builder.ToString();
        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// ブランチ名を変更する。
    /// git branch -M &lt;oldName&gt; &lt;newName&gt; を実行する。
    /// </summary>
    /// <param name="to">新しいブランチ名。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> RenameAsync(string to)
    {
        // git branch -M: ブランチ名を強制的にリネームする（同名が存在しても上書き）
        Args = $"branch -M {_name.Quoted()} {to.Quoted()}";
        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// ブランチの上流（トラッキング）ブランチを設定または解除する。
    /// git branch &lt;name&gt; -u &lt;upstream&gt; または --unset-upstream を実行する。
    /// </summary>
    /// <param name="tracking">トラッキング先のリモートブランチ。nullの場合は上流を解除する。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> SetUpstreamAsync(Models.Branch tracking)
    {
        if (tracking is null)
            // 上流ブランチの設定を解除する
            Args = $"branch {_name.Quoted()} --unset-upstream";
        else
            // 上流ブランチを指定されたリモートブランチに設定する
            Args = $"branch {_name.Quoted()} -u {tracking.FriendlyName.Quoted()}";

        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// ローカルブランチを強制削除する。
    /// git branch -D &lt;name&gt; を実行する。
    /// </summary>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> DeleteLocalAsync()
    {
        // git branch -D: マージ状態に関わらずブランチを強制削除する
        Args = $"branch -D {_name.Quoted()}";
        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// リモートトラッキングブランチのローカル参照を削除する。
    /// git branch -D -r &lt;remote&gt;/&lt;name&gt; を実行する。
    /// </summary>
    /// <param name="remote">リモート名。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> DeleteRemoteAsync(string remote)
    {
        // git branch -D -r: リモートトラッキングブランチの参照を強制削除する。
        // remote/name の形を 1 個の引数として Quoted する（途中スラッシュは git refspec として有効）
        Args = $"branch -D -r {$"{remote}/{_name}".Quoted()}";
        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 操作対象のブランチ名。
    /// </summary>
    private readonly string _name;
}
