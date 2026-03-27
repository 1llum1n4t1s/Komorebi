using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>
///     リポジトリ設定ダイアログのViewModel。
///     ユーザー情報、GPG署名、プロキシ、課題トラッカー、カスタムアクション等を管理する。
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

    /// <summary>自動フェッチを有効にするかどうか。</summary>
    public bool EnableAutoFetch
    {
        get => _repo.Settings.EnableAutoFetch;
        set => _repo.Settings.EnableAutoFetch = value;
    }

    /// <summary>自動フェッチの間隔（分）。1未満は無効。</summary>
    public int? AutoFetchInterval
    {
        get => _repo.Settings.AutoFetchInterval;
        set
        {
            if (value is null || value < 1)
                return;

            var interval = (int)value;
            if (_repo.Settings.AutoFetchInterval != interval)
                _repo.Settings.AutoFetchInterval = interval;
        }
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
    ///     コンストラクタ。git configから現在の設定値を読み込み、
    ///     リモート一覧・OpenAIサービス一覧・課題トラッカーを初期化する。
    /// </summary>
    public RepositoryConfigure(Repository repo)
    {
        _repo = repo;

        // リモート名一覧を構築
        Remotes = new List<string>();
        foreach (var remote in _repo.Remotes)
            Remotes.Add(remote.Name);

        // OpenAIサービス一覧を構築（「---」= 未選択）
        AvailableOpenAIServices = new List<string>() { "---" };
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
        var outs = new List<string>();
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
    ///     設定をgit configとリポジトリ設定ファイルに保存する。
    ///     変更があったキーのみを書き込む。
    /// </summary>
    public async Task SaveAsync()
    {
        await SetIfChangedAsync("user.name", UserName, "");
        await SetIfChangedAsync("user.email", UserEmail, "");
        await SetIfChangedAsync("commit.gpgsign", GPGCommitSigningEnabled ? "true" : "false", "false");
        await SetIfChangedAsync("tag.gpgsign", GPGTagSigningEnabled ? "true" : "false", "false");
        await SetIfChangedAsync("user.signingkey", GPGUserSigningKey, "");
        await SetIfChangedAsync("http.proxy", HttpProxy, "");
        await SetIfChangedAsync("fetch.prune", EnablePruneOnFetch ? "true" : "false", "false");

        await ApplyIssueTrackerChangesAsync();
        await _repo.Settings.SaveAsync();
    }

    /// <summary>git configの値が変更されている場合のみ書き込む。</summary>
    private async Task SetIfChangedAsync(string key, string value, string defValue)
    {
        if (value != _cached.GetValueOrDefault(key, defValue))
            await new Commands.Config(_repo.FullPath).SetAsync(key, value);
    }

    /// <summary>
    ///     課題トラッカーの変更差分を計算し、追加・更新・削除をgit configに適用する。
    ///     共有/ローカルの切り替えも処理する。
    /// </summary>
    private async Task ApplyIssueTrackerChangesAsync()
    {
        var changed = false;
        var oldRules = new Dictionary<string, Models.IssueTracker>();
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

    private readonly Repository _repo;                                       // 対象リポジトリ
    private readonly Dictionary<string, string> _cached;                     // git configの初期値キャッシュ
    private string _httpProxy;                                               // HTTPプロキシ設定
    private Models.CommitTemplate _selectedCommitTemplate = null;            // 選択中のコミットテンプレート
    private Models.IssueTracker _selectedIssueTracker = null;                // 選択中の課題トラッカー
    private Models.CustomAction _selectedCustomAction = null;                // 選択中のカスタムアクション
}
