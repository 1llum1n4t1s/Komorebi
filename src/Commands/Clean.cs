namespace Komorebi.Commands
{
    /// <summary>
    ///     作業ツリーから追跡されていないファイルを削除するgitコマンド。
    ///     git clean を実行し、クリーンモードに応じて対象ファイルを変える。
    /// </summary>
    public class Clean : Command
    {
        /// <summary>
        ///     Cleanコマンドを初期化する。
        /// </summary>
        /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
        /// <param name="mode">クリーンモード（未追跡のみ、無視ファイルのみ、全て）。</param>
        public Clean(string repo, Models.CleanMode mode)
        {
            WorkingDirectory = repo;
            Context = repo;

            // git clean オプション:
            //   -q: 静かモード（削除ファイル名を表示しない）
            //   -f: 強制実行
            //   -d: 追跡されていないディレクトリも削除
            //   -X: .gitignoreで無視されたファイルのみ削除
            //   -x: 無視ファイルを含む全ての追跡されていないファイルを削除
            Args = mode switch
            {
                Models.CleanMode.OnlyUntrackedFiles => "clean -qfd",
                Models.CleanMode.OnlyIgnoredFiles => "clean -qfdX",
                _ => "clean -qfdx",
            };
        }
    }
}
