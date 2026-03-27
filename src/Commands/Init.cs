namespace Komorebi.Commands;

/// <summary>
///     新しいgitリポジトリを初期化するコマンド。
///     git init -q を実行する。
/// </summary>
public class Init : Command
{
    /// <summary>
    ///     Initコマンドを初期化する。
    /// </summary>
    /// <param name="ctx">エラー表示用のコンテキスト文字列。</param>
    /// <param name="dir">リポジトリを初期化するディレクトリパス。</param>
    public Init(string ctx, string dir)
    {
        Context = ctx;
        WorkingDirectory = dir;

        // git init -q: 新しいリポジトリを静かモードで初期化する
        Args = "init -q";
    }
}
