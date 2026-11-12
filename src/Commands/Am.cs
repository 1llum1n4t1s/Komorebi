using System.Text;

namespace Komorebi.Commands;

/// <summary>
/// メール形式のパッチ（git format-patch の出力）をコミットとして適用する git am コマンド。
/// upstream ef017624: パッチの作者・日付・メッセージを保ったままコミットを作成する。
/// </summary>
public class Am : Command
{
    /// <summary>
    /// Am コマンドを初期化する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    /// <param name="file">適用するパッチファイルのパス。</param>
    /// <param name="ignoreWhitespace">空白の違いを無視するかどうか。</param>
    /// <param name="whitespaceMode">空白処理モード（fix, warn, error など）。git apply へ渡される。</param>
    /// <param name="extra">追加のオプション文字列（--3way など）。</param>
    public Am(string repo, string file, bool ignoreWhitespace, string whitespaceMode, string extra)
    {
        WorkingDirectory = repo;
        Context = repo;

        var builder = new StringBuilder(1024);

        // git am: パッチをコミットとして適用する
        builder.Append("am ");

        // 空白の処理方法は git apply と同じオプションで指定する
        if (ignoreWhitespace)
            builder.Append("--ignore-whitespace ");
        else
            builder.Append("--whitespace=").Append(whitespaceMode).Append(' ');

        // 追加オプションがあれば付加する
        if (!string.IsNullOrEmpty(extra))
            builder.Append(extra).Append(' ');

        // パッチファイルのパスは引数境界を守るため Quoted で渡す
        Args = builder.Append(file.Quoted()).ToString();
    }
}
