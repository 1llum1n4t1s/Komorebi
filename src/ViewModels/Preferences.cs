using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>
/// アプリケーション設定のシングルトンViewModel。
/// UI設定、Git設定、外部ツール設定、リポジトリノード管理、ワークスペース管理を担当する。
/// preference.jsonに永続化される。
/// </summary>
public class Preferences : ObservableObject
{
    /// <summary>シングルトンインスタンス。初回アクセス時にファイルから読み込み、各種初期化を行う。</summary>
    [JsonIgnore]
    public static Preferences Instance
    {
        get
        {
            if (_instance is not null)
                return _instance;

            _instance = Load();
            _instance._isLoading = false;

            _instance.PrepareGit();
            _instance.PrepareShellOrTerminal();
            PrepareExternalDiffMergeTool();
            _instance.PrepareWorkspaces();

            return _instance;
        }
    }

    /// <summary>UIロケール（例: "ja_JP", "en_US"）。変更時にアプリ全体のロケールを切り替える。</summary>
    public string Locale
    {
        get => _locale;
        set
        {
            if (SetProperty(ref _locale, value) && !_isLoading)
                App.SetLocale(value);
        }
    }

    /// <summary>カラーテーマ名（例: "Default", "Dark"）。変更時にアプリ全体のテーマを切り替える。</summary>
    public string Theme
    {
        get => _theme;
        set
        {
            if (SetProperty(ref _theme, value) && !_isLoading)
                App.SetTheme(_theme, _themeOverrides);
        }
    }

    /// <summary>テーマのカスタムオーバーライド設定。変更時にテーマを再適用する。</summary>
    public string ThemeOverrides
    {
        get => _themeOverrides;
        set
        {
            if (SetProperty(ref _themeOverrides, value) && !_isLoading)
                App.SetTheme(_theme, value);
        }
    }

    /// <summary>UI全体のデフォルトフォントファミリー。変更時にフォントを再適用する。</summary>
    public string DefaultFontFamily
    {
        get => _defaultFontFamily;
        set
        {
            if (SetProperty(ref _defaultFontFamily, value) && !_isLoading)
                App.SetFonts(value, _monospaceFontFamily);
        }
    }

    /// <summary>エディタ・差分表示用の等幅フォントファミリー。変更時にフォントを再適用する。</summary>
    public string MonospaceFontFamily
    {
        get => _monospaceFontFamily;
        set
        {
            if (SetProperty(ref _monospaceFontFamily, value) && !_isLoading)
                App.SetFonts(_defaultFontFamily, value);
        }
    }

    /// <summary>OSネイティブのウィンドウフレームを使用するかどうか。</summary>
    public bool UseSystemWindowFrame
    {
        get => Native.OS.UseSystemWindowFrame;
        set => Native.OS.UseSystemWindowFrame = value;
    }

    /// <summary>UIのデフォルトフォントサイズ。</summary>
    public double DefaultFontSize
    {
        get => _defaultFontSize;
        set => SetProperty(ref _defaultFontSize, value);
    }

    /// <summary>エディタ・差分表示のフォントサイズ。</summary>
    public double EditorFontSize
    {
        get => _editorFontSize;
        set => SetProperty(ref _editorFontSize, value);
    }

    /// <summary>エディタのタブ幅（スペース数）。</summary>
    public int EditorTabWidth
    {
        get => _editorTabWidth;
        set => SetProperty(ref _editorTabWidth, value);
    }

    /// <summary>UI全体のズーム倍率（1.0 = 100%）。</summary>
    public double Zoom
    {
        get => _zoom;
        set => SetProperty(ref _zoom, value);
    }

    /// <summary>ウィンドウとパネルのレイアウト情報。</summary>
    public LayoutInfo Layout
    {
        get => _layout;
        set => SetProperty(ref _layout, value);
    }

    /// <summary>コミット詳細画面でデフォルトでローカル変更を表示するかどうか。</summary>
    public bool ShowLocalChangesByDefault
    {
        get;
        set;
    } = false;

