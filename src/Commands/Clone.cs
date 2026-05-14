using System.Text;

namespace Komorebi.Commands;

/// <summary>
/// リモートリポジトリをローカルにクローンするgitコマンド。
/// git clone --progress --verbose を実行する。
/// </summary>
public class Clone : Command
{
    /// <summary>
    /// Cloneコマンドを初期化する。
    /// </summary>
    /// <param name="ctx">エラー表示用のコンテキスト文字列。</param>
    /// <param name="path">クローン先の親ディレクトリパス。</param>
    /// <param name="url">クローン元のリポジトリURL。</param>
    /// <param name="localName">ローカルディレクトリ名（空の場合はリポジトリ名が使用される）。</param>
    /// <param name="sshKey">SSH認証用の秘密鍵パス。</param>
    /// <param name="extraArgs">追加のクローンオプション（--depth, --branch等）。</param>
    public Clone(string ctx, string path, string url, string localName, string sshKey, string extraArgs)
    {
        Context = ctx;
        WorkingDirectory = path;
        SSHKey = sshKey;

        // AWS CodeCommit (GRC / HTTPS / SSH 形式) なら GCM を完全抑止する。
        // GRC URL (`codecommit::region://...`) 形式では git-remote-codecommit が SigV4 署名済みの
        // 巨大な一時 HTTPS URL を内部生成し、その資格情報を GCM が Windows 資格情報マネージャに
        // 保存しようとして 0x6c6 (ERROR_INVALID_BOUND 系) で clone / fetch / pull が失敗するため。
        if (Models.Remote.IsCodeCommitURL(url))
            DisableCredentialHelper = true;

        var builder = new StringBuilder(1024);

        // git clone: リモートリポジトリをローカルにコピーする
        // --progress: 進捗状況を表示
        // --verbose: 詳細な情報を出力
        builder.Append("clone --progress --verbose ");

        // 追加オプション（--depth, --single-branch等）があれば付加する
        if (!string.IsNullOrEmpty(extraArgs))
            builder.Append(extraArgs).Append(' ');

        // クローン元URLを指定する。Quoted() で囲んで、URL に空白や引用符を含む値が
        // 渡されても引数境界が崩れないように防衛する（他 Command の規約と統一）。
        builder.Append(url.Quoted()).Append(' ');

        // ローカルディレクトリ名が指定されている場合に追加する
        if (!string.IsNullOrEmpty(localName))
            builder.Append(localName.Quoted());

        Args = builder.ToString();
    }
}
