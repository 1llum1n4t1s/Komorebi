namespace Komorebi.Commands
{
    /// <summary>
    ///     リポジトリの特定リビジョンをZIPアーカイブとして保存するgitコマンド。
    ///     git archive --format=zip を実行する。
    /// </summary>
    public class Archive : Command
    {
        /// <summary>
        ///     Archiveコマンドを初期化する。
        /// </summary>
        /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
        /// <param name="revision">アーカイブ対象のリビジョン（ブランチ名、タグ名、SHA等）。</param>
        /// <param name="saveTo">ZIPファイルの保存先パス。</param>
        public Archive(string repo, string revision, string saveTo)
        {
            WorkingDirectory = repo;
            Context = repo;

            // git archive: 指定リビジョンのファイルをZIP形式でアーカイブする
            // --format=zip: 出力形式をZIPに指定
            // --verbose: 処理中のファイル名を出力
            // --output: 保存先ファイルパスを指定
            Args = $"archive --format=zip --verbose --output={saveTo.Quoted()} {revision}";
        }
    }
}
