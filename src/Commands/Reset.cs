namespace Komorebi.Commands;

/// <summary>
/// HEADを指定リビジョンにリセットする、またはファイルをアンステージするgitコマンド。
/// git reset を実行する。
/// </summary>
public class Reset : Command
{
    /// <summary>
    /// 指定リビジョンとモードでリセットするコマンドを初期化する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス</param>
    /// <param name="revision">リセット先のリビジョン（コミットSHAまたはブランチ名）</param>
    /// <param name="mode">リセットモード（--soft, --mixed, --hard のいずれか）</param>
    public Reset(string repo, string revision, string mode)
    {
        WorkingDirectory = repo;
        Context = repo;
        // git reset <mode> <revision>: 指定モードでHEADを移動する
        Args = $"reset {mode} {revision}";
    }

    /// <summary>
    /// pathspec-from-fileを使用してファイルをアンステージするコマンドを初期化する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス</param>
    /// <param name="pathspec">アンステージ対象のファイルパスが列挙されたファイルのパス</param>
    public Reset(string repo, string pathspec)
    {
        WorkingDirectory = repo;
        Context = repo;
        // git reset --pathspec-from-file: ファイルからパスリストを読んでアンステージする
        Args = $"reset --pathspec-from-file={pathspec.Quoted()}";
    }
}
