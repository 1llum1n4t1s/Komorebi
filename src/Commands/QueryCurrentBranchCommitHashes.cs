using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// 現在のブランチにおける指定タイムスタンプ以降のコミットハッシュを取得するクラス。
/// マージ済みコミットの判定に使用される。
/// </summary>
public class QueryCurrentBranchCommitHashes : Command
{
    /// <summary>
    /// コンストラクタ。指定タイムスタンプ以降のコミットSHAを取得するコマンドを設定する。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    /// <param name="sinceTimestamp">検索開始のUnixタイムスタンプ</param>
    public QueryCurrentBranchCommitHashes(string repo, ulong sinceTimestamp)
    {
        // Unixタイムスタンプをローカル日時文字列に変換
        var since = DateTime.UnixEpoch.AddSeconds(sinceTimestamp).ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss");
        WorkingDirectory = repo;
        Context = repo;
        Args = $"log --since={since.Quoted()} --format=%H";
    }

    /// <summary>
    /// コマンドを非同期で実行し、コミットSHAのハッシュセットを返す。
    /// </summary>
    /// <returns>コミットSHAのハッシュセット</returns>
    public async Task<HashSet<string>> GetResultAsync()
    {
        var outs = new HashSet<string>();

        try
        {
            using var proc = new Process();
            proc.StartInfo = CreateGitStartInfo(true);
            proc.Start();

            // 8文字以上の行をSHAとして追加
            while (await proc.StandardOutput.ReadLineAsync().ConfigureAwait(false) is { Length: > 8 } line)
                outs.Add(line);

            await proc.WaitForExitAsync().ConfigureAwait(false);
        }
        catch
        {
            // 例外は無視する
        }

        return outs;
    }
}
