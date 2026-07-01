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
        WorkingDirectory = repo;
        Context = repo;
        // Unixタイムスタンプは`@<timestamp>`形式でそのままgitに渡せる（日時文字列への変換不要）
        Args = $"log --since=@{sinceTimestamp} --format=%H";
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
            var stderrDrain = DrainReaderAsync(proc.StandardError);

            // 8文字以上の行をSHAとして追加
            while (await proc.StandardOutput.ReadLineAsync().ConfigureAwait(false) is { Length: > 8 } line)
                outs.Add(line);

            await proc.WaitForExitAsync().ConfigureAwait(false);
            await stderrDrain.ConfigureAwait(false);
        }
        catch
        {
            // 例外は無視する
        }

        return outs;
    }
}
