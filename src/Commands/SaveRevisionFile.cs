// nullable 移行未実施。1 ファイルずつ null 注釈を入れてこの 2 行を削除していく。
#nullable disable warnings
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
    /// <returns>保存に成功した場合はtrue</returns>
    /// <remarks>
    /// git が失敗しても保存先を書き換えないよう、一時ファイルへ書き出して
    /// 成功時のみ差し替える。失敗時に保存先を切り詰めると、既存ファイルへ
    /// 上書き保存したユーザーの元データを壊してしまうため。
    /// </remarks>
    public static async Task<bool> RunAsync(string repo, string revision, string file, string saveTo)
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
            if (pointerStream is null)
            {
                App.RaiseException(repo, App.Text("Error.FailedToSaveRevisionFile", file));
                return false;
            }

            return await ExecCmdAsync(repo, "lfs smudge", saveTo, pointerStream).ConfigureAwait(false);
        }

        // 通常ファイル: git show で直接取得
        return await ExecCmdAsync(repo, $"show {revision}:{file.Quoted()}", saveTo).ConfigureAwait(false);
    }

    /// <summary>
    /// gitコマンドを実行し、標準出力をファイルに保存する。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    /// <param name="args">gitコマンドの引数</param>
    /// <param name="outputFile">出力先ファイルパス</param>
    /// <param name="input">標準入力に渡すストリーム（LFS smudge用）</param>
    /// <returns>gitが正常終了し、保存先へ差し替えできた場合はtrue</returns>
    private static async Task<bool> ExecCmdAsync(string repo, string args, string outputFile, Stream input = null)
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

        // 成功が確定するまで保存先には触れない
        var tmpFile = outputFile + ".komorebi.tmp";
        try
        {
            await using (var sw = File.Create(tmpFile))
            {
                using var proc = Process.Start(starter)!;

                if (input is not null)
                {
                    using var inputReader = new StreamReader(input);
                    var inputString = await inputReader.ReadToEndAsync().ConfigureAwait(false);
                    await proc.StandardInput.WriteAsync(inputString).ConfigureAwait(false);
                }

                // 入力終端を子プロセスへ伝える（UpdateIndexInfo と同じ契約）。
                proc.StandardInput.Close();

                // stderr は RedirectStandardError=true にした以上、必ず並列に読み切る。
                // stdout だけを待つと、子プロセスが stderr パイプを埋めた時点で
                // 「親は stdout 待ち / 子は stderr 書き込み待ち」で相互ブロックする。
                // 実測では stderr が 16KB を超えると確実にデッドロックし、
                // 大きな LFS オブジェクトの取得では進捗出力がこの量に達し得る。
                var stderrTask = proc.StandardError.ReadToEndAsync();

                await proc.StandardOutput.BaseStream.CopyToAsync(sw).ConfigureAwait(false);
                var stderr = await stderrTask.ConfigureAwait(false);
                await proc.WaitForExitAsync().ConfigureAwait(false);

                // ExitCode を見ないと、git show の失敗や lfs smudge の取得失敗
                // （ポインタをそのまま stdout へ echo して非 0 終了する）を
                // 「保存成功」として確定させてしまう。
                if (proc.ExitCode != 0)
                {
                    App.RaiseException(repo, App.Text("Error.FailedToSaveRevisionFile",
                        string.IsNullOrWhiteSpace(stderr) ? $"git exited with {proc.ExitCode}" : stderr.Trim()));
                    return false;
                }
            }

            File.Move(tmpFile, outputFile, overwrite: true);
            return true;
        }
        catch (Exception e)
        {
            App.RaiseException(repo, App.Text("Error.FailedToSaveRevisionFile", e.Message));
            return false;
        }
        finally
        {
            // 失敗時に一時ファイルを残さない（成功時は Move 済みなので存在しない）
            try
            {
                if (File.Exists(tmpFile))
                    File.Delete(tmpFile);
            }
            catch
            {
                // 後始末の失敗は本処理の成否に影響させない
            }
        }
    }
}
