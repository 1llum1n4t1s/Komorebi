using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Komorebi.Commands
{
    /// <summary>
    ///     git ls-files -v を実行して、assume-unchanged（変更なしと仮定）フラグが設定されたファイルの一覧を取得するクラス。
    /// </summary>
    public partial class QueryAssumeUnchangedFiles : Command
    {
        /// <summary>
        ///     ls-files -v の出力行を解析する正規表現。フラグ文字とファイルパスを抽出する。
        /// </summary>
        [GeneratedRegex(@"^(\w)\s+(.+)$")]
        private static partial Regex REG_PARSE();

        /// <summary>
        ///     コンストラクタ。ls-files -v コマンドを設定する。
        /// </summary>
        /// <param name="repo">リポジトリのパス</param>
        public QueryAssumeUnchangedFiles(string repo)
        {
            WorkingDirectory = repo;
            Args = "ls-files -v";
            RaiseError = false;
        }

        /// <summary>
        ///     コマンドを非同期で実行し、assume-unchangedファイルの一覧を返す。
        /// </summary>
        /// <returns>assume-unchangedフラグが設定されたファイルパスのリスト</returns>
        public async Task<List<string>> GetResultAsync()
        {
            var outs = new List<string>();
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var match = REG_PARSE().Match(line);
                if (!match.Success)
                    continue;

                // 'h' フラグはassume-unchangedを示す（小文字のhが変更なしと仮定されたファイル）
                if (match.Groups[1].Value == "h")
                    outs.Add(match.Groups[2].Value);
            }

            return outs;
        }
    }
}
