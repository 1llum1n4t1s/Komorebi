using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// 指定リビジョンのファイルをローカルに保存する静的クラス。
/// LFSフィルタ対象の場合は lfs smudge を使用し、通常のファイルは git show で取得する。
/// </summary>
public static class SaveRevisionFile
{
    /// <summary>
    /// 指定リビジョンのファイルを指定パスに保存する。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    /// <param name="revision">対象リビジョン</param>
    /// <param name="file">対象ファイルのパス</param>
    /// <param name="saveTo">保存先ファイルパス</param>
    public static async Task RunAsync(string repo, string revision, string file, string saveTo)
    {
        // 保存先ディレクトリが存在しない場合は作成
        var dir = Path.GetDirectoryName(saveTo) ?? string.Empty;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        // LFSフィルタ対象かどうかを確認
        var isLFSFiltered = await new IsLFSFiltered(repo, revision, file).GetResultAsync().ConfigureAwait(false);
        if (isLFSFiltered)
        {
            // LFSファイル: ポインタを取得し、lfs smudge で実ファイルに展開
            var pointerStream = await QueryFileContent.RunAsync(repo, revision, file).ConfigureAwait(false);
            await ExecCmdAsync(repo, "lfs smudge", saveTo, pointerStream).ConfigureAwait(false);
        }
        else
        {
            // 通常ファイル: git show で直接取得
            await ExecCmdAsync(repo, $"show {revision}:{file.Quoted()}", saveTo).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// gitコマンドを実行し、標準出力をファイルに保存する。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    /// <param name="args">gitコマンドの引数</param>
    /// <param name="outputFile">出力先ファイルパス</param>
    /// <param name="input">標準入力に渡すストリーム（LFS smudge用）</param>
    private static async Task ExecCmdAsync(string repo, string args, string outputFile, Stream input = null)
    {
        var starter = new ProcessStartInfo();
        starter.WorkingDirectory = repo;
        starter.FileName = Native.OS.GitExecutable;
        starter.Arguments = args;
        starter.UseShellExecute = false;
        starter.CreateNoWindow = true;
        starter.WindowStyle = ProcessWindowStyle.Hidden;
        starter.RedirectStandardInput = true;
        starter.RedirectStandardOutput = true;
        starter.RedirectStandardError = true;

        await using (var sw = File.Create(outputFile))
        {
            try
            {
                using var proc = Process.Start(starter)!;

                if (input is not null)
                {
                    using var inputReader = new StreamReader(input);
                    var inputString = await inputReader.ReadToEndAsync().ConfigureAwait(false);
                    await proc.StandardInput.WriteAsync(inputString).ConfigureAwait(false);
                }

                await proc.StandardOutput.BaseStream.CopyToAsync(sw).ConfigureAwait(false);
                await proc.WaitForExitAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                App.RaiseException(repo, App.Text("Error.FailedToSaveRevisionFile", e.Message));
            }
        }
    }
}
