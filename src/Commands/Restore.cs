namespace Komorebi.Commands;

/// <summary>
///     作業ツリーのファイルを復元するgitコマンド。
///     git restore を実行し、変更を破棄して最後のコミット状態に戻す。
/// </summary>
public class Restore : Command
{
    /// <summary>
    ///     Restoreコマンドを初期化する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    /// <param name="pathspecFile">復元対象のファイルパスが記載されたファイルのパス。</param>
    public Restore(string repo, string pathspecFile)
    {
        WorkingDirectory = repo;
        Context = repo;

        // git restore: 作業ツリーのファイルをHEADの状態に復元する
        // --progress: 進捗状況を表示
        // --worktree: 作業ツリーを対象にする
        // --recurse-submodules: サブモジュール内も再帰的に復元
        // --pathspec-from-file: ファイルから復元対象パスを読み込む
        Args = $"restore --progress --worktree --recurse-submodules --pathspec-from-file={pathspecFile.Quoted()}";
    }
}
