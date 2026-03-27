using System.Text;

namespace Komorebi.Commands;

/// <summary>
///     指定コミットの変更を取り消す新しいコミットを作成するgitコマンド。
///     git revert を実行する。
/// </summary>
public class Revert : Command
{
    /// <summary>
    ///     Revertコマンドを初期化する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    /// <param name="commit">取り消し対象のコミットSHA。</param>
    /// <param name="autoCommit">trueの場合自動コミット、falseの場合作業ツリーへの変更のみ。</param>
    public Revert(string repo, string commit, bool autoCommit)
    {
        WorkingDirectory = repo;
        Context = repo;

        var builder = new StringBuilder(512);

        // git revert -m 1: マージコミットの場合、最初の親を使用して取り消す
        // --no-edit: コミットメッセージの編集をスキップする
        builder
            .Append("revert -m 1 ")
            .Append(commit)
            .Append(" --no-edit");

        // --no-commit: コミットせず作業ツリーへの変更のみ行う
        if (!autoCommit)
            builder.Append(" --no-commit");

        Args = builder.ToString();
    }
}
