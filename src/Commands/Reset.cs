namespace Komorebi.Commands
{
    /// <summary>
    ///     HEADを指定リビジョンにリセット、またはステージングからファイルを取り除くgitコマンド。
    ///     git reset を実行する。
    /// </summary>
    public class Reset : Command
    {
        /// <summary>
        ///     HEADを指定リビジョンにリセットするコンストラクタ。
        /// </summary>
        /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
        /// <param name="revision">リセット先のリビジョン（コミットSHA、ブランチ名等）。</param>
        /// <param name="mode">リセットモード（--soft, --mixed, --hard 等）。</param>
        public Reset(string repo, string revision, string mode)
        {
            WorkingDirectory = repo;
            Context = repo;

            // git reset <mode> <revision>: HEADを指定リビジョンに移動する
            Args = $"reset {mode} {revision}";
        }

        /// <summary>
        ///     ステージングエリアからファイルをアンステージするコンストラクタ。
        /// </summary>
        /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
        /// <param name="pathspec">アンステージ対象のファイルパスが記載されたファイルのパス。</param>
        public Reset(string repo, string pathspec)
        {
            WorkingDirectory = repo;
            Context = repo;

            // git reset HEAD --pathspec-from-file: 指定ファイルをステージングから取り除く
            Args = $"reset HEAD --pathspec-from-file={pathspec.Quoted()}";
        }
    }
}
