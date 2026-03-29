using System.Text;

namespace Komorebi.Commands;

/// <summary>
/// 指定コミットの変更を現在のブランチに適用するgitコマンド。
/// git cherry-pick を実行する。
/// </summary>
public class CherryPick : Command
{
    /// <summary>
    /// CherryPickコマンドを初期化する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    /// <param name="commits">チェリーピック対象のコミットSHA（スペース区切りで複数指定可）。</param>
    /// <param name="noCommit">trueの場合、変更を適用するがコミットはしない。</param>
    /// <param name="appendSourceToMessage">trueの場合、元のコミットSHAをメッセージに追記する。</param>
    /// <param name="extraParams">追加のパラメータ文字列。</param>
    public CherryPick(string repo, string commits, bool noCommit, bool appendSourceToMessage, string extraParams)
    {
        WorkingDirectory = repo;
        Context = repo;

        var builder = new StringBuilder(1024);

        // git cherry-pick: 指定コミットの変更を現在のブランチに取り込む
        builder.Append("cherry-pick ");

        // -n: コミットせずに変更のみ適用する（ステージングまで）
        if (noCommit)
            builder.Append("-n ");

        // -x: コミットメッセージに元のSHAを「(cherry picked from commit ...)」として追記する
        if (appendSourceToMessage)
            builder.Append("-x ");

        // 追加パラメータがあれば付加する
        if (!string.IsNullOrEmpty(extraParams))
            builder.Append(extraParams).Append(' ');

        // チェリーピック対象のコミットSHAを追加する
        Args = builder.Append(commits).ToString();
    }
}
