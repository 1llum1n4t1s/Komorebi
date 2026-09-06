using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// 2つのリビジョン間の差分を比較し、変更されたファイル一覧を取得するgitコマンド。
/// git diff --name-status を実行する。
/// </summary>
public class CompareRevisions : Command
{
    /// <summary>
    /// CompareRevisionsコマンドを初期化する（リポジトリ全体の比較）。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    /// <param name="start">比較開始リビジョン。空の場合は作業ツリーとの逆方向比較（-R）。</param>
    /// <param name="end">比較終了リビジョン。</param>
    public CompareRevisions(string repo, string start, string end)
    {
        WorkingDirectory = repo;
        Context = repo;

        // startが空の場合は-R（逆方向diff）を使用し、作業ツリーとendの差分を取る
        // （-R はフラグなのでクォートしない。リビジョン側は ref 名に `"` が入り得るためクォートする）
        var based = string.IsNullOrEmpty(start) ? "-R" : start.Quoted();
        Args = $"diff --name-status {based} {end.Quoted()}";
    }

    /// <summary>
    /// CompareRevisionsコマンドを初期化する（特定パスに絞った比較）。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    /// <param name="start">比較開始リビジョン。空の場合は作業ツリーとの逆方向比較（-R）。</param>
    /// <param name="end">比較終了リビジョン。</param>
    /// <param name="path">比較対象を限定するパス。</param>
    public CompareRevisions(string repo, string start, string end, string path)
    {
        WorkingDirectory = repo;
        Context = repo;

        // startが空の場合は-R（逆方向diff）を使用し、作業ツリーとendの差分を取る
        // （-R はフラグなのでクォートしない。リビジョン側は ref 名に `"` が入り得るためクォートする）
        var based = string.IsNullOrEmpty(start) ? "-R" : start.Quoted();
        Args = $"diff --name-status {based} {end.Quoted()} -- {path.Quoted()}";
    }

    /// <summary>
    /// 差分比較を非同期で実行し、変更ファイルの一覧を返す。
    /// </summary>
    /// <returns>変更されたファイルのリスト（パスの数値順でソート済み）。</returns>
    public async Task<List<Models.Change>> ReadAsync()
    {
        try
        {
            return await ReadAsync(CreateGitStartInfo(true), CancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // 起動設定の作成失敗も従来どおり空の一覧として扱う。
            return [];
        }
    }

    /// <summary>差分プロセスを所有し、実終了まで出力を読み取る。</summary>
    internal static async Task<List<Models.Change>> ReadAsync(ProcessStartInfo start, CancellationToken cancellationToken)
    {
        List<Models.Change> changes = [];
        if (cancellationToken.IsCancellationRequested)
            return changes;
        try
        {
            // gitプロセスを起動して差分出力を取得する
            using var proc = new Process();
            proc.StartInfo = start;
            proc.Start();

            // CommitDetail はコミット切替のたびに CancellationToken を渡してくる。
            // ここで尊重しないと、切替を連打した分だけ巨大 diff の git プロセスが
            // 走り続けて積み上がる。基底の ReadToEndAsync と同じくプロセスツリーごと落とす。
            using var cancelRegistration = cancellationToken.Register(() => Native.CommandCancellation.Terminate(proc, false));

            var stderrDrain = DrainReaderAsync(proc.StandardError);

            // 基底クラスの共通パーサーを使用して--name-status出力を解析する
            // 中止通知では読み取りを抜けず、Kill後のEOFまで登録を保持する。
            while (await proc.StandardOutput.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                var parsed = ParseNameStatusLine(line);
                if (parsed is null)
                    continue;

                // パース結果からChangeモデルを生成する
                var change = new Models.Change() { Path = parsed.Value.path };
                change.Set(parsed.Value.state);
                changes.Add(change);
            }

            await proc.WaitForExitAsync().ConfigureAwait(false);
            await stderrDrain.ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
                return [];

            // パスの数値を考慮した自然順ソートを行う
            changes.Sort((l, r) => Models.NumericSort.Compare(l.Path, r.Path));
        }
        catch
        {
            // gitプロセスの実行エラーは無視し、空のリストを返す
        }

        return changes;
    }
}
