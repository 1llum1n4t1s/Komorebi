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

    /// <summary>バックグラウンド処理が 1 サイクル分進行中かどうか。</summary>
    public bool IsRunning => _isRunning;

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
                catch
                {
                    // 個々のティック失敗はループ全体を止めない
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

        // 実行中なら終了後に再実行を予約、非実行中ならウェイトを解除して即ティック
        Volatile.Write(ref _pendingTrigger, true);

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
        if (_isRunning)
            return;

        var enabled = Preferences.Instance.EnableAutoFetch;
        var forced = Volatile.Read(ref _pendingTrigger);

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
        Volatile.Write(ref _pendingTrigger, false);

        _isRunning = true;
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
            if (!token.IsCancellationRequested)
            {
                try
                {
                    await Dispatcher.UIThread.InvokeAsync(() => Preferences.Instance.Save());
                }
                catch
                {
                    // 保存失敗はループ全体を止めない
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
            _isRunning = false;
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
                catch
                {
                    // 個別ノード失敗は全体を止めない
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
        catch
        {
            // 個別例外は上位で握りつぶす
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

        foreach (var repo in repos)
        {
            if (token.IsCancellationRequested)
                break;

            try
            {
                // Repository の自動フェッチ実装は UI スレッド前提（_uiStates / _settings を触るため）
                await Dispatcher.UIThread.InvokeAsync(repo.RunAutoFetchAsync);
            }
            catch
            {
                // 個別リポの失敗は全体を止めない
            }
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
    // volatile: 外部スレッド（UI 側）からも IsRunning プロパティ経由で参照されるため可視性保証が必要
    private volatile bool _isRunning;
    private bool _pendingTrigger;
}
