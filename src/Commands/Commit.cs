using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// ステージングされた変更をコミットするgitコマンド。
/// git commit --file を使用し、一時ファイル経由でコミットメッセージを渡す。
/// </summary>
public class Commit : Command
{
    /// <summary>
    /// Commitコマンドを初期化する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    /// <param name="message">コミットメッセージ。</param>
    /// <param name="signOff">Signed-off-byをメッセージに追加するかどうか。</param>
    /// <param name="noVerify">pre-commitフックをスキップするかどうか。</param>
    /// <param name="amend">直前のコミットを修正するかどうか。</param>
    /// <param name="resetAuthor">amend時に作成者情報をリセットするかどうか。</param>
    public Commit(string repo, string message, bool signOff, bool noVerify, bool amend, bool resetAuthor)
    {
        // コミットメッセージを一時ファイルに書き込むためのパスを取得する
        _tmpFile = Path.GetTempFileName();
        _message = message;

        WorkingDirectory = repo;
        Context = repo;

        var builder = new StringBuilder();

        // git commit --allow-empty: 変更がなくてもコミットを許可する
        // --file: コミットメッセージを一時ファイルから読み込む
        builder.Append("commit --allow-empty --file=");
        builder.Append(_tmpFile.Quoted());
        builder.Append(' ');

        // --signoff: Signed-off-byヘッダーをメッセージに追加する
        if (signOff)
            builder.Append("--signoff ");

        // --no-verify: pre-commitおよびcommit-msgフックをスキップする
        if (noVerify)
            builder.Append("--no-verify ");

        // --amend: 直前のコミットを修正する
        if (amend)
        {
            builder.Append("--amend ");

            // --reset-author: 作成者情報を現在のユーザーにリセットする
            if (resetAuthor)
                builder.Append("--reset-author ");

            // --no-edit: エディタを起動せずにメッセージを保持する
            builder.Append("--no-edit");
        }

        Args = builder.ToString();
    }

    /// <summary>
    /// コミットを実行する。
    /// 一時ファイルにメッセージを書き込み、git commitを実行後、一時ファイルを削除する。
    /// </summary>
    /// <returns>コミットが成功した場合はtrue。</returns>
    public async Task<bool> RunAsync()
    {
        try
        {
            // コミットメッセージを一時ファイルに書き込む
            await File.WriteAllTextAsync(_tmpFile, _message).ConfigureAwait(false);

            // git commitを非同期で実行する
            return await ExecAsync().ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
        finally
        {
            // 例外発生時も一時ファイルを確実に削除する（旧: catchブロックで削除されずリーク）
            try { File.Delete(_tmpFile); } catch { /* 削除失敗は無視 */ }
        }
    }

    /// <summary>
    /// コミットメッセージを格納する一時ファイルのパス。
    /// </summary>
    private readonly string _tmpFile;

    /// <summary>
    /// コミットメッセージの内容。
    /// </summary>
    private readonly string _message;
}
