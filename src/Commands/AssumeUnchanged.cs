namespace Komorebi.Commands;

/// <summary>
///     ファイルの「変更なし」フラグを設定/解除するgitコマンド。
///     git update-index --assume-unchanged / --no-assume-unchanged を実行する。
///     このフラグを設定すると、gitはファイルの変更を追跡しなくなる。
/// </summary>
public class AssumeUnchanged : Command
{
    /// <summary>
    ///     AssumeUnchangedコマンドを初期化する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    /// <param name="file">対象ファイルのパス。</param>
    /// <param name="bAdd">trueの場合フラグを設定、falseの場合フラグを解除する。</param>
    public AssumeUnchanged(string repo, string file, bool bAdd)
    {
        // フラグの設定/解除モードを決定する
        var mode = bAdd ? "--assume-unchanged" : "--no-assume-unchanged";

        WorkingDirectory = repo;
        Context = repo;

        // git update-index: インデックス内のファイルのフラグを更新する
        Args = $"update-index {mode} -- {file.Quoted()}";
    }
}
