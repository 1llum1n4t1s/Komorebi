namespace Komorebi.Commands
{
    /// <summary>
    ///     二分探索でバグの原因コミットを特定するgitコマンド。
    ///     git bisect のサブコマンド（start, good, bad, resetなど）を実行する。
    /// </summary>
    public class Bisect : Command
    {
        /// <summary>
        ///     Bisectコマンドを初期化する。
        /// </summary>
        /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
        /// <param name="subcmd">bisectのサブコマンド（start, good, bad, reset等）。</param>
        public Bisect(string repo, string subcmd)
        {
            WorkingDirectory = repo;
            Context = repo;

            // bisectはユーザー操作の一部として実行されるため、エラーを例外として上げない
            RaiseError = false;

            // git bisect <subcmd>: 二分探索のサブコマンドを実行する
            Args = $"bisect {subcmd}";
        }
    }
}
