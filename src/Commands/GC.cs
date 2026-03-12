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
        /// <param name="aggressive">trueの場合、--aggressiveオプションでデルタ圧縮を最適化する。</param>
        public GC(string repo, bool aggressive = false)
        {
            WorkingDirectory = repo;
            Context = repo;

            // git gc --prune=now: 不要オブジェクトを即時プルーニングしてリポジトリを最適化する
            // --aggressive: デルタ圧縮をゼロからやり直し、より強力に最適化する（時間がかかる）
            Args = aggressive ? "gc --aggressive --prune=now" : "gc --prune=now";
        }
    }
}
