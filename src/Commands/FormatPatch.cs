namespace Komorebi.Commands;

/// <summary>
/// 指定コミットからパッチファイルを生成するgitコマンド。
/// git format-patch を実行し、メール送信可能な形式のパッチを作成する。
/// </summary>
public class FormatPatch : Command
{
    /// <summary>
    /// FormatPatchコマンドを初期化する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    /// <param name="commit">パッチ生成対象のコミットSHA。</param>
    /// <param name="saveTo">パッチファイルの保存先パス。</param>
    public FormatPatch(string repo, string commit, string saveTo)
    {
        WorkingDirectory = repo;
        Context = repo;

        // エディタは不要なので無効にする
        Editor = EditorType.None;

        // git format-patch: コミットからパッチファイルを生成する
        // -1: 指定コミット1つ分のパッチのみ生成
        // --output: パッチファイルの保存先を指定
        Args = $"format-patch {commit} -1 --output={saveTo.Quoted()}";
    }
}
