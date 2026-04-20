using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>
/// リポジトリツリー内のノード（リポジトリまたはグループフォルダ）を表すViewModel。
/// ウェルカム画面やランチャーのサイドバーに表示されるツリー構造の各項目を管理する。
/// </summary>
public class RepositoryNode : ObservableObject
{
    /// <summary>
    /// リモート 1 件あたりの ls-remote タイムアウト時間。接続・TLS・SSH handshake の合計上限。
    /// </summary>
    private static readonly TimeSpan ReachabilityCheckTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// ノードの一意識別子。リポジトリの場合はフルパス、グループの場合はグループ名。
    /// パス区切り文字は正規化され、末尾のスラッシュは除去される。
    /// </summary>
    public string Id
    {
        get => _id;
        set
        {
            var normalized = value.Replace('\\', '/').TrimEnd('/');
            SetProperty(ref _id, normalized);
        }
    }

    /// <summary>
    /// ノードの表示名。UIに表示されるリポジトリ名またはグループ名。
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>
    /// ブックマークのインデックス。色分け用のブックマーク識別子（0はブックマークなし）。
    /// </summary>
    public int Bookmark
    {
        get => _bookmark;
        set => SetProperty(ref _bookmark, value);
    }

    /// <summary>
    /// このノードがリポジトリかどうか。falseの場合はグループフォルダ。
    /// </summary>
    public bool IsRepository
    {
        get => _isRepository;
        set => SetProperty(ref _isRepository, value);
    }

    /// <summary>
    /// ツリー上でこのノードが展開されているかどうか。
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    /// <summary>
    /// 検索フィルタリング時の表示/非表示状態。JSON保存対象外。
    /// </summary>
    [JsonIgnore]
    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    /// <summary>
    /// リポジトリのパスが存在しない場合にtrueを返す。無効なリポジトリの検出に使用。
    /// </summary>
    [JsonIgnore]
    public bool IsInvalid
    {
        get => _isRepository && !Directory.Exists(_id);
    }

    /// <summary>
    /// ツリー内のネスト深度。UIのインデント表示に使用。JSON保存対象外。
    /// </summary>
    [JsonIgnore]
    public int Depth
    {
        get;
        set;
    } = 0;

    /// <summary>
    /// リポジトリの現在のステータス（未コミットの変更数など）。
    /// </summary>
    public Models.RepositoryStatus Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    /// <summary>
    /// リモートの到達可能性状態。Welcome 画面のバッジ表示に使用する。
    /// 前回スキャン結果を保持して次回起動時の初期表示に使う目的で JSON に保存する。
    /// スキャン中は値を変更せず、完了時に最終結果で直接上書きする（Checking 状態は未使用）。
    /// </summary>
    public Models.RemoteReachability RemoteReachability
    {
        get => _remoteReachability;
        set => SetProperty(ref _remoteReachability, value);
    }

    /// <summary>
    /// 直近のスキャンで到達不可だったリモート名のカンマ区切り文字列。ツールチップ表示用。
    /// プライバシー配慮のため JSON 保存対象外。内部サーバー名などのプライベートな情報を含む可能性があるため
    /// preference.json には残さない（バッジの色は <see cref="RemoteReachability"/> のみで復元する）。
    /// </summary>
    [JsonIgnore]
    public string UnreachableRemotesText
    {
        get => _unreachableRemotesText;
        set => SetProperty(ref _unreachableRemotesText, value);
    }

    /// <summary>
    /// 子ノードのリスト。グループフォルダの場合に子リポジトリやサブグループを保持する。
    /// </summary>
    public List<RepositoryNode> SubNodes
    {
        get;
        set;
    } = [];

    /// <summary>
    /// ノードを開く。リポジトリの場合は新しいタブで開き、グループの場合は全子ノードを再帰的に開く。
    /// </summary>
    public void Open()
    {
        if (IsRepository)
        {
            App.GetLauncher().OpenRepositoryInTab(this, null);
            return;
        }

        foreach (var subNode in SubNodes)
            subNode.Open();
    }

