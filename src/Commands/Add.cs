namespace Komorebi.Commands;

/// <summary>
/// ファイルをステージングエリア（インデックス）に追加するgitコマンド。
/// git add --force --verbose --pathspec-from-file を実行する。
/// </summary>
public class Add : Command
{
    /// <summary>
    /// Addコマンドを初期化する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    /// <param name="pathspecFromFile">追加対象のファイルパスが記載されたファイルのパス。</param>
    public Add(string repo, string pathspecFromFile)
    {
        WorkingDirectory = repo;
        Context = repo;

        // git add --force: .gitignoreに含まれるファイルも強制追加
        // --verbose: 追加されたファイル名を出力
        // --pathspec-from-file: ファイルから追加対象のパスを読み込む
        Args = $"add --force --verbose --pathspec-from-file={pathspecFromFile.Quoted()}";
    }
}
