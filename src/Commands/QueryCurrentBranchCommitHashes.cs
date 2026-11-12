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

        // Komorebi 独自: 呼び出し元（QueryCommits）から渡された CancellationToken で中断できるようにする。
        // 中断済みなら git を起動せず、途中で中断された場合は部分的な集合を返さず空を返す
        // （呼び出し元は中断後の結果を使わないが、部分集合で誤ったマージ済み判定をしないよう安全側に倒す）。
        if (CancellationToken.IsCancellationRequested)
            return outs;

        try
        {
            using var proc = new Process();
            proc.StartInfo = CreateGitStartInfo(true);
            proc.Start();

            // Command.ReadToEndAsync と同じく、プロセスの実終了（WaitForExitAsync）まで Kill 登録を保持する
            using var registration = CancellationToken.Register(() => Native.CommandCancellation.Terminate(proc, false));
            var stderrDrain = DrainReaderAsync(proc.StandardError);

            // 8文字以上の行をSHAとして追加
            while (await proc.StandardOutput.ReadLineAsync().ConfigureAwait(false) is { Length: > 8 } line)
                outs.Add(line);

            await proc.WaitForExitAsync().ConfigureAwait(false);
            await stderrDrain.ConfigureAwait(false);

            if (CancellationToken.IsCancellationRequested)
                outs.Clear();
        }
        catch
        {
            // 例外は無視する。ただし中断（Kill による読取失敗を含む）で例外になった場合は、
            // 途中まで読んだ部分集合を返さない（正常終了の経路と同じく空にする）。
            if (CancellationToken.IsCancellationRequested)
                outs.Clear();
        }

        return outs;
    }
}