    /// <summary>
    /// ノードの編集ダイアログを表示する。名前やパスの変更が可能。
    /// </summary>
    public void Edit()
    {
        var activePage = App.GetLauncher().ActivePage;
        if (activePage is not null && activePage.CanCreatePopup())
            activePage.Popup = new EditRepositoryNode(this);
    }

    /// <summary>
    /// このノードの下にサブフォルダ（グループ）を作成するダイアログを表示する。
    /// </summary>
    public void AddSubFolder()
    {
        var activePage = App.GetLauncher().ActivePage;
        if (activePage is not null && activePage.CanCreatePopup())
            activePage.Popup = new CreateGroup(this);
    }

    /// <summary>
    /// ノードを別のグループに移動するダイアログを表示する。
    /// </summary>
    public void Move()
    {
        var activePage = App.GetLauncher().ActivePage;
        if (activePage is not null && activePage.CanCreatePopup())
            activePage.Popup = new MoveRepositoryNode(this);
    }

    /// <summary>
    /// リポジトリをOSのファイルマネージャで開く。
    /// </summary>
    public void OpenInFileManager()
    {
        if (!IsRepository)
            return;
        Native.OS.OpenInFileManager(_id);
    }

    /// <summary>
    /// リポジトリのディレクトリでターミナルを開く。
    /// </summary>
    public void OpenTerminal()
    {
        if (!IsRepository)
            return;
        Native.OS.OpenTerminal(_id);
    }

    /// <summary>
    /// ノードの削除確認ダイアログを表示する。
    /// </summary>
    public void Delete()
    {
        var activePage = App.GetLauncher().ActivePage;
        if (activePage is not null && activePage.CanCreatePopup())
            activePage.Popup = new DeleteRepositoryNode(this);
    }

    /// <summary>
    /// リポジトリのステータスを非同期に更新する。
    /// グループノードの場合は全子ノードを再帰的に更新する。
    /// 強制更新でない場合、前回の更新から10秒以内の再更新はスキップする。
    /// </summary>
    /// <param name="force">trueの場合、クールダウン期間を無視して強制更新する</param>
    /// <param name="token">キャンセルトークン</param>
    public async Task UpdateStatusAsync(bool force, CancellationToken? token)
    {
        if (token is { IsCancellationRequested: true })
            return;

        if (!_isRepository)
        {
            Status = null;

            if (SubNodes.Count > 0)
            {
                // 列挙中のコレクション変更を回避するためコピーを作成
                List<RepositoryNode> nodes = [];
                nodes.AddRange(SubNodes);

                foreach (var node in nodes)
                    await node.UpdateStatusAsync(force, token);
            }

            return;
        }

        if (!Directory.Exists(_id))
        {
            _lastUpdateStatus = DateTime.Now;
            Status = null;
            OnPropertyChanged(nameof(IsInvalid));
            return;
        }

        // 強制更新でなければ10秒のクールダウンを適用
        if (!force)
        {
            var passed = DateTime.Now - _lastUpdateStatus;
            if (passed.TotalSeconds < 10.0)
                return;
        }

        _lastUpdateStatus = DateTime.Now;
        Status = await new Commands.QueryRepositoryStatus(_id).GetResultAsync();
        OnPropertyChanged(nameof(IsInvalid));
    }

