using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

    /// <summary>
    /// クローンとサブモジュール初期化を実行し、Windowsの長いパスによる失敗を一度だけ復旧する。
    /// </summary>
    /// <param name="repositoryPath">クローン先の絶対パス。</param>
    /// <param name="initializeSubmodules">サブモジュールも再帰的に初期化するかどうか。</param>
    /// <returns>クローンと必要な復旧・初期化が完了した場合はtrue。</returns>
    public async Task<bool> CloneAsync(string repositoryPath, bool initializeSubmodules)
    {
        var originalArgs = Args;
        var originalDirectory = WorkingDirectory;
        var raiseError = RaiseError;
        var ownsDestination = IsUnusedDestination(repositoryPath);
        var retried = false;
        RaiseError = false;

        try
        {
            var success = await ExecAsync().ConfigureAwait(false);
            if (!success)
            {
                // 上流との差分: 新規クローンの長いパスエラーだけ、取得済みHEADから展開を再開する。
                // cloneを再実行すると、取得済みの非空フォルダーを理由に失敗してしまう。
                if (!ownsDestination || !CanRetryWithLongPaths(repositoryPath))
                    return ReportFailure(raiseError);

                var submodulesFailed = ErrorMessage.Contains("submodule path", StringComparison.Ordinal);
                retried = true;
                if (!await EnableLongPathsAsync(repositoryPath).ConfigureAwait(false) ||
                    !await ExecInRepositoryAsync(repositoryPath, "-c core.longpaths=true reset --hard HEAD").ConfigureAwait(false))
                    return ReportFailure(raiseError);

                initializeSubmodules |= submodulesFailed;
            }

            if (initializeSubmodules &&
                (Directory.Exists(Path.Combine(repositoryPath, ".git")) || File.Exists(Path.Combine(repositoryPath, ".git"))))
            {
                var args = retried
                    ? "-c core.longpaths=true submodule update --init --recursive --checkout --force"
                    : "submodule update --init --recursive";
                success = await UpdateSubmodulesAsync(repositoryPath, args).ConfigureAwait(false);
                if (!success)
                {
                    if (retried || !ownsDestination || !CanRetryWithLongPaths(repositoryPath))
                        return ReportFailure(raiseError);

                    retried = true;
                    if (!await EnableLongPathsAsync(repositoryPath).ConfigureAwait(false) ||
                        !await UpdateSubmodulesAsync(repositoryPath, "-c core.longpaths=true submodule update --init --recursive --checkout --force").ConfigureAwait(false))
                        return ReportFailure(raiseError);
                }
            }

            return !CancellationToken.IsCancellationRequested;
        }
        finally
        {
            Args = originalArgs;
            WorkingDirectory = originalDirectory;
            RaiseError = raiseError;
        }
    }

    private bool CanRetryWithLongPaths(string repositoryPath)
    {
        return OperatingSystem.IsWindows() && !CancellationToken.IsCancellationRequested &&
            ErrorMessage.Contains("Filename too long", StringComparison.OrdinalIgnoreCase) &&
            (Directory.Exists(Path.Combine(repositoryPath, ".git")) || File.Exists(Path.Combine(repositoryPath, ".git")));
    }

    private static bool IsUnusedDestination(string repositoryPath)
    {
        try
        {
            return !Directory.Exists(repositoryPath) || !Directory.EnumerateFileSystemEntries(repositoryPath).Any();
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private Task<bool> EnableLongPathsAsync(string repositoryPath)
    {
        // Git for Windowsは起動初期にlongpathsをキャッシュするため、globalの明示的falseも更新する。
        return ExecInRepositoryAsync(repositoryPath, "config --global core.longpaths true");
    }

    private Task<bool> ExecInRepositoryAsync(string repositoryPath, string args)
    {
        WorkingDirectory = repositoryPath;
        Args = args;
        return ExecAsync();
    }

    private async Task<bool> UpdateSubmodulesAsync(string repositoryPath, string args)
    {
        // 上流との差分: 親リモートの認証指定を別ホストのサブモジュールへ引き継がない。
        // 従来の独立したSubmoduleコマンドと同じく、親環境とホスト別SSH設定を使用する。
        var sshKey = SSHKey;
        var disableCredentialHelper = DisableCredentialHelper;
        SSHKey = string.Empty;
        DisableCredentialHelper = false;
        try
        {
            return await ExecInRepositoryAsync(repositoryPath, args).ConfigureAwait(false);
        }
        finally
        {
            SSHKey = sshKey;
            DisableCredentialHelper = disableCredentialHelper;
        }
    }

    private bool ReportFailure(bool raiseError)
    {
        if (raiseError && !CancellationToken.IsCancellationRequested && !string.IsNullOrEmpty(ErrorMessage))
            App.RaiseException(Context, ErrorMessage);
        return false;
    }
}
