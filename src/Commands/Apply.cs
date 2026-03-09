using System.Text;

namespace Komorebi.Commands
{
    /// <summary>
    ///     パッチファイルを適用するgitコマンド。
    ///     git apply を実行し、空白処理やオプションを指定できる。
    /// </summary>
    public class Apply : Command
    {
        /// <summary>
        ///     Applyコマンドを初期化する。
        /// </summary>
        /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
        /// <param name="file">適用するパッチファイルのパス。</param>
        /// <param name="ignoreWhitespace">空白の違いを無視するかどうか。</param>
        /// <param name="whitespaceMode">空白処理モード（fix, warn, error など）。</param>
        /// <param name="extra">追加のオプション文字列。</param>
        public Apply(string repo, string file, bool ignoreWhitespace, string whitespaceMode, string extra)
        {
            WorkingDirectory = repo;
            Context = repo;

            var builder = new StringBuilder(1024);

            // git apply: パッチファイルを作業ツリーに適用する
            builder.Append("apply ");

            // 空白の処理方法を指定する
            if (ignoreWhitespace)
                builder.Append("--ignore-whitespace ");
            else
                builder.Append("--whitespace=").Append(whitespaceMode).Append(' ');

            // 追加オプションがあれば付加する
            if (!string.IsNullOrEmpty(extra))
                builder.Append(extra).Append(' ');

            // パッチファイルのパスを引数に追加する
            Args = builder.Append(file.Quoted()).ToString();
        }
    }
}
