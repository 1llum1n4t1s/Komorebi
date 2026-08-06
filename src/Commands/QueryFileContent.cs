using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// 指定リビジョンにおけるファイル内容をストリームとして取得する静的クラス。
/// 通常ファイルとLFSファイルの両方をサポートする。
/// </summary>
public static class QueryFileContent
{
    /// <summary>
    /// git show を使用して、指定リビジョンのファイル内容をストリームとして取得する。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    /// <param name="revision">対象リビジョン</param>
    /// <param name="file">対象ファイルのパス</param>
    /// <returns>ファイル内容のストリーム。取得に失敗した場合は null</returns>
    /// <remarks>
    /// 失敗時は null を返す。空ストリームを返すと「中身が空のファイル」と区別できず、
    /// 呼び出し側が失敗を成功として扱ってしまうため（空ファイル自体は正当な内容なので、
    /// 出力長ではなく ExitCode で成否を判定する）。
    /// </remarks>
    public static async Task<Stream?> RunAsync(string repo, string revision, string file)
    {
        var starter = new ProcessStartInfo();
        starter.WorkingDirectory = repo;
        starter.FileName = Native.OS.GitExecutable;
        starter.Arguments = $"show {revision}:{file.Quoted()}";
        starter.UseShellExecute = false;
        starter.CreateNoWindow = true;
        starter.WindowStyle = ProcessWindowStyle.Hidden;
        starter.RedirectStandardOutput = true;

        var stream = new MemoryStream();
        try
        {
            using var proc = Process.Start(starter)!;
            await proc.StandardOutput.BaseStream.CopyToAsync(stream).ConfigureAwait(false);
            await proc.WaitForExitAsync().ConfigureAwait(false);

            if (proc.ExitCode != 0)
            {
                await stream.DisposeAsync().ConfigureAwait(false);
                return null;
            }
        }
        catch (Exception e)
        {
            App.RaiseException(repo, App.Text("Error.FailedToQueryFileContent", e));
            await stream.DisposeAsync().ConfigureAwait(false);
            return null;
        }

        stream.Position = 0;
        return stream;
    }

    /// <summary>
    /// git lfs smudge を使用して、LFSオブジェクトの実際のファイル内容をストリームとして取得する。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    /// <param name="oid">LFSオブジェクトのSHA256 OID</param>
    /// <param name="size">ファイルサイズ</param>
    /// <returns>ファイル内容のストリーム。取得に失敗した場合は null</returns>
    /// <remarks>
    /// <see cref="RunAsync"/> と同じく ExitCode で成否を判定し、失敗時は null を返す。
    /// なお git lfs smudge は stdin の EOF を待たずにポインタを処理するため
    /// （成功・失敗どちらの経路でも実測済み）、StandardInput の明示クローズは不要。
    /// </remarks>
    public static async Task<Stream?> FromLFSAsync(string repo, string oid, long size)
    {
        var starter = new ProcessStartInfo();
        starter.WorkingDirectory = repo;
        starter.FileName = Native.OS.GitExecutable;
        starter.Arguments = "lfs smudge";
        starter.UseShellExecute = false;
        starter.CreateNoWindow = true;
        starter.WindowStyle = ProcessWindowStyle.Hidden;
        starter.RedirectStandardInput = true;
        starter.RedirectStandardOutput = true;

        var stream = new MemoryStream();
        try
        {
            using var proc = Process.Start(starter)!;
            await proc.StandardInput.WriteLineAsync("version https://git-lfs.github.com/spec/v1").ConfigureAwait(false);
            await proc.StandardInput.WriteLineAsync($"oid sha256:{oid}").ConfigureAwait(false);
            await proc.StandardInput.WriteLineAsync($"size {size}").ConfigureAwait(false);
            await proc.StandardOutput.BaseStream.CopyToAsync(stream).ConfigureAwait(false);
            await proc.WaitForExitAsync().ConfigureAwait(false);

            if (proc.ExitCode != 0)
            {
                await stream.DisposeAsync().ConfigureAwait(false);
                return null;
            }
        }
        catch (Exception e)
        {
            App.RaiseException(repo, App.Text("Error.FailedToQueryFileContent", e));
            await stream.DisposeAsync().ConfigureAwait(false);
            return null;
        }

        stream.Position = 0;
        return stream;
    }
}
