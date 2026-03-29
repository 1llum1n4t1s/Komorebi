using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// ローカルの変更を破棄するための静的ユーティリティクラス。
/// 全変更の一括破棄と、選択した変更のみの破棄をサポートする。
/// </summary>
public static class Discard
{
    /// <summary>
    /// 全てのローカル変更（ステージ済み・未ステージ）を破棄する。
    /// 未追跡ファイルの削除とgit reset --hardの組み合わせで実現する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    /// <param name="includeUntracked">未追跡ファイルも削除するかどうか。</param>
    /// <param name="includeIgnored">無視ファイルも削除するかどうか。</param>
    /// <param name="log">コマンドログの出力先。</param>
    public static async Task AllAsync(string repo, bool includeUntracked, bool includeIgnored, Models.ICommandLog log)
    {
        if (includeUntracked)
        {
            // `.git`ファイルを含む未追跡パス（デタッチされたサブモジュール等）は手動で削除する必要がある
            // Untracked paths that contains `.git` file (detached submodule) must be removed manually.
            var changes = await new QueryLocalChanges(repo).GetResultAsync().ConfigureAwait(false);
            try
            {
                // 未追跡・新規追加・リネームされたファイルのディレクトリを削除する
                foreach (var c in changes)
                {
                    if (c.WorkTree == Models.ChangeState.Untracked ||
                        c.WorkTree == Models.ChangeState.Added ||
                        c.Index == Models.ChangeState.Added ||
                        c.Index == Models.ChangeState.Renamed)
                    {
                        var fullPath = Path.Combine(repo, c.Path);
                        if (Directory.Exists(fullPath))
                            Directory.Delete(fullPath, true);
                    }
                }
            }
            catch (Exception e)
            {
                App.RaiseException(repo, App.Text("Error.FailedToDiscard", e.Message));
            }

            // git cleanで残りの未追跡ファイルを削除する
            if (includeIgnored)
                await new Clean(repo, Models.CleanMode.All).Use(log).ExecAsync().ConfigureAwait(false);
            else
                await new Clean(repo, Models.CleanMode.OnlyUntrackedFiles).Use(log).ExecAsync().ConfigureAwait(false);
        }
        else if (includeIgnored)
        {
            // 無視ファイルのみを削除する
            await new Clean(repo, Models.CleanMode.OnlyIgnoredFiles).Use(log).ExecAsync().ConfigureAwait(false);
        }

        // git reset --hard: HEADの状態にインデックスと作業ツリーをリセットする
        await new Reset(repo, "", "--hard").Use(log).ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 選択した変更のみを破棄する（未ステージの変更のみ対象）。
    /// 未追跡ファイルは直接削除し、変更されたファイルはgit restoreで復元する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    /// <param name="changes">破棄対象の変更リスト。</param>
    /// <param name="log">コマンドログの出力先。</param>
    public static async Task ChangesAsync(string repo, List<Models.Change> changes, Models.ICommandLog log)
    {
        // git restoreで復元するファイルのリスト
        var restores = new List<string>();

        try
        {
            foreach (var c in changes)
            {
                if (c.WorkTree == Models.ChangeState.Untracked || c.WorkTree == Models.ChangeState.Added)
                {
                    // 未追跡・新規追加ファイルはファイルシステムから直接削除する
                    var fullPath = Path.Combine(repo, c.Path);
                    if (Directory.Exists(fullPath))
                        Directory.Delete(fullPath, true);
                    else
                        File.Delete(fullPath);
                }
                else
                {
                    // 変更されたファイルはgit restoreで復元するリストに追加する
                    restores.Add(c.Path);
                }
            }
        }
        catch (Exception e)
        {
            App.RaiseException(repo, App.Text("Error.FailedToDiscard", e.Message));
        }

        // git restoreで復元するファイルがある場合、一時ファイル経由で実行する
        if (restores.Count > 0)
        {
            var pathSpecFile = Path.GetTempFileName();
            await File.WriteAllLinesAsync(pathSpecFile, restores).ConfigureAwait(false);
            await new Restore(repo, pathSpecFile).Use(log).ExecAsync().ConfigureAwait(false);
            File.Delete(pathSpecFile);
        }
    }
}