    /// <summary>
    /// このリポジトリの全リモートに対して到達可能性をチェックする。
    /// リポジトリノード（IsRepository == true）のみ処理し、グループノードは何もしない。
    /// グループ配下のリポジトリ列挙は呼び出し側（AutoFetchService.CollectRepoNodes）でフラット化済みの前提。
    /// この関数内でグループ再帰を行うとセマフォ同時実行制御をバイパスできる「伏線」になるため廃止。
    /// 1 リモートあたり 15 秒のタイムアウトを適用する。
    /// </summary>
    /// <param name="token">キャンセルトークン。null の場合はキャンセルなし。</param>
    public async Task CheckRemotesReachabilityAsync(CancellationToken? token)
    {
        if (token is { IsCancellationRequested: true })
            return;

        // グループノードは対象外（契約: 呼び出し側でフラット化してリポジトリノードだけ渡す）
        if (!_isRepository)
            return;

        // 無効なリポジトリ（パス消失）はスキップして状態をリセット
        if (!Directory.Exists(_id))
        {
            await SetReachabilityOnUIAsync(Models.RemoteReachability.Unknown, string.Empty);
            return;
        }

        // スキャン中は前回値（バッジ）を保ったまま裏で確認する。完了時に最終結果で上書きするため
        // Checking 状態への一時遷移はせず、UI フリッカを避ける。

        // リポジトリのリモート一覧を取得する
        var remotes = await new Commands.QueryRemotes(_id).GetResultAsync();
        if (remotes == null || remotes.Count == 0)
        {
            await SetReachabilityOnUIAsync(Models.RemoteReachability.NoRemotes, string.Empty);
            return;
        }

        // SSH キー設定を 1 回の git config -l で一括取得する
        // （リモート数分の per-remote git config プロセス起動を回避）
        var configs = await new Commands.Config(_id).ReadAllAsync();
        var globalSSHKey = Preferences.Instance?.GlobalSSHKey;

        // リモートごとに 15 秒のタイムアウトで逐次チェックする（同一リポ内は並列化しすぎない）
        int reachable = 0;
        List<string> failed = [];
        foreach (var remote in remotes)
        {
            if (token is { IsCancellationRequested: true })
                return;

            using var cts = token is { } t
                ? CancellationTokenSource.CreateLinkedTokenSource(t)
                : new CancellationTokenSource();
            cts.CancelAfter(ReachabilityCheckTimeout);

            // 事前読み込み済み config から SSH キー値を解決（per-remote の git config 呼び出しを回避）
            configs.TryGetValue($"remote.{remote.Name}.sshkey", out var configValue);
            var sshKey = Commands.Command.ResolveSSHKeyValue(configValue, globalSSHKey);

            bool ok;
            try
            {
                ok = await new Commands.Remote(_id).CheckReachabilityAsync(remote.Name, sshKey, cts.Token);
            }
            catch
            {
                ok = false;
            }

            if (ok)
                reachable++;
            else
                failed.Add(remote.Name);
        }

        // 結果を集計して状態とツールチップ用文字列を更新する
        Models.RemoteReachability finalState;
        if (failed.Count == 0)
            finalState = Models.RemoteReachability.AllReachable;
        else if (reachable == 0)
            finalState = Models.RemoteReachability.AllUnreachable;
        else
            finalState = Models.RemoteReachability.SomeUnreachable;

        await SetReachabilityOnUIAsync(finalState, string.Join(", ", failed));
    }

    /// <summary>
    /// Reachability 関連プロパティの書き込みを UI スレッドにマーシャリングする。
    /// <see cref="AutoFetchService"/> は Task.Run で本メソッドを呼ぶため、SetProperty から発火する
    /// PropertyChanged を UI スレッドで起こさないと Avalonia バインディング側で Thread affinity
    /// 違反のリスクがある。
    /// </summary>
    private async Task SetReachabilityOnUIAsync(Models.RemoteReachability state, string unreachableText)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            RemoteReachability = state;
            UnreachableRemotesText = unreachableText;
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            RemoteReachability = state;
            UnreachableRemotesText = unreachableText;
        });
    }

    private string _id = string.Empty;
    private string _name = string.Empty;
    private bool _isRepository = false;
    private int _bookmark = 0;
    private bool _isExpanded = false;
    private bool _isVisible = true;
    private Models.RepositoryStatus _status = null;
    private Models.RemoteReachability _remoteReachability = Models.RemoteReachability.Unknown;
    private string _unreachableRemotesText = string.Empty;
    private DateTime _lastUpdateStatus = DateTime.UnixEpoch.ToLocalTime();
}
