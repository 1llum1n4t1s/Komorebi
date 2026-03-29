using System.Text;

namespace Komorebi.Commands;

/// <summary>
/// 現在のブランチを指定ブランチ上にリベースするgitコマンド。
/// git rebase を実行する。
/// </summary>
public class Rebase : Command
{
    /// <summary>
    /// Rebaseコマンドを初期化する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    /// <param name="basedOn">リベースの基準となるブランチ名またはコミットSHA。</param>
    /// <param name="autoStash">リベース前に変更を自動的にスタッシュするかどうか。</param>
    public Rebase(string repo, string basedOn, bool autoStash)
    {
        WorkingDirectory = repo;
        Context = repo;

        var builder = new StringBuilder(512);

        // git rebase: 現在のブランチのコミットを指定ブランチの先端に再適用する
        builder.Append("rebase ");

        // --autostash: リベース前に未コミットの変更を自動スタッシュし、完了後に復元する
        if (autoStash)
            builder.Append("--autostash ");

        // リベースの基準ブランチを指定する
        Args = builder.Append(basedOn).ToString();
    }
}
