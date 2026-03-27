using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
///     変更内容をパッチファイルとして保存する静的クラス。
///     git diff を使用して、各変更のdiff出力をファイルに書き込む。
/// </summary>
public static class SaveChangesAsPatch
{
    /// <summary>
    ///     ローカルの変更をパッチファイルとして保存する。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    /// <param name="changes">保存対象の変更リスト</param>
    /// <param name="isUnstaged">未ステージの変更かどうか</param>
    /// <param name="saveTo">保存先ファイルパス</param>
    /// <returns>成功時true</returns>
    public static async Task<bool> ProcessLocalChangesAsync(string repo, List<Models.Change> changes, bool isUnstaged, string saveTo)
    {
        await using (var sw = File.Create(saveTo))
        {
            foreach (var change in changes)
            {
                if (!await ProcessSingleChangeAsync(repo, new Models.DiffOption(change, isUnstaged), sw))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     リビジョン間の比較結果をパッチファイルとして保存する。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    /// <param name="changes">保存対象の変更リスト</param>
    /// <param name="baseRevision">比較元リビジョン</param>
    /// <param name="targetRevision">比較先リビジョン</param>
    /// <param name="saveTo">保存先ファイルパス</param>
    /// <returns>成功時true</returns>
    public static async Task<bool> ProcessRevisionCompareChangesAsync(string repo, List<Models.Change> changes, string baseRevision, string targetRevision, string saveTo)
    {
        await using (var sw = File.Create(saveTo))
        {
            foreach (var change in changes)
            {
                if (!await ProcessSingleChangeAsync(repo, new Models.DiffOption(baseRevision, targetRevision, change), sw))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     スタッシュの変更をパッチファイルとして保存する。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    /// <param name="opts">diffオプションのリスト</param>
    /// <param name="saveTo">保存先ファイルパス</param>
    /// <returns>成功時true</returns>
    public static async Task<bool> ProcessStashChangesAsync(string repo, List<Models.DiffOption> opts, string saveTo)
    {
        await using (var sw = File.Create(saveTo))
        {
            foreach (var opt in opts)
            {
                if (!await ProcessSingleChangeAsync(repo, opt, sw))
                    return false;
            }
        }
        return true;
    }

    /// <summary>
    ///     単一の変更のdiff出力をストリームに書き込む。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    /// <param name="opt">diffオプション</param>
    /// <param name="writer">出力先ファイルストリーム</param>
    /// <returns>成功時true</returns>
    private static async Task<bool> ProcessSingleChangeAsync(string repo, Models.DiffOption opt, FileStream writer)
    {
        var starter = new ProcessStartInfo();
        starter.WorkingDirectory = repo;
        starter.FileName = Native.OS.GitExecutable;
        starter.Arguments = $"diff --no-color --no-ext-diff --ignore-cr-at-eol --unified=4 {opt}";
        starter.UseShellExecute = false;
        starter.CreateNoWindow = true;
        starter.WindowStyle = ProcessWindowStyle.Hidden;
        starter.RedirectStandardOutput = true;

        try
        {
            using var proc = Process.Start(starter)!;
            await proc.StandardOutput.BaseStream.CopyToAsync(writer).ConfigureAwait(false);
            await proc.WaitForExitAsync().ConfigureAwait(false);
            return proc.ExitCode == 0;
        }
        catch (Exception e)
        {
            App.RaiseException(repo, App.Text("Error.FailedToSavePatch", e.Message));
            return false;
        }
    }
}
