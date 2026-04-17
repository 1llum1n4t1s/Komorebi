using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>
/// リポジトリ設定ダイアログのViewModel。
/// ユーザー情報、GPG署名、プロキシ、課題トラッカー、カスタムアクション等を管理する。
/// </summary>
public class RepositoryConfigure : ObservableObject
{
    /// <summary>git config user.name の値。</summary>
    public string UserName
    {
        get;
        set;
    }

    /// <summary>git config user.email の値。</summary>
    public string UserEmail
    {
        get;
        set;
    }

    /// <summary>リポジトリのリモート名一覧。</summary>
    public List<string> Remotes
    {
        get;
    }

    /// <summary>リポジトリのリモートオブジェクト一覧（リモート設定セクション用）。</summary>
    public List<Models.Remote> RemoteObjects
    {
        get;
    }

    /// <summary>選択中のリモートのModelsオブジェクト。</summary>
    public Models.Remote SelectedRemote
    {
        get => _selectedRemote;
        set
        {
            var old = _selectedRemote;
            // 切替前にUI上の編集値をキャプチャ（SetProperty後はUI値が上書きされるため）
            var pendingName = _selectedRemoteName;
            var pendingUrl = _selectedRemoteUrl;
            var pendingUseSSH = _selectedRemoteUseSSH;
            var pendingSSHKey = _selectedRemoteSSHKey;
            var pendingPushDisabled = _selectedRemotePushDisabled;

            if (SetProperty(ref _selectedRemote, value))
            {
                // 前のリモートの編集内容をキャプチャした値で保存（タスクを連鎖して順序を保証）
                if (old is not null)
                    _pendingSaveTask = ChainSaveAsync(old, pendingName, pendingUrl, pendingUseSSH, pendingSSHKey, pendingPushDisabled);
                if (value is not null)
                    _loadTask = LoadRemoteSettingsAsync(value);
            }
        }
    }

    /// <summary>選択中リモートの名前。</summary>
    public string SelectedRemoteName
    {
        get => _selectedRemoteName;
        set => SetProperty(ref _selectedRemoteName, value);
    }

    /// <summary>選択中リモートのURL。</summary>
    public string SelectedRemoteUrl
    {
        get => _selectedRemoteUrl;
        set
        {
            if (SetProperty(ref _selectedRemoteUrl, value))
                SelectedRemoteUseSSH = Models.Remote.IsSSH(value);
        }
    }

    /// <summary>選択中リモートがSSH接続かどうか。</summary>
    public bool SelectedRemoteUseSSH
    {
        get => _selectedRemoteUseSSH;
        set => SetProperty(ref _selectedRemoteUseSSH, value);
    }

    /// <summary>選択中リモートのSSHキーパス。</summary>
    public string SelectedRemoteSSHKey
    {
        get => _selectedRemoteSSHKey;
        set => SetProperty(ref _selectedRemoteSSHKey, value);
    }

    /// <summary>選択中リモートのプッシュが禁止されているかどうか。</summary>
    /// <remarks>
    /// git remote set-url --push &lt;name&gt; no_push で push URL を無効値に設定することで実現する。
    /// push URL が fetch URL と一致しない場合にプッシュ禁止とみなす。
    /// </remarks>
    public bool SelectedRemotePushDisabled
    {
        get => _selectedRemotePushDisabled;
        set => SetProperty(ref _selectedRemotePushDisabled, value);
    }

