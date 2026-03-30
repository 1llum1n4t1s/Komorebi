using System.Collections.Generic;
using System.Text;

namespace Komorebi.Commands;

/// <summary>
/// ファイルをインデックスと作業ツリーから削除するgitコマンド。
/// git rm -f を実行する。
/// </summary>
public class Remove : Command
{
    /// <summary>
    /// Removeコマンドを初期化する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    /// <param name="files">削除対象のファイルパスのリスト。</param>
    public Remove(string repo, List<string> files)
    {
        WorkingDirectory = repo;
        Context = repo;

        var builder = new StringBuilder();

        // git rm -f --: ファイルを強制的にインデックスと作業ツリーから削除する
        // --: 以降の引数をすべてファイルパスとして扱う
        builder.Append("rm -f --");

        // 削除対象の各ファイルパスを引数に追加する
        foreach (var file in files)
            builder.Append(' ').Append(file.Quoted());

        Args = builder.ToString();
    }
}
