using System.Text;

namespace Komorebi.Commands;

/// <summary>
/// 対話的リベースを実行するgitコマンド。
/// git rebase -i --autosquash を実行し、コミットの編集・並べ替え・統合を行う。
/// </summary>
public class InteractiveRebase : Command
{
    /// <summary>
    /// InteractiveRebaseコマンドを初期化する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    /// <param name="basedOn">リベースの基準となるコミットSHA。</param>
    /// <param name="autoStash">リベース前に変更を自動スタッシュするかどうか。</param>
    public InteractiveRebase(string repo, string basedOn, bool autoStash)
    {
        WorkingDirectory = repo;
        Context = repo;

        // リベースエディタを使用する（todoリストとメッセージ編集用）
        Editor = EditorType.RebaseEditor;

        var builder = new StringBuilder(512);

        // git rebase -i --autosquash: 対話的リベースを実行する
        // --autosquash: fixup!/squash!プレフィックスのコミットを自動的に並べ替える
        builder.Append("rebase -i --autosquash ");

        // --autostash: リベース前に未コミットの変更を自動スタッシュする
        if (autoStash)
            builder.Append("--autostash ");

        // リベースの基準コミットを指定する
        Args = builder.Append(basedOn).ToString();
    }
}
