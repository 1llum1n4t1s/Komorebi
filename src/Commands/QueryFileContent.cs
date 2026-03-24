using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Komorebi.Commands
{
    /// <summary>
    ///     指定リビジョンにおけるファイル内容をストリームとして取得する静的クラス。
    ///     通常ファイルとLFSファイルの両方をサポートする。
    /// </summary>
    public static class QueryFileContent
    {
        /// <summary>
        ///     git show を使用して、指定リビジョンのファイル内容をストリームとして取得する。
        /// </summary>
        /// <param name="repo">リポジトリのパス</param>
        /// <param name="revision">対象リビジョン</param>
        /// <param name="file">対象ファイルのパス</param>
        /// <returns>ファイル内容のストリーム</returns>
        public static async Task<Stream> RunAsync(string repo, string revision, string file)
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
            }
            catch (Exception e)
            {
                App.RaiseException(repo, App.Text("Error.FailedToQueryFileContent", e));
            }

            stream.Position = 0;
            return stream;
        }

        /// <summary>
        ///     git lfs smudge を使用して、LFSオブジェクトの実際のファイル内容をストリームとして取得する。
        /// </summary>
        /// <param name="repo">リポジトリのパス</param>
        /// <param name="oid">LFSオブジェクトのSHA256 OID</param>
        /// <param name="size">ファイルサイズ</param>
        /// <returns>ファイル内容のストリーム</returns>
        public static async Task<Stream> FromLFSAsync(string repo, string oid, long size)
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
            }
            catch (Exception e)
            {
                App.RaiseException(repo, App.Text("Error.FailedToQueryFileContent", e));
            }

            stream.Position = 0;
            return stream;
        }
    }
}
