using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// コミットのGPG/SSH署名情報を取得するクラス。
/// 検証結果、署名者、鍵情報を返す。
/// </summary>
public class QueryCommitSignInfo : Command
{
    /// <summary>
    /// コンストラクタ。署名検証情報を取得するコマンドを設定する。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    /// <param name="sha">対象コミットのSHA</param>
    /// <param name="useFakeSignersFile">ダミーの許可済み署名者ファイルを使用するかどうか</param>
    public QueryCommitSignInfo(string repo, string sha, bool useFakeSignersFile)
    {
        WorkingDirectory = repo;
        Context = repo;

        // %G?: 署名検証結果、%GS: 署名者、%GK: 署名鍵
        const string baseArgs = "show --no-show-signature --format=%G?%n%GS%n%GK -s";
        // SSH署名の場合、許可済み署名者ファイルをダミーに設定して検証をバイパス
        const string fakeSignersFileArg = "-c gpg.ssh.allowedSignersFile=/dev/null";
        Args = $"{(useFakeSignersFile ? fakeSignersFileArg : string.Empty)} {baseArgs} {sha}";
    }

    /// <summary>
    /// コマンドを非同期で実行し、署名検証情報を返す。
    /// </summary>
    /// <returns>署名情報。署名がない場合や失敗時はnull</returns>
    public async Task<Models.CommitSignInfo> GetResultAsync()
    {
        var rs = await ReadToEndAsync().ConfigureAwait(false);
        if (!rs.IsSuccess)
            return null;

        var raw = rs.StdOut.Trim().ReplaceLineEndings("\n");
        // 署名情報が1文字以下の場合は署名なし
        if (raw.Length <= 1)
            return null;

        var lines = raw.Split('\n');
        if (lines.Length < 3 || lines[0].Length == 0)
            return null;

        return new Models.CommitSignInfo()
        {
            VerifyResult = lines[0][0],
            Signer = lines[1],
            Key = lines[2]
        };
    }
}
