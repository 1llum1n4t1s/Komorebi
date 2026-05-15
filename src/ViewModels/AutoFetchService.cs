using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Threading;

namespace Komorebi.ViewModels;

/// <summary>
/// グローバル自動フェッチ＆リモート到達性スキャンを一本化して回すシングルトン・バックグラウンドサービス。
/// <para>
/// 旧設計: <c>Repository._autoFetchTimer</c> がリポジトリごとに 5 秒ティックで回り、
///        開いているタブの git fetch だけ行っていた。
/// 新設計: 1 つのサービスが <see cref="Preferences.EnableAutoFetch"/> /
///        <see cref="Preferences.AutoFetchInterval"/> を共通設定として使い、
///        インターバルごとに以下を一括実行する。
/// </para>
/// <list type="number">
///   <item>Phase A: 全登録 <see cref="RepositoryNode"/> のリモート到達性スキャン
///                  （<c>git ls-remote</c>、同時実行 4 まで）</item>
///   <item>Phase B: 現在開いている全 <see cref="Repository"/> タブの自動フェッチ</item>
/// </list>
/// 起動は <see cref="App.TryLaunchAsNormal"/>、停止は <see cref="App.Quit"/> から。
/// 本クラスは <see cref="Preferences"/> / <see cref="RepositoryNode"/> / <see cref="Repository"/> を
/// 直接オーケストレーションするため ViewModels 層に配置する（Models 層から ViewModels への逆依存を避ける）。
/// </summary>
public class AutoFetchService
{
    /// <summary>
    /// バックグラウンドループのウェイク間隔。
    /// TriggerNow の応答遅延上限でもある（設定のインターバル未到達時は即座にスピンせずこの周期で目覚める）。
    /// </summary>
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);

    /// <summary>シングルトンインスタンス。</summary>
    public static AutoFetchService Instance => s_instance ??= new AutoFetchService();

    private static AutoFetchService s_instance;

    /// <summary>
    /// バックグラウンド処理が 1 サイクル分進行中かどうか。
    /// /rere 10 人分隊 P1#26 (B2-I5): TOCTOU 回避のため int + Interlocked.CompareExchange ベースに変更。
    /// </summary>
    public bool IsRunning => Volatile.Read(ref _isRunningInt) != 0;

    /// <summary>
    /// バックグラウンドループを開始する。再呼び出しは無視される（多重起動防止）。
    /// 複数スレッドからの同時 Start でもループが 1 本しか走らないよう
    /// <see cref="Interlocked.CompareExchange{T}(ref T, T, T)"/> で CTS を atomic に入れ替える。
    /// </summary>
    public void Start()
    {
        var freshCts = new CancellationTokenSource();
        // TOCTOU 対策: _cts が null の場合だけ freshCts を書き込み、既に他スレッドが入れたら負ける
        if (Interlocked.CompareExchange(ref _cts, freshCts, null) is not null)
        {
            freshCts.Dispose();
            return;
        }

        var token = freshCts.Token;

        Task.Run(async () =>
        {
            // 起動直後に 1 回目が走るように _lastRun は既定の MinValue のまま
            while (!token.IsCancellationRequested)
            {
                // 30 秒待機 or 手動トリガ（TriggerNow）で即座にワイルカップ
                var trigger = new TaskCompletionSource();
                lock (_syncLock)
                {
                    _forceTrigger = trigger;
                }

                using (token.Register(() => trigger.TrySetResult()))
                {
                    await Task.WhenAny(Task.Delay(TickInterval), trigger.Task).ConfigureAwait(false);
                }

                if (token.IsCancellationRequested)
                    break;

                try
                {
                    await TickAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // キャンセルは正常経路、ログ不要
                }
                catch (Exception ex)
                {
                    // /rere 10 人分隊 P0#4 (F-CRIT-1): 個別ティック失敗をログに残す。
                    // シナリオ D 「auto-fetch が遅い/効かない」の切り分け起点として必須。
                    Models.Logger.LogException("AutoFetch tick failed", ex);
                }
            }
        });
    }

    /// <summary>
    /// バックグラウンドループを停止する。アプリ終了時に 1 回だけ呼び出す。
    /// <see cref="Interlocked.Exchange{T}(ref T, T)"/> で CTS を atomic に剥がして
    /// 二重 Dispose を防ぐ。
    /// </summary>
    public void Stop()
    {
        var cts = Interlocked.Exchange(ref _cts, null);
        if (cts is null)
            return;

        try
        {
            cts.Cancel();
        }
        finally
        {
            cts.Dispose();
        }
    }

    /// <summary>
    /// 次のティックを待たずに即座に 1 サイクルを走らせる。手動ボタンからの「強制再スキャン」に使う。
    /// サイクル実行中に呼ばれた場合は、実行終了直後に追加でもう 1 サイクル走るように
    /// <see cref="_pendingTrigger"/> フラグを立てる（ユーザーの「今すぐ」期待に沿う）。
    /// </summary>
    public void TriggerNow()
    {
        // 次回インターバルチェックを回避するため最終実行時刻をリセットする
        _lastRun = DateTime.MinValue;

        // 実行中なら終了後に再実行を予約、非実行中ならウェイトを解除して即ティック。
        // _pendingTrigger は volatile 宣言済みのため、直接代入で acquire/release セマンティクスが保証される。
        _pendingTrigger = true;

        lock (_syncLock)
        {
            _forceTrigger?.TrySetResult();
        }
    }

    /// <summary>
    /// 1 サイクル分の処理を実行する。設定無効・インターバル未到達・既に実行中のときは早期リターン。
    /// 手動トリガ（<see cref="TriggerNow"/>）は <see cref="Preferences.EnableAutoFetch"/> を無視して
    /// Phase A（到達性スキャン）のみ強制実行する。Phase B（fetch）は EnableAutoFetch=true のときだけ走る。
    /// 「フェッチは嫌だがバッジは更新したい」という要求に応えるための設計。
    /// </summary>
    private async Task TickAsync(CancellationToken token)
    {
        // /rere 10 人分隊 P1#26 (B2-I5): TOCTOU 回避のため atomic CompareExchange でロックを取得。
        // 旧: `if (_isRunning) return; ... _isRunning = true;` で 2 つのスレッドが同時に通過する race があった。
        if (Interlocked.CompareExchange(ref _isRunningInt, 1, 0) != 0)
            return;

        var enabled = Preferences.Instance.EnableAutoFetch;
        var forced = _pendingTrigger;

        if (!enabled && !forced)
        {
            // 自動フェッチ無効 + 手動トリガ無 → 何もしない
            _lastRun = DateTime.MinValue;
            return;
        }

        // インターバル計算（設定値は 1〜60 分、0 以下は 1 分にクランプ）
        var intervalMinutes = Math.Max(1, Preferences.Instance.AutoFetchInterval);
        var interval = TimeSpan.FromMinutes(intervalMinutes);

        // 手動トリガが立っていればインターバルチェックを迂回する
        if (!forced && _lastRun != DateTime.MinValue && DateTime.Now - _lastRun < interval)
            return;

        // このサイクルが手動トリガ分を消費する
        _pendingTrigger = false;

        // CompareExchange で既にロック取得済み (上の冒頭ガード参照)
        try
        {
            // オフライン判定: ネットワーク未接続なら前回値を保持したままスキャンをスキップする
            // （全リモートがタイムアウトで赤バッジになる「機内モード誤判定」を防ぐ）
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                // _lastRun は進めない（回復後の次ティックで即実行させる）
                return;
            }

            // Phase A: 全登録リポの到達性スキャン（手動トリガ時も自動フェッチ有効時も走らせる）
            await ScanReachabilityAsync(token).ConfigureAwait(false);

            // Phase A 完了時点のスキャン結果を preference.json に永続化する。
            // /rere 10 人分隊 P0#9 (C2-S1-1): UI スレッドで同期 I/O (Serialize + File.Copy + File.Move) を
            // 走らせると、Phase A 完了直後にユーザーが UI 操作する瞬間に 5-30ms (NVMe) / 100-500ms (HDD/AV) の
            // フリーズが発生する。Task.Run でワーカースレッドに完全に外す。
            if (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Run(() => Preferences.Instance.Save(), token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // キャンセル経路は正常
                }
                catch (Exception ex)
                {
                    // /rere 10 人分隊 P0#4: 保存失敗もログに残す (今までは握り潰し)
                    Models.Logger.LogException("AutoFetch Phase A 後の Preferences.Save 失敗", ex);
                }
            }

            if (token.IsCancellationRequested)
                return;

            // Phase B: 開いているリポのフェッチ
            // 条件: EnableAutoFetch=true **かつ** 手動トリガではないとき（定期サイクルのみ）
            // 手動トリガ（ツールバー「リモート到達性を確認」ボタン）は read-only な操作として
            // fetch を発生させない（ユーザーはバッジ更新だけを期待しているため）。
            if (enabled && !forced)
                await FetchOpenReposAsync(token).ConfigureAwait(false);

            _lastRun = DateTime.Now;
        }
        finally
        {
            Interlocked.Exchange(ref _isRunningInt, 0);
        }
    }

    /// <summary>
    /// 全登録 RepositoryNode に対してリモート到達性スキャンを並列実行する（同時 4 件まで）。
    /// </summary>
    private static async Task ScanReachabilityAsync(CancellationToken token)
    {
        // RepositoryNodes ツリーは UI スレッド側で Add/Move/Delete が走るため、
        // ワーカースレッドから直接走査すると InvalidOperationException のリスクがある。
        // Dispatcher 上でフラット化スナップショットを取得してからバックグラウンド実行に渡す。
        List<RepositoryNode> repoNodes = await Dispatcher.UIThread.InvokeAsync(() =>
        {
            List<RepositoryNode> sink = [];
            CollectRepoNodes(Preferences.Instance.RepositoryNodes, sink);
            return sink;
        });

        if (repoNodes.Count == 0)
            return;

        using var gate = new SemaphoreSlim(4);
        var tasks = new List<Task>(repoNodes.Count);
        foreach (var node in repoNodes)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await gate.WaitAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                try
                {
                    await node.CheckRemotesReachabilityAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // キャンセルは正常経路
                }
                catch (Exception ex)
                {
                    // /rere 10 人分隊 P0#4: 個別ノード失敗をログに残す (node.Name で特定可能化)
                    Models.Logger.LogException($"AutoFetch reachability check failed: {node.Name}", ex);
                }
                finally
                {
                    gate.Release();
                }
            }, token));
        }

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // キャンセル経路は正常
        }
        catch (Exception ex)
        {
            // /rere 10 人分隊 P0#4: 個別例外は WhenAll 内の Task.Run catch でログ済みだが、
            // WhenAll 自体の集約例外 (例: 全タスクの cancellation) も記録する
            Models.Logger.LogException("AutoFetch reachability scan aggregate failed", ex);
        }
    }

    /// <summary>
    /// 現在 Launcher.Pages で開かれている Repository タブそれぞれに対して自動フェッチを実行する。
    /// </summary>
    private static async Task FetchOpenReposAsync(CancellationToken token)
    {
        var launcher = App.GetLauncher();
        if (launcher is null)
            return;

        // Launcher.Pages は AvaloniaList（UI スレッド専用）なので UI スレッドでスナップショットを取る
        List<Repository> repos = [];
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var page in launcher.Pages)
            {
                if (page.Data is Repository r)
                    repos.Add(r);
            }
        });

        // 複数タブのフェッチを並列実行する。git fetch は I/O バウンドなので、
        // 逐次実行だと N タブで N×フェッチ時間の待機になるが、Task.WhenAll なら最長フェッチ時間のみ。
        // RunAutoFetchAsync は UI スレッド前提なので個別に Dispatcher.InvokeAsync を通す。
        var tasks = new List<Task>(repos.Count);
        foreach (var repo in repos)
        {
            if (token.IsCancellationRequested)
                break;
            tasks.Add(SafeRunAutoFetchAsync(repo));
        }

        if (tasks.Count > 0)
            await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// 個別リポジトリの自動フェッチを例外安全にラップする。失敗は記録するがループ全体は止めない。
    /// /rere 10 人分隊 P0#4 (F-CRIT-1): リポジトリ単位の失敗もログに残す。
    /// </summary>
    private static async Task SafeRunAutoFetchAsync(Repository repo)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(repo.RunAutoFetchAsync);
        }
        catch (OperationCanceledException)
        {
            // キャンセル経路は正常
        }
        catch (Exception ex)
        {
            Models.Logger.LogException($"AutoFetch failed: {repo.FullPath}", ex);
        }
    }

    /// <summary>
    /// ノード階層を再帰的に走査し、リポジトリノードのみをフラットリストに集める。
    /// </summary>
    private static void CollectRepoNodes(List<RepositoryNode> source, List<RepositoryNode> sink)
    {
        foreach (var node in source)
        {
            if (node.IsRepository)
                sink.Add(node);
            else if (node.SubNodes.Count > 0)
                CollectRepoNodes(node.SubNodes, sink);
        }
    }

    private CancellationTokenSource _cts;
    private DateTime _lastRun = DateTime.MinValue;
    private readonly Lock _syncLock = new();
    private TaskCompletionSource _forceTrigger;
    // /rere 10 人分隊 P1#26 (B2-I5): TOCTOU 回避のため int (0=idle, 1=running) + Interlocked.CompareExchange に変更。
    // 旧 `volatile bool _isRunning` だと Test-Then-Set が 2 命令に分割されて race の余地があった。
    private int _isRunningInt;
    // volatile: TriggerNow() は UI スレッドから呼ばれ、TickAsync() はバックグラウンドループで読む非対称構造。
    // Volatile.Read/Write で保護しているが、将来の保守者が直接アクセスに変えてもサイレントバグ化しないよう宣言自体も volatile にする。
    private volatile bool _pendingTrigger;
}
