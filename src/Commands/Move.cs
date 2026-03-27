using System.Text;

namespace Komorebi.Commands;

/// <summary>
///     ファイルやディレクトリを移動（リネーム）するgitコマンド。
///     git mv を実行する。
/// </summary>
public class Move : Command
{
    /// <summary>
    ///     Moveコマンドを初期化する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    /// <param name="oldPath">移動元のファイルパス。</param>
    /// <param name="newPath">移動先のファイルパス。</param>
    /// <param name="force">上書きを強制するかどうか。</param>
    public Move(string repo, string oldPath, string newPath, bool force)
    {
        WorkingDirectory = repo;
        Context = repo;

        var builder = new StringBuilder();

        // git mv -v: ファイルを移動し、結果を表示する
        builder.Append("mv -v ");

        // -f: 移動先に既存ファイルがあっても強制上書きする
        if (force)
            builder.Append("-f ");

        // 移動元と移動先のパスを指定する
        builder.Append(oldPath.Quoted());
        builder.Append(' ');
        builder.Append(newPath.Quoted());

        Args = builder.ToString();
    }
}