    /// <summary>コミット詳細画面でデフォルトで変更ファイルを表示するかどうか。</summary>
    public bool ShowChangesInCommitDetailByDefault
    {
        get;
        set;
    } = false;

    /// <summary>コミット履歴の最大表示件数。</summary>
    public int MaxHistoryCommits
    {
        get => _maxHistoryCommits;
        set => SetProperty(ref _maxHistoryCommits, value);
    }

    /// <summary>コミットメッセージのサブジェクト行ガイド文字数。</summary>
    public int SubjectGuideLength
    {
        get => _subjectGuideLength;
        set => SetProperty(ref _subjectGuideLength, value);
    }

    /// <summary>日時表示フォーマットのインデックス。</summary>
    public int DateTimeFormat
    {
        get => Models.DateTimeFormat.ActiveIndex;
        set
        {
            if (value != Models.DateTimeFormat.ActiveIndex &&
                value >= 0 &&
                value < Models.DateTimeFormat.Supported.Count)
            {
                Models.DateTimeFormat.ActiveIndex = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>24時間制で時刻を表示するかどうか。</summary>
    public bool Use24Hours
    {
        get => Models.DateTimeFormat.Use24Hours;
        set
        {
            if (value != Models.DateTimeFormat.Use24Hours)
            {
                Models.DateTimeFormat.Use24Hours = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>タブ幅を固定するかどうか。</summary>
    public bool UseFixedTabWidth
    {
        get => _useFixedTabWidth;
        set => SetProperty(ref _useFixedTabWidth, value);
    }

    /// <summary>スクロールバーを自動的に非表示にするかどうか。</summary>
    public bool UseAutoHideScrollBars
    {
        get => _useAutoHideScrollBars;
        set => SetProperty(ref _useAutoHideScrollBars, value);
    }

    /// <summary>GitHub形式のアバター（Identicon）を使用するかどうか。</summary>
    public bool UseGitHubStyleAvatar
    {
        get => _useGitHubStyleAvatar;
        set => SetProperty(ref _useGitHubStyleAvatar, value);
    }

    /// <summary>起動時にアップデートを自動チェックするかどうか。</summary>
    public bool Check4UpdatesOnStartup
    {
        get => _check4UpdatesOnStartup;
        set => SetProperty(ref _check4UpdatesOnStartup, value);
    }

    /// <summary>コミットグラフにAuthorDate（作成日時）を表示するかどうか。</summary>
    public bool ShowAuthorTimeInGraph
    {
        get => _showAuthorTimeInGraph;
        set => SetProperty(ref _showAuthorTimeInGraph, value);
    }

    /// <summary>コミットの子コミットを表示するかどうか。</summary>
    public bool ShowChildren
    {
        get => _showChildren;
        set => SetProperty(ref _showChildren, value);
    }

    /// <summary>アップデートを無視するタグ名。このタグのバージョンは通知しない。</summary>
    public string IgnoreUpdateTag
    {
        get => _ignoreUpdateTag;
        set => SetProperty(ref _ignoreUpdateTag, value);
    }

    /// <summary>コミットグラフにタグを表示するかどうか。</summary>
    public bool ShowTagsInGraph
    {
        get => _showTagsInGraph;
        set => SetProperty(ref _showTagsInGraph, value);
    }

    /// <summary>履歴画面で2カラムレイアウトを使用するかどうか。</summary>
    public bool UseTwoColumnsLayoutInHistories
    {
        get => _useTwoColumnsLayoutInHistories;
        set => SetProperty(ref _useTwoColumnsLayoutInHistories, value);
    }

    /// <summary>履歴画面で日時を経過期間として表示するかどうか（例: "3日前"）。</summary>
    public bool DisplayTimeAsPeriodInHistories
    {
        get => _displayTimeAsPeriodInHistories;
        set => SetProperty(ref _displayTimeAsPeriodInHistories, value);
    }

    /// <summary>差分表示をサイドバイサイド（左右並列）で行うかどうか。</summary>
    public bool UseSideBySideDiff
    {
        get => _useSideBySideDiff;
        set => SetProperty(ref _useSideBySideDiff, value);
    }

    /// <summary>差分表示でシンタックスハイライトを使用するかどうか。</summary>
    public bool UseSyntaxHighlighting
    {
        get => _useSyntaxHighlighting;
        set => SetProperty(ref _useSyntaxHighlighting, value);
    }

    /// <summary>差分表示で行末のCR（キャリッジリターン）を無視するかどうか。</summary>
    public bool IgnoreCRAtEOLInDiff
    {
        get => Models.DiffOption.IgnoreCRAtEOL;
        set
        {
            if (Models.DiffOption.IgnoreCRAtEOL != value)
            {
                Models.DiffOption.IgnoreCRAtEOL = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>差分表示でホワイトスペースの変更を無視するかどうか。</summary>
    public bool IgnoreWhitespaceChangesInDiff
    {
        get => _ignoreWhitespaceChangesInDiff;
        set => SetProperty(ref _ignoreWhitespaceChangesInDiff, value);
    }

    /// <summary>差分ビューでワードラップを有効にするかどうか。</summary>
    public bool EnableDiffViewWordWrap
    {
        get => _enableDiffViewWordWrap;
        set => SetProperty(ref _enableDiffViewWordWrap, value);
    }

    /// <summary>差分ビューで非表示文字（タブ、スペース等）を表示するかどうか。</summary>
    public bool ShowHiddenSymbolsInDiffView
    {
        get => _showHiddenSymbolsInDiffView;
        set => SetProperty(ref _showHiddenSymbolsInDiffView, value);
    }

    /// <summary>差分表示でファイル全体を表示するかどうか（falseの場合は変更箇所の前後のみ）。</summary>
    public bool UseFullTextDiff
    {
        get => _useFullTextDiff;
        set => SetProperty(ref _useFullTextDiff, value);
    }

    /// <summary>LFS画像プレビューのアクティブなタブインデックス。</summary>
    public int LFSImageActiveIdx
    {
        get => _lfsImageActiveIdx;
        set => SetProperty(ref _lfsImageActiveIdx, value);
    }

    /// <summary>画像差分表示のアクティブなタブインデックス。</summary>
    public int ImageDiffActiveIdx
    {
        get => _imageDiffActiveIdx;
        set => SetProperty(ref _imageDiffActiveIdx, value);
    }

    /// <summary>変更ツリーでフォルダをコンパクト表示にするかどうか（連続する単一子フォルダを結合）。</summary>
    public bool EnableCompactFoldersInChangesTree
    {
        get => _enableCompactFoldersInChangesTree;
        set => SetProperty(ref _enableCompactFoldersInChangesTree, value);
    }

    /// <summary>ステージングされていない変更の表示モード（リスト/ツリー/グリッド）。</summary>
    public Models.ChangeViewMode UnstagedChangeViewMode
    {
        get => _unstagedChangeViewMode;
        set => SetProperty(ref _unstagedChangeViewMode, value);
    }

    /// <summary>ステージング済み変更の表示モード。</summary>
    public Models.ChangeViewMode StagedChangeViewMode
    {
        get => _stagedChangeViewMode;
        set => SetProperty(ref _stagedChangeViewMode, value);
    }

    /// <summary>コミット詳細の変更ファイル表示モード。</summary>
    public Models.ChangeViewMode CommitChangeViewMode
    {
        get => _commitChangeViewMode;
        set => SetProperty(ref _commitChangeViewMode, value);
    }

    /// <summary>スタッシュの変更ファイル表示モード。</summary>
    public Models.ChangeViewMode StashChangeViewMode
    {
        get => _stashChangeViewMode;
        set => SetProperty(ref _stashChangeViewMode, value);
    }

    /// <summary>Gitの実行ファイルパス。変更時にNative.OSを更新する。</summary>
    public string GitInstallPath
    {
        get => Native.OS.GitExecutable;
        set
        {
            if (Native.OS.GitExecutable != value)
            {
                Native.OS.GitExecutable = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>git cloneのデフォルト保存先ディレクトリ。</summary>
    public string GitDefaultCloneDir
    {
        get => _gitDefaultCloneDir;
        set => SetProperty(ref _gitDefaultCloneDir, value);
    }

    /// <summary>グローバルSSH秘密鍵のファイルパス。リモート個別設定がない場合のフォールバック。</summary>
    public string GlobalSSHKey
    {
        get => _globalSSHKey;
        set => SetProperty(ref _globalSSHKey, value);
    }

    /// <summary>Linux環境でGCMの代わりにlibsecretを資格情報ヘルパーとして使用するかどうか。</summary>
    public bool UseLibsecretInsteadOfGCM
    {
        get => Native.OS.CredentialHelper.Equals("libsecret", StringComparison.Ordinal);
        set
        {
            var helper = value ? "libsecret" : "manager";
            if (OperatingSystem.IsLinux() && !Native.OS.CredentialHelper.Equals(helper, StringComparison.Ordinal))
            {
                Native.OS.CredentialHelper = helper;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>シェルまたはターミナルの種類インデックス。変更時にNative.OSを更新する。</summary>
    public int ShellOrTerminalType
    {
        get => _shellOrTerminalType;
        set
        {
            if (SetProperty(ref _shellOrTerminalType, value) && !_isLoading)
            {
                if (value >= 0 && value < Models.ShellOrTerminal.Supported.Count)
                    Native.OS.SetShellOrTerminal(Models.ShellOrTerminal.Supported[value]);
                else
                    Native.OS.SetShellOrTerminal(null);

                OnPropertyChanged(nameof(ShellOrTerminalPath));
                OnPropertyChanged(nameof(ShellOrTerminalArgs));
            }
        }
    }

    /// <summary>シェルまたはターミナルの実行ファイルパス。</summary>
    public string ShellOrTerminalPath
    {
        get => Native.OS.ShellOrTerminal;
        set
        {
            if (value != Native.OS.ShellOrTerminal)
            {
                Native.OS.ShellOrTerminal = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>シェルまたはターミナルの起動引数。</summary>
    public string ShellOrTerminalArgs
    {
        get => Native.OS.ShellOrTerminalArgs;
        set
        {
            if (value != Native.OS.ShellOrTerminalArgs)
            {
                Native.OS.ShellOrTerminalArgs = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>外部マージ/差分ツールの種類インデックス。変更時に実行ファイルパスを自動検出する。</summary>
    public int ExternalMergeToolType
    {
        get => Native.OS.ExternalMergerType;
        set
        {
            if (Native.OS.ExternalMergerType != value)
            {
                Native.OS.ExternalMergerType = value;
                OnPropertyChanged();

                if (!_isLoading)
                {
                    // ツール種類変更時に実行ファイルパスと引数を自動設定
                    Native.OS.AutoSelectExternalMergeToolExecFile();
                    OnPropertyChanged(nameof(ExternalMergeToolPath));
                    OnPropertyChanged(nameof(ExternalMergeToolDiffArgs));
                    OnPropertyChanged(nameof(ExternalMergeToolMergeArgs));
                }
            }
        }
    }

    /// <summary>外部マージ/差分ツールの実行ファイルパス。</summary>
    public string ExternalMergeToolPath
    {
        get => Native.OS.ExternalMergerExecFile;
        set
        {
            if (!Native.OS.ExternalMergerExecFile.Equals(value, StringComparison.Ordinal))
            {
                Native.OS.ExternalMergerExecFile = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>外部差分ツールのコマンドライン引数テンプレート。</summary>
    public string ExternalMergeToolDiffArgs
    {
        get => Native.OS.ExternalDiffArgs;
        set
        {
            if (!Native.OS.ExternalDiffArgs.Equals(value, StringComparison.Ordinal))
            {
                Native.OS.ExternalDiffArgs = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>外部マージツールのコマンドライン引数テンプレート。</summary>
    public string ExternalMergeToolMergeArgs
    {
        get => Native.OS.ExternalMergeArgs;
        set
        {
            if (!Native.OS.ExternalMergeArgs.Equals(value, StringComparison.Ordinal))
            {
                Native.OS.ExternalMergeArgs = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>統計グラフのサンプルカラー（ARGB形式）。</summary>
    public uint StatisticsSampleColor
    {
        get => _statisticsSampleColor;
        set => SetProperty(ref _statisticsSampleColor, value);
    }

    /// <summary>リポジトリノードのツリー構造（グループとリポジトリ）。</summary>
    public List<RepositoryNode> RepositoryNodes
    {
        get;
        set;
    } = [];

    /// <summary>ワークスペースの一覧。</summary>
    public List<Workspace> Workspaces
    {
        get;
        set;
    } = [];

    /// <summary>ユーザー定義のカスタムアクション一覧。</summary>
    public AvaloniaList<Models.CustomAction> CustomActions
    {
        get;
        set;
    } = [];

    /// <summary>OpenAI/AI APIサービスの設定一覧。AIコミットメッセージ生成に使用する。</summary>
    public AvaloniaList<AI.Service> OpenAIServices
    {
        get;
        set;
    } = [];

    /// <summary>JSONデシリアライズ時の未知プロパティを保持する拡張データ。</summary>
    [JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement> ExtensionData { get; set; }

    /// <summary>設定の変更を許可する。初回ロード完了後に呼び出す。</summary>
    public void SetCanModify()
    {
        _isReadonly = false;
    }

    /// <summary>Gitの実行ファイルが設定済みかつ存在するかを返す。</summary>
    public bool IsGitConfigured()
    {
        var path = GitInstallPath;
        return !string.IsNullOrEmpty(path) && File.Exists(path);
    }

    /// <summary>起動時にアップデートをチェックすべきかどうかを返す。</summary>
    public bool ShouldCheck4UpdateOnStartup()
    {
        return _check4UpdatesOnStartup;
    }

    /// <summary>アクティブなワークスペースを取得する。ワークスペースが空の場合はDefaultを作成する。</summary>
    public Workspace GetActiveWorkspace()
    {
        foreach (var w in Workspaces)
        {
            if (w.IsActive)
                return w;
        }

        // ワークスペースが存在しない場合はデフォルトを作成
        if (Workspaces.Count == 0)
            Workspaces.Add(new Workspace() { Name = "Default", IsActive = true });

        var first = Workspaces[0];
        first.IsActive = true;
        return first;
    }

    /// <summary>リポジトリノードを指定グループに追加し、ソートする。</summary>
    public void AddNode(RepositoryNode node, RepositoryNode to, bool save)
    {
        var collection = to is null ? RepositoryNodes : to.SubNodes;
        collection.Add(node);
        SortNodes(collection);

        if (save)
            Save();
    }

    /// <summary>ノードコレクションをグループ優先・名前順でソートする。</summary>
    public void SortNodes(List<RepositoryNode> collection)
    {
        collection?.Sort((l, r) =>
        {
            // グループをリポジトリより先に表示
            if (l.IsRepository != r.IsRepository)
                return l.IsRepository ? 1 : -1;

            return Models.NumericSort.Compare(l.Name, r.Name);
        });
    }

    /// <summary>IDでリポジトリノードを再帰検索する。見つからない場合はnullを返す。</summary>
    public RepositoryNode FindNode(string id)
    {
        return FindNodeRecursive(id, RepositoryNodes);
    }

    /// <summary>
    /// リポジトリパスに対応するノードを検索し、存在しなければ新規作成して追加する。
    /// shouldMoveNodeがtrueで既存ノードがある場合は指定グループに移動する。
    /// </summary>
    public RepositoryNode FindOrAddNodeByRepositoryPath(string repo, RepositoryNode parent, bool shouldMoveNode, bool save = true)
    {
        // パス区切り文字を統一し、末尾のスラッシュを除去
        var normalized = repo.Replace('\\', '/').TrimEnd('/');

        var node = FindNodeRecursive(normalized, RepositoryNodes);
        if (node is null)
        {
            node = new RepositoryNode()
            {
                Id = normalized,
                Name = Path.GetFileName(normalized),
                Bookmark = 0,
                IsRepository = true,
            };

            AddNode(node, parent, save);
        }
        else if (shouldMoveNode)
        {
            MoveNode(node, parent, save);
        }

        return node;
    }

    /// <summary>ノードを別のグループに移動する。既に同じグループにいる場合は何もしない。</summary>
    public void MoveNode(RepositoryNode node, RepositoryNode to, bool save)
    {
        if (to is null && RepositoryNodes.Contains(node))
            return;
        if (to is not null && to.SubNodes.Contains(node))
            return;

        RemoveNode(node, false);
        AddNode(node, to, false);

        if (save)
            Save();
    }

    /// <summary>リポジトリノードをツリーから削除する。</summary>
    public void RemoveNode(RepositoryNode node, bool save)
    {
        RemoveNodeRecursive(node, RepositoryNodes);

        if (save)
            Save();
    }

    /// <summary>リネームされたノードの親コレクションを再ソートし、設定を保存する。</summary>
    public void SortByRenamedNode(RepositoryNode node)
    {
        var container = FindNodeContainer(node, RepositoryNodes);
        SortNodes(container);
        Save();
    }

    /// <summary>ディスク上に存在しない無効なリポジトリノードを自動削除する。</summary>
    public void AutoRemoveInvalidNode()
    {
        RemoveInvalidRepositoriesRecursive(RepositoryNodes);
    }

    /// <summary>設定をpreference.jsonファイルに保存する。読み込み中またはリードオンリーの場合はスキップする。</summary>
    public void Save()
    {
        if (_isLoading || _isReadonly)
            return;

        ExtensionData = null;

        try
        {
            var file = Path.Combine(Native.OS.DataDir, "preference.json");
            using var stream = File.Create(file);
            JsonSerializer.Serialize(stream, this, JsonCodeGen.Default.Preferences);
        }
        catch (Exception ex)
        {
            Models.Logger.LogException("設定ファイルの保存に失敗しました", ex);
        }
    }

    /// <summary>preference.jsonから設定を読み込む。ファイルが存在しない場合はデフォルト設定を返す。</summary>
    private static Preferences Load()
    {
        var path = Path.Combine(Native.OS.DataDir, "preference.json");
        if (!File.Exists(path))
            return new Preferences();

        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize(stream, JsonCodeGen.Default.Preferences);
        }
        catch (Exception ex)
        {
            Models.Logger.Log($"設定ファイルの読み込みに失敗、デフォルト設定を使用: {ex.Message}", Models.LogLevel.Warning);
            return new Preferences();
        }
    }

    /// <summary>Git実行ファイルのパスを検出・設定する。未設定または存在しない場合に自動検出する。</summary>
    private void PrepareGit()
    {
        var path = Native.OS.GitExecutable;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            GitInstallPath = Native.OS.FindGitExecutable();
    }

    /// <summary>利用可能なシェル/ターミナルを自動検出して設定する。既に設定済みの場合はスキップ。</summary>
    private void PrepareShellOrTerminal()
    {
        if (_shellOrTerminalType >= 0)
            return;

        for (int i = 0; i < Models.ShellOrTerminal.Supported.Count; i++)
        {
            var shell = Models.ShellOrTerminal.Supported[i];
            if (Native.OS.TestShellOrTerminal(shell))
            {
                ShellOrTerminalType = i;
                break;
            }
        }
    }

    /// <summary>外部差分/マージツールの引数テンプレートが未設定の場合にデフォルト値を設定する。</summary>
    private static void PrepareExternalDiffMergeTool()
    {
        var mergerType = Native.OS.ExternalMergerType;
        if (mergerType > 0 && mergerType < Models.ExternalMerger.Supported.Count)
        {
            var merger = Models.ExternalMerger.Supported[mergerType];
            if (string.IsNullOrEmpty(Native.OS.ExternalDiffArgs))
                Native.OS.ExternalDiffArgs = merger.DiffCmd;
            if (string.IsNullOrEmpty(Native.OS.ExternalMergeArgs))
                Native.OS.ExternalMergeArgs = merger.MergeCmd;
        }
    }

    /// <summary>ワークスペースを初期化する。空の場合はDefaultワークスペースを作成する。</summary>
    private void PrepareWorkspaces()
    {
        if (Workspaces.Count == 0)
        {
            Workspaces.Add(new Workspace() { Name = "Default" });
            return;
        }

        // 起動時復元が無効なワークスペースのリポジトリ一覧をクリア
        foreach (var workspace in Workspaces)
        {
            if (!workspace.RestoreOnStartup)
            {
                workspace.Repositories.Clear();
                workspace.ActiveIdx = 0;
            }
        }
    }

    /// <summary>指定IDのノードをツリーから再帰的に検索する。</summary>
    private static RepositoryNode FindNodeRecursive(string id, List<RepositoryNode> collection)
    {
        foreach (var node in collection)
        {
            if (node.Id == id)
                return node;

            var sub = FindNodeRecursive(id, node.SubNodes);
            if (sub is not null)
                return sub;
        }

        return null;
    }

    /// <summary>指定ノードが属する親コレクションを再帰的に検索する。</summary>
    private static List<RepositoryNode> FindNodeContainer(RepositoryNode node, List<RepositoryNode> collection)
    {
        foreach (var sub in collection)
        {
            if (node == sub)
                return collection;

            var subCollection = FindNodeContainer(node, sub.SubNodes);
            if (subCollection is not null)
                return subCollection;
        }

        return null;
    }

    /// <summary>指定ノードをツリーから再帰的に削除する。</summary>
    private static bool RemoveNodeRecursive(RepositoryNode node, List<RepositoryNode> collection)
    {
        if (collection.Contains(node))
        {
            collection.Remove(node);
            return true;
        }

        foreach (var one in collection)
        {
            if (RemoveNodeRecursive(node, one.SubNodes))
                return true;
        }

        return false;
    }

    /// <summary>無効な（ディスク上に存在しない）リポジトリノードを再帰的に削除する。</summary>
    private static bool RemoveInvalidRepositoriesRecursive(List<RepositoryNode> collection)
    {
        bool changed = false;

        // 後ろからループしてインデックスの整合性を維持
        for (int i = collection.Count - 1; i >= 0; i--)
        {
            var node = collection[i];
            if (node.IsInvalid)
            {
                collection.RemoveAt(i);
                changed = true;
            }
            else if (!node.IsRepository)
            {
                changed |= RemoveInvalidRepositoriesRecursive(node.SubNodes);
            }
        }

        return changed;
    }

    /// <summary>システムのカルチャ情報からデフォルトロケールを自動検出する。</summary>
    private static string DetectDefaultLocale()
    {
        var supported = new HashSet<string>(StringComparer.Ordinal)
        {
            "de_DE", "en_US", "es_ES", "fil_PH", "fr_FR", "id_ID", "it_IT", "ja_JP",
            "ko_KR", "pt_BR", "ru_RU", "ta_IN", "uk_UA", "zh_CN", "zh_TW",
        };

        var culture = System.Globalization.CultureInfo.CurrentUICulture;

        var exact = culture.Name.Replace('-', '_');
        if (supported.Contains(exact))
            return exact;

        var lang = culture.TwoLetterISOLanguageName;

        if (lang == "zh")
        {
            var name = culture.Name;
            if (name.Contains("Hant") || name.Contains("TW") || name.Contains("HK") || name.Contains("MO"))
                return "zh_TW";
            return "zh_CN";
        }

        foreach (var locale in supported)
        {
            if (locale.StartsWith(lang + "_", StringComparison.Ordinal))
                return locale;
        }

        return "en_US";
    }

    private static Preferences _instance = null;                     // シングルトンインスタンス
    private static readonly string s_detectedLocale = DetectDefaultLocale(); // 自動検出されたロケール

    /// <summary>システムから自動検出されたデフォルトロケール。</summary>
    internal static string DetectedLocale => s_detectedLocale;

    private bool _isLoading = true;                                  // 設定読み込み中フラグ
    private bool _isReadonly = true;                                  // 読み取り専用フラグ
    private string _locale = "en_US";                                // UIロケール
    private string _theme = "Default";                               // カラーテーマ
    private string _themeOverrides = string.Empty;                   // テーマオーバーライド
    private string _defaultFontFamily = s_detectedLocale == "ja_JP" ? "Yu Gothic UI" : "Inter";       // デフォルトフォント
    private string _monospaceFontFamily = s_detectedLocale == "ja_JP" ? "UDEV Gothic JPDOC" : "JetBrains Mono"; // 等幅フォント
    private double _defaultFontSize = 13;                            // デフォルトフォントサイズ
    private double _editorFontSize = 13;                             // エディタフォントサイズ
    private int _editorTabWidth = 4;                                 // エディタタブ幅
    private double _zoom = 1.0;                                      // ズーム倍率
    private LayoutInfo _layout = new();                              // レイアウト情報

    private int _maxHistoryCommits = 20000;                          // 最大履歴コミット数
    private int _subjectGuideLength = 50;                            // サブジェクトガイド文字数
    private bool _useFixedTabWidth = true;                           // 固定タブ幅
    private bool _useAutoHideScrollBars = true;                      // スクロールバー自動非表示
    private bool _useGitHubStyleAvatar = true;                       // GitHub形式アバター
    private bool _showAuthorTimeInGraph = false;                     // グラフにAuthorDate表示
    private bool _showChildren = false;                              // 子コミット表示

    private bool _check4UpdatesOnStartup = true;                     // 起動時アップデートチェック
    private string _ignoreUpdateTag = string.Empty;                  // 無視するアップデートタグ

    private bool _showTagsInGraph = true;                            // グラフにタグ表示
    private bool _useTwoColumnsLayoutInHistories = false;            // 履歴2カラムレイアウト
    private bool _displayTimeAsPeriodInHistories = false;            // 経過期間表示
    private bool _useSideBySideDiff = false;                         // サイドバイサイド差分
    private bool _ignoreWhitespaceChangesInDiff = false;             // ホワイトスペース無視
    private bool _useSyntaxHighlighting = false;                     // シンタックスハイライト
    private bool _enableDiffViewWordWrap = false;                    // 差分ワードラップ
    private bool _showHiddenSymbolsInDiffView = false;               // 非表示文字表示
    private bool _useFullTextDiff = false;                           // 全文差分表示
    private int _lfsImageActiveIdx = 0;                              // LFS画像タブインデックス
    private int _imageDiffActiveIdx = 0;                             // 画像差分タブインデックス
    private bool _enableCompactFoldersInChangesTree = false;         // コンパクトフォルダ表示

    private Models.ChangeViewMode _unstagedChangeViewMode = Models.ChangeViewMode.List;   // 未ステージング変更表示モード
    private Models.ChangeViewMode _stagedChangeViewMode = Models.ChangeViewMode.List;     // ステージング済み変更表示モード
    private Models.ChangeViewMode _commitChangeViewMode = Models.ChangeViewMode.List;     // コミット変更表示モード
    private Models.ChangeViewMode _stashChangeViewMode = Models.ChangeViewMode.List;      // スタッシュ変更表示モード

    private string _gitDefaultCloneDir = string.Empty;               // デフォルトクローン先
    private string _globalSSHKey = string.Empty;                     // グローバルSSHキー
    private int _shellOrTerminalType = -1;                           // シェル/ターミナル種類
    private uint _statisticsSampleColor = 0xFF00FF00;                // 統計グラフサンプルカラー
}