    /// <summary>デフォルトのリモート名（Push/Pull時に使用）。</summary>
    public string DefaultRemote
    {
        get => _repo.Settings.DefaultRemote;
        set
        {
            if (_repo.Settings.DefaultRemote != value)
            {
                _repo.Settings.DefaultRemote = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>優先マージモード（Fast-Forward/No-FF/Squash等）。</summary>
    public int PreferredMergeMode
    {
        get => _repo.Settings.PreferredMergeMode;
        set
        {
            if (_repo.Settings.PreferredMergeMode != value)
            {
                _repo.Settings.PreferredMergeMode = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>コミット時のGPG署名を有効にするかどうか。</summary>
    public bool GPGCommitSigningEnabled
    {
        get;
        set;
    }

    /// <summary>タグ作成時のGPG署名を有効にするかどうか。</summary>
    public bool GPGTagSigningEnabled
    {
        get;
        set;
    }

    /// <summary>GPG署名に使用するユーザーキーID。</summary>
    public string GPGUserSigningKey
    {
        get;
        set;
    }

    /// <summary>HTTPプロキシ設定（git config http.proxy）。</summary>
    public string HttpProxy
    {
        get => _httpProxy;
        set => SetProperty(ref _httpProxy, value);
    }

    /// <summary>Conventional Commitのタイプ定義のオーバーライド。</summary>
    public string ConventionalTypesOverride
    {
        get => _repo.Settings.ConventionalTypesOverride;
        set
        {
            if (_repo.Settings.ConventionalTypesOverride != value)
            {
                _repo.Settings.ConventionalTypesOverride = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>フェッチ時にリモートの削除済みブランチを自動プルーンするかどうか。</summary>
    public bool EnablePruneOnFetch
    {
        get;
        set;
    }

    /// <summary>サブモジュール自動更新前に確認ダイアログを表示するかどうか。</summary>
    public bool AskBeforeAutoUpdatingSubmodules
    {
        get => _repo.Settings.AskBeforeAutoUpdatingSubmodules;
        set => _repo.Settings.AskBeforeAutoUpdatingSubmodules = value;
    }

    /// <summary>コミットメッセージテンプレートの一覧。</summary>
    public AvaloniaList<Models.CommitTemplate> CommitTemplates
    {
        get => _repo.Settings.CommitTemplates;
    }

    /// <summary>現在選択中のコミットテンプレート。</summary>
    public Models.CommitTemplate SelectedCommitTemplate
    {
        get => _selectedCommitTemplate;
        set => SetProperty(ref _selectedCommitTemplate, value);
    }

    /// <summary>課題トラッカー（Issue Tracker）のルール一覧。</summary>
    public AvaloniaList<Models.IssueTracker> IssueTrackers
    {
        get;
    } = [];

    /// <summary>現在選択中の課題トラッカールール。</summary>
    public Models.IssueTracker SelectedIssueTracker
    {
        get => _selectedIssueTracker;
        set => SetProperty(ref _selectedIssueTracker, value);
    }

    /// <summary>利用可能なOpenAIサービス名の一覧（「---」を含む）。</summary>
    public List<string> AvailableOpenAIServices
    {
        get;
        private set;
    }

    /// <summary>このリポジトリで優先的に使用するOpenAIサービス名。</summary>
    public string PreferredOpenAIService
    {
        get => _repo.Settings.PreferredOpenAIService;
        set => _repo.Settings.PreferredOpenAIService = value;
    }

    /// <summary>カスタムアクションの一覧。</summary>
    public AvaloniaList<Models.CustomAction> CustomActions
    {
        get => _repo.Settings.CustomActions;
    }

    /// <summary>現在選択中のカスタムアクション。</summary>
    public Models.CustomAction SelectedCustomAction
    {
        get => _selectedCustomAction;
        set => SetProperty(ref _selectedCustomAction, value);
    }

    /// <summary>
    /// コンストラクタ。git configから現在の設定値を読み込み、
    /// リモート一覧・OpenAIサービス一覧・課題トラッカーを初期化する。
    /// </summary>
    public RepositoryConfigure(Repository repo)
    {
        _repo = repo;

        // リモート名一覧を構築
        Remotes = [];
        foreach (var remote in _repo.Remotes)
            Remotes.Add(remote.Name);

        // リモートオブジェクト一覧を構築（リモート設定セクション用）
        RemoteObjects = new List<Models.Remote>(_repo.Remotes);

        // OpenAIサービス一覧を構築（「---」= 未選択）
        AvailableOpenAIServices = ["---"];
        foreach (var service in Preferences.Instance.OpenAIServices)
            AvailableOpenAIServices.Add(service.Name);

        if (!AvailableOpenAIServices.Contains(PreferredOpenAIService))
            PreferredOpenAIService = "---";

        // git configの値を一括読み込みして各プロパティに反映
        _cached = new Commands.Config(repo.FullPath).ReadAll();
        if (_cached.TryGetValue("user.name", out var name))
            UserName = name;
        if (_cached.TryGetValue("user.email", out var email))
            UserEmail = email;
        if (_cached.TryGetValue("commit.gpgsign", out var gpgCommitSign))
            GPGCommitSigningEnabled = gpgCommitSign == "true";
        if (_cached.TryGetValue("tag.gpgsign", out var gpgTagSign))
            GPGTagSigningEnabled = gpgTagSign == "true";
        if (_cached.TryGetValue("user.signingkey", out var signingKey))
            GPGUserSigningKey = signingKey;
        if (_cached.TryGetValue("http.proxy", out var proxy))
            HttpProxy = proxy;
        if (_cached.TryGetValue("fetch.prune", out var prune))
            EnablePruneOnFetch = (prune == "true");

        // 課題トラッカーのルールをコピーして編集用リストに追加
        foreach (var rule in _repo.IssueTrackers)
        {
            IssueTrackers.Add(new()
            {
                IsShared = rule.IsShared,
                Name = rule.Name,
                RegexString = rule.RegexString,
                URLTemplate = rule.URLTemplate,
            });
        }

        // 既定のリモートが未設定でリモートが存在する場合は "origin"（なければ先頭）を自動選択する
        if (string.IsNullOrEmpty(DefaultRemote) && Remotes.Count > 0)
            DefaultRemote = Remotes.Contains("origin") ? "origin" : Remotes[0];

        // 最初のリモートを自動選択
        if (RemoteObjects.Count > 0)
            SelectedRemote = RemoteObjects[0];
    }

    /// <summary>HTTPプロキシ設定をクリアする。</summary>
    public void ClearHttpProxy()
    {
        HttpProxy = string.Empty;
    }

    /// <summary>新しいコミットテンプレートを追加し、選択状態にする。</summary>
    public void AddCommitTemplate()
    {
        var template = new Models.CommitTemplate() { Name = "New Template" };
        _repo.Settings.CommitTemplates.Add(template);
        SelectedCommitTemplate = template;
    }

    /// <summary>選択中のコミットテンプレートを削除する。</summary>
    public void RemoveSelectedCommitTemplate()
    {
        if (_selectedCommitTemplate is not null)
            _repo.Settings.CommitTemplates.Remove(_selectedCommitTemplate);
        SelectedCommitTemplate = null;
    }

    /// <summary>リモートのWeb閲覧用URLの一覧を取得する。</summary>
    public List<string> GetRemoteVisitUrls()
    {
        List<string> outs = [];
        foreach (var remote in _repo.Remotes)
        {
            if (remote.TryGetVisitURL(out var url))
                outs.Add(url);
        }
        return outs;
    }

    /// <summary>新しい課題トラッカールールを追加し、選択状態にする。</summary>
    public void AddIssueTracker(string name, string regex, string url)
    {
        var rule = new Models.IssueTracker()
        {
            IsShared = false,
            Name = name,
            RegexString = regex,
            URLTemplate = url,
        };

        IssueTrackers.Add(rule);
        SelectedIssueTracker = rule;
    }

    /// <summary>選択中の課題トラッカールールを削除する。</summary>
    public void RemoveIssueTracker()
    {
        if (_selectedIssueTracker is { } rule)
            IssueTrackers.Remove(rule);

        SelectedIssueTracker = null;
    }

    /// <summary>新しいカスタムアクションを追加し、選択状態にする。</summary>
    public void AddNewCustomAction()
    {
        SelectedCustomAction = _repo.Settings.AddNewCustomAction();
    }

    /// <summary>選択中のカスタムアクションを削除する。</summary>
    public void RemoveSelectedCustomAction()
    {
        _repo.Settings.RemoveCustomAction(_selectedCustomAction);
        SelectedCustomAction = null;
    }

    /// <summary>選択中のカスタムアクションを一つ上に移動する。</summary>
    public void MoveSelectedCustomActionUp()
    {
        if (_selectedCustomAction is not null)
            _repo.Settings.MoveCustomActionUp(_selectedCustomAction);
    }

    /// <summary>選択中のカスタムアクションを一つ下に移動する。</summary>
    public void MoveSelectedCustomActionDown()
    {
        if (_selectedCustomAction is not null)
            _repo.Settings.MoveCustomActionDown(_selectedCustomAction);
    }

    /// <summary>
    /// 設定をgit configとリポジトリ設定ファイルに保存する。
    /// 変更があったキーのみを書き込む。
    /// </summary>
    /// <summary>保存中にエラーが発生したかどうか。</summary>
    public bool HasSaveError { get; set; }

    public async Task SaveAsync()
    {
        HasSaveError = false;

        try
        {
            await SetIfChangedAsync("user.name", UserName, "");
            await SetIfChangedAsync("user.email", UserEmail, "");
            await SetIfChangedAsync("commit.gpgsign", GPGCommitSigningEnabled ? "true" : "false", "false");
            await SetIfChangedAsync("tag.gpgsign", GPGTagSigningEnabled ? "true" : "false", "false");
            await SetIfChangedAsync("user.signingkey", GPGUserSigningKey, "");
            await SetIfChangedAsync("http.proxy", HttpProxy, "");
            await SetIfChangedAsync("fetch.prune", EnablePruneOnFetch ? "true" : "false", "false");

            // リモート設定の読み込み・保存の完了を待ってから現在のリモートを保存
            await _pendingSaveTask;
            await _loadTask;

            // リモート設定の保存
            if (_selectedRemote is not null)
                await SaveRemoteSettingsAsync();

            await ApplyIssueTrackerChangesAsync();
            await _repo.Settings.SaveAsync();
        }
        catch (Exception ex)
        {
            HasSaveError = true;
            App.RaiseException(_repo.FullPath, $"Failed to save settings: {ex.Message}");
        }
    }

    /// <summary>git configの値が変更されている場合のみ書き込む。</summary>
    private async Task SetIfChangedAsync(string key, string value, string defValue)
    {
        if (value != _cached.GetValueOrDefault(key, defValue))
            await new Commands.Config(_repo.FullPath).SetAsync(key, value);
    }

    /// <summary>選択されたリモートの設定を読み込む。</summary>
    private async Task LoadRemoteSettingsAsync(Models.Remote remote)
    {
        SelectedRemoteName = remote.Name;
        SelectedRemoteUrl = remote.URL;
        SelectedRemoteUseSSH = Models.Remote.IsSSH(remote.URL);
        SelectedRemoteSSHKey = string.Empty;
        SelectedRemotePushDisabled = false;

        try
        {
            var remoteCmd = new Commands.Remote(_repo.FullPath);

            // プッシュURLを取得し、フェッチURLと異なればプッシュ禁止と判定する
            var pushUrl = await remoteCmd.GetURLAsync(remote.Name, true);
            if (!ReferenceEquals(_selectedRemote, remote))
                return;

            SelectedRemotePushDisabled = !string.IsNullOrEmpty(pushUrl) && pushUrl != remote.URL;

            if (SelectedRemoteUseSSH)
            {
                var key = await new Commands.Config(_repo.FullPath)
                    .GetAsync($"remote.{remote.Name}.sshkey");

                // await中にリモート選択が変わっていたら結果を破棄する（競合状態防止）
                if (!ReferenceEquals(_selectedRemote, remote))
                    return;

                SelectedRemoteSSHKey = key;
            }
        }
        catch (Exception ex)
        {
            App.RaiseException(_repo.FullPath, $"Failed to load remote settings: {ex.Message}");
        }
    }

    /// <summary>現在選択中のリモートの設定をUI上の値でgit configに保存する。</summary>
    private Task SaveRemoteSettingsAsync()
    {
        if (_selectedRemote is null)
            return Task.CompletedTask;

        return SavePendingRemoteSettingsAsync(
            _selectedRemote, _selectedRemoteName, _selectedRemoteUrl,
            _selectedRemoteUseSSH, _selectedRemoteSSHKey, _selectedRemotePushDisabled);
    }

    /// <summary>前のリモート保存タスクの完了を待ってから次の保存を実行する。</summary>
    private async Task ChainSaveAsync(
        Models.Remote remote, string name, string url, bool useSSH, string sshKey, bool pushDisabled)
    {
        // ConfigureAwait(false) は使わない — SavePendingRemoteSettingsAsync 内で
        // Remotes リストや OnPropertyChanged 等の UI バインドプロパティを更新するため
        await _pendingSaveTask;
        await SavePendingRemoteSettingsAsync(remote, name, url, useSSH, sshKey, pushDisabled);
    }

    /// <summary>指定リモートの設定をキャプチャ済みの値でgit configに保存する。</summary>
    private async Task SavePendingRemoteSettingsAsync(
        Models.Remote remote, string name, string url, bool useSSH, string sshKey, bool pushDisabled)
    {
        var remoteName = remote.Name;
        var remoteCmd = new Commands.Remote(_repo.FullPath);

        // リモート名の変更
        if (name != remoteName)
        {
            if (await remoteCmd.RenameAsync(remoteName, name))
            {
                // DefaultRemote用の名前リストを再構築してComboBoxに通知する
                var idx = Remotes.IndexOf(remoteName);
                if (idx >= 0)
                {
                    Remotes[idx] = name;
                    OnPropertyChanged(nameof(Remotes));
                }

                if (DefaultRemote == remoteName)
                    DefaultRemote = name;

                remote.Name = name;
                remoteName = name;
            }
            else
            {
                // リネーム失敗時はエラーを通知し、URL/SSHキーの更新も中止する
                HasSaveError = true;
                App.RaiseException(_repo.FullPath,
                    $"Failed to rename remote '{remoteName}' to '{name}'");
                return;
            }
        }

        // URL変更
        if (url != remote.URL)
        {
            var succ = await remoteCmd.SetURLAsync(remoteName, url, false);
            if (succ)
            {
                remote.URL = url;

                // プッシュ禁止中でなければ Push URL を fetch URL に同期する
                if (!pushDisabled)
                {
                    var pushUrl = await remoteCmd.GetURLAsync(remoteName, true);
                    if (!string.IsNullOrEmpty(pushUrl) && pushUrl != url)
                        await remoteCmd.SetURLAsync(remoteName, url, true);
                }
            }
            else
            {
                // URL変更失敗時はエラーを通知し、SSHキーの更新も中止する
                HasSaveError = true;
                App.RaiseException(_repo.FullPath,
                    $"Failed to set URL for remote '{remoteName}'");
                return;
            }
        }

        // プッシュ禁止設定
        var currentPushUrl = await remoteCmd.GetURLAsync(remoteName, true);
        var isPushCurrentlyDisabled = !string.IsNullOrEmpty(currentPushUrl) && currentPushUrl != (url ?? remote.URL);
        if (pushDisabled != isPushCurrentlyDisabled)
        {
            if (pushDisabled)
            {
                // push URL を無効値に設定してプッシュを禁止する
                await remoteCmd.SetURLAsync(remoteName, "no_push", true);
            }
            else
            {
                // push URL を削除して fetch URL に戻す
                await remoteCmd.DeletePushURLAsync(remoteName, currentPushUrl);
            }
        }

        var config = new Commands.Config(_repo.FullPath);

        // SSHキー設定
        await config.SetAsync(
            $"remote.{remoteName}.sshkey",
            useSSH ? sshKey : null);
    }

    /// <summary>
    /// 課題トラッカーの変更差分を計算し、追加・更新・削除をgit configに適用する。
    /// 共有/ローカルの切り替えも処理する。
    /// </summary>
    private async Task ApplyIssueTrackerChangesAsync()
    {
        var changed = false;
        Dictionary<string, Models.IssueTracker> oldRules = [];
        foreach (var rule in _repo.IssueTrackers)
            oldRules.Add(rule.Name, rule);

        foreach (var rule in IssueTrackers)
        {
            if (oldRules.TryGetValue(rule.Name, out var old))
            {
                // 共有/ローカルが変更された場合は削除→再追加
                if (old.IsShared != rule.IsShared)
                {
                    changed = true;
                    await new Commands.IssueTracker(_repo.FullPath, old.IsShared).RemoveAsync(old.Name);
                    await new Commands.IssueTracker(_repo.FullPath, rule.IsShared).AddAsync(rule);
                }
                else
                {
                    // 正規表現パターンの変更チェック
                    if (!old.RegexString.Equals(rule.RegexString, StringComparison.Ordinal))
                    {
                        changed = true;
                        await new Commands.IssueTracker(_repo.FullPath, old.IsShared).UpdateRegexAsync(rule);
                    }

                    // URLテンプレートの変更チェック
                    if (!old.URLTemplate.Equals(rule.URLTemplate, StringComparison.Ordinal))
                    {
                        changed = true;
                        await new Commands.IssueTracker(_repo.FullPath, old.IsShared).UpdateURLTemplateAsync(rule);
                    }
                }

                oldRules.Remove(rule.Name);
            }
            else
            {
                // 新規追加
                changed = true;
                await new Commands.IssueTracker(_repo.FullPath, rule.IsShared).AddAsync(rule);
            }
        }

        // 残ったルールは削除されたもの
        if (oldRules.Count > 0)
        {
            changed = true;

            foreach (var kv in oldRules)
                await new Commands.IssueTracker(_repo.FullPath, kv.Value.IsShared).RemoveAsync(kv.Key);
        }

        // 変更があった場合はリポジトリの課題トラッカーリストを更新
        if (changed)
        {
            _repo.IssueTrackers.Clear();
            _repo.IssueTrackers.AddRange(IssueTrackers);
        }
    }

    private Task _pendingSaveTask = Task.CompletedTask;                       // 保留中のリモート設定保存タスク
    private Task _loadTask = Task.CompletedTask;                             // 進行中のリモート設定読み込みタスク
    private readonly Repository _repo;                                       // 対象リポジトリ
    private readonly Dictionary<string, string> _cached;                     // git configの初期値キャッシュ
    private string _httpProxy;                                               // HTTPプロキシ設定
    private Models.CommitTemplate _selectedCommitTemplate = null;            // 選択中のコミットテンプレート
    private Models.IssueTracker _selectedIssueTracker = null;                // 選択中の課題トラッカー
    private Models.CustomAction _selectedCustomAction = null;                // 選択中のカスタムアクション
    private Models.Remote _selectedRemote;                                   // 選択中のリモート
    private string _selectedRemoteName = string.Empty;                      // 選択中リモートの名前
    private string _selectedRemoteUrl = string.Empty;                        // 選択中リモートのURL
    private bool _selectedRemoteUseSSH;                                      // 選択中リモートがSSH接続か
    private string _selectedRemoteSSHKey = string.Empty;                     // 選択中リモートのSSHキーパス
    private bool _selectedRemotePushDisabled;                                // 選択中リモートのプッシュ禁止状態
}
