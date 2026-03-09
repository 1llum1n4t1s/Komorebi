namespace Komorebi.Commands
{
    /// <summary>
    ///     リポジトリのガベージコレクションを実行するgitコマンド。
    ///     git gc --prune=now を実行し、不要なオブジェクトを即時削除する。
    /// </summary>
    public class GC : Command
    {
        /// <summary>
        ///     GCコマンドを初期化する。
        /// </summary>
        /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
        public GC(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;

            // git gc --prune=now: 不要オブジェクトを即時プルーニングしてリポジトリを最適化する
            Args = "gc --prune=now";
        }
    }
}
