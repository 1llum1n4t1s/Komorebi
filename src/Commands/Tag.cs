using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Komorebi.Commands
{
    /// <summary>
    ///     タグの作成・削除を行うgitコマンド。
    ///     git tag を実行する。
    /// </summary>
    public class Tag : Command
    {
        /// <summary>
        ///     Tagコマンドを初期化する。
        /// </summary>
        /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
        /// <param name="name">タグ名。</param>
        public Tag(string repo, string name)
        {
            WorkingDirectory = repo;
            Context = repo;
            _name = name;
        }

        /// <summary>
        ///     軽量タグを作成する。
        ///     git tag --no-sign を実行する。
        /// </summary>
        /// <param name="basedOn">タグを付けるコミットSHA。</param>
        /// <returns>コマンドが成功した場合はtrue。</returns>
        public async Task<bool> AddAsync(string basedOn)
        {
            // git tag --no-sign: 署名なしの軽量タグを作成する
            Args = $"tag --no-sign {_name} {basedOn}";
            return await ExecAsync().ConfigureAwait(false);
        }

        /// <summary>
        ///     注釈付きタグを作成する。
        ///     git tag -a を実行する。メッセージは一時ファイル経由で渡す。
        /// </summary>
        /// <param name="basedOn">タグを付けるコミットSHA。</param>
        /// <param name="message">タグメッセージ。</param>
        /// <param name="sign">GPG署名を付けるかどうか。</param>
        /// <returns>コマンドが成功した場合はtrue。</returns>
        public async Task<bool> AddAsync(string basedOn, string message, bool sign)
        {
            var builder = new StringBuilder();

            // git tag --sign/-a / --no-sign -a: 署名有無を指定して注釈付きタグを作成する
            builder
                .Append("tag ")
                .Append(sign ? "--sign -a " : "--no-sign -a ")
                .Append(_name)
                .Append(' ')
                .Append(basedOn);

            if (!string.IsNullOrEmpty(message))
            {
                // メッセージを一時ファイルに書き出し、-F オプションで指定する
                string tmp = Path.GetTempFileName();
                await File.WriteAllTextAsync(tmp, message);
                builder.Append(" -F ").Append(tmp.Quoted());

                Args = builder.ToString();
                var succ = await ExecAsync().ConfigureAwait(false);

                // 一時ファイルを削除する
                File.Delete(tmp);
                return succ;
            }

            // メッセージが空の場合はタグ名をメッセージとして使用する
            builder.Append(" -m ");
            builder.Append(_name);

            Args = builder.ToString();
            return await ExecAsync().ConfigureAwait(false);
        }

        /// <summary>
        ///     タグを削除する。
        ///     git tag --delete を実行する。
        /// </summary>
        /// <returns>コマンドが成功した場合はtrue。</returns>
        public async Task<bool> DeleteAsync()
        {
            // git tag --delete: 指定タグを削除する
            Args = $"tag --delete {_name}";
            return await ExecAsync().ConfigureAwait(false);
        }

        /// <summary>
        ///     操作対象のタグ名。
        /// </summary>
        private readonly string _name;
    }
}
