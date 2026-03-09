using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     アプリケーション設定を管理するシングルトンViewModel。
    ///     ロケール、テーマ、フォント、差分表示、外部ツール、リポジトリノード、ワークスペース等を保持する。
    ///     preference.jsonにシリアライズされる。
    /// </summary>
    public class Preferences : ObservableObject
    {
        /// <summary>
        ///     シングルトンインスタンス。初回アクセス時にJSONから読み込み、
        ///     日本語ロケールのフォントマイグレーションやGit/シェル/外部ツールの初期化を行う。
        /// </summary>
        [JsonIgnore]
        public static Preferences Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                _instance = Load();

                // 日本語ロケールユーザーのフォントデフォルトをマイグレーション
                if (_instance._locale == "ja_JP")
                {
                    if (_instance._defaultFontFamily == "Inter")
                        _instance._defaultFontFamily = "Yu Gothic UI";
                    if (_instance._monospaceFontFamily == "JetBrains Mono")
                        _instance._monospaceFontFamily = "UDEV Gothic JPDOC";
                }

                _instance._isLoading = false;

                // 各種外部ツール・環境設定の初期化
                _instance.PrepareGit();
                _instance.PrepareShellOrTerminal();
                _instance.PrepareExternalDiffMergeTool();
                _instance.PrepareWorkspaces();

                return _instance;
            }
        }

        /// <summary>UIのロケール（例: "ja_JP", "en_US"）。変更時にアプリ全体のロケールを切り替える。</summary>
        public string Locale
        {
            get => _locale;
            set
            {
                if (SetProperty(ref _locale, value) && !_isLoading)
                    App.SetLocale(value);
            }
        }

        /// <summary>カラーテーマ名（"Default", "Dark", "White"等）。変更時にテーマを即時適用する。</summary>
        public string Theme
        {
            get => _theme;
            set
            {
                if (SetProperty(ref _theme, value) && !_isLoading)
                    App.SetTheme(_theme, _themeOverrides);
            }
        }

        /// <summary>テーマカラーのオーバーライド設定。</summary>
        public string ThemeOverrides
        {
            get => _themeOverrides;
            set
            {
                if (SetProperty(ref _themeOverrides, value) && !_isLoading)
                    App.SetTheme(_theme, value);
            }
        }

        /// <summary>UIのデフォルトフォントファミリー。空の場合は"Inter"にフォールバック。</summary>
        public string DefaultFontFamily
        {
            get => _defaultFontFamily;
            set
            {
                if (string.IsNullOrEmpty(value))
                    value = "Inter";

                if (SetProperty(ref _defaultFontFamily, value) && !_isLoading)
                    App.SetFonts(value, _monospaceFontFamily);
            }
        }

        /// <summary>等幅フォントファミリー（差分表示・エディタ用）。空の場合は"JetBrains Mono"にフォールバック。</summary>
        public string MonospaceFontFamily
        {
            get => _monospaceFontFamily;
            set
            {
                if (string.IsNullOrEmpty(value))
                    value = "JetBrains Mono";

                if (SetProperty(ref _monospaceFontFamily, value) && !_isLoading)
                    App.SetFonts(_defaultFontFamily, value);
            }
        }

        /// <summary>OSのシステムウィンドウフレームを使用するかどうか。</summary>
        public bool UseSystemWindowFrame
        {
            get => Native.OS.UseSystemWindowFrame;
            set => Native.OS.UseSystemWindowFrame = value;
        }

        /// <summary>UIのデフォルトフォントサイズ（ピクセル）。</summary>
        public double DefaultFontSize
        {
            get => _defaultFontSize;
            set => SetProperty(ref _defaultFontSize, value);
        }

        /// <summary>エディタ（差分表示等）のフォントサイズ（ピクセル）。</summary>
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

        /// <summary>ウィンドウレイアウト情報（サイズ・位置・分割幅等）。</summary>
        public LayoutInfo Layout
        {
            get => _layout;
            set => SetProperty(ref _layout, value);
        }

        /// <summary>リポジトリを開いた時にデフォルトでローカル変更を表示するかどうか。</summary>
        public bool ShowLocalChangesByDefault
        {
            get;
            set;
        } = false;

        /// <summary>コミット詳細画面でデフォルトで変更一覧を表示するかどうか。</summary>
        public bool ShowChangesInCommitDetailByDefault
        {
            get;
            set;
        } = false;

        /// <summary>履歴に表示するコミットの最大件数。</summary>
        public int MaxHistoryCommits
        {
            get => _maxHistoryCommits;
            set => SetProperty(ref _maxHistoryCommits, value);
        }

        /// <summary>コミットメッセージのサブジェクト行ガイド長（文字数）。</summary>
        public int SubjectGuideLength
        {
            get => _subjectGuideLength;
            set => SetProperty(ref _subjectGuideLength, value);
        }

        /// <summary>日時の表示フォーマットのインデックス。</summary>
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

        /// <summary>タブ幅を固定するかどうか。falseの場合はタブ名に応じて可変幅になる。</summary>
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

        /// <summary>GitHubスタイルのアバター画像を使用するかどうか。</summary>
        public bool UseGitHubStyleAvatar
        {
            get => _useGitHubStyleAvatar;
            set => SetProperty(ref _useGitHubStyleAvatar, value);
        }

        /// <summary>起動時に自動更新チェックを行うかどうか。</summary>
        public bool Check4UpdatesOnStartup
        {
            get => _check4UpdatesOnStartup;
            set => SetProperty(ref _check4UpdatesOnStartup, value);
        }

        /// <summary>コミットグラフでコミット日時の代わりに作者日時を表示するかどうか。</summary>
        public bool ShowAuthorTimeInGraph
        {
            get => _showAuthorTimeInGraph;
            set => SetProperty(ref _showAuthorTimeInGraph, value);
        }

        /// <summary>コミット詳細で子コミットへの参照を表示するかどうか。</summary>
        public bool ShowChildren
        {
            get => _showChildren;
            set => SetProperty(ref _showChildren, value);
        }

        /// <summary>無視する更新バージョンタグ。このバージョンの更新通知は表示されない。</summary>
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

        /// <summary>履歴画面で日時を相対時間（「3時間前」等）で表示するかどうか。</summary>
        public bool DisplayTimeAsPeriodInHistories
        {
            get => _displayTimeAsPeriodInHistories;
            set => SetProperty(ref _displayTimeAsPeriodInHistories, value);
        }

        /// <summary>差分表示をサイドバイサイド（左右並列）モードにするかどうか。</summary>
        public bool UseSideBySideDiff
        {
            get => _useSideBySideDiff;
            set => SetProperty(ref _useSideBySideDiff, value);
        }

        /// <summary>差分表示でシンタックスハイライトを有効にするかどうか。</summary>
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

        /// <summary>差分表示で空白文字の変更を無視するかどうか。</summary>
        public bool IgnoreWhitespaceChangesInDiff
        {
            get => _ignoreWhitespaceChangesInDiff;
            set => SetProperty(ref _ignoreWhitespaceChangesInDiff, value);
        }

        /// <summary>差分表示で行の折り返しを有効にするかどうか。</summary>
        public bool EnableDiffViewWordWrap
        {
            get => _enableDiffViewWordWrap;
            set => SetProperty(ref _enableDiffViewWordWrap, value);
        }

        /// <summary>差分表示で不可視文字（タブ、スペース等）を表示するかどうか。</summary>
        public bool ShowHiddenSymbolsInDiffView
        {
            get => _showHiddenSymbolsInDiffView;
            set => SetProperty(ref _showHiddenSymbolsInDiffView, value);
        }

        /// <summary>差分表示で全テキスト差分を使用するかどうか（コンテキスト行数制限なし）。</summary>
        public bool UseFullTextDiff
        {
            get => _useFullTextDiff;
            set => SetProperty(ref _useFullTextDiff, value);
        }

        /// <summary>LFS画像の差分表示モードのアクティブインデックス。</summary>
        public int LFSImageActiveIdx
        {
            get => _lfsImageActiveIdx;
            set => SetProperty(ref _lfsImageActiveIdx, value);
        }

        /// <summary>画像差分表示モードのアクティブインデックス。</summary>
        public int ImageDiffActiveIdx
        {
            get => _imageDiffActiveIdx;
            set => SetProperty(ref _imageDiffActiveIdx, value);
        }

        /// <summary>変更ツリーで空のフォルダを折りたたんでコンパクト表示するかどうか。</summary>
        public bool EnableCompactFoldersInChangesTree
        {
            get => _enableCompactFoldersInChangesTree;
            set => SetProperty(ref _enableCompactFoldersInChangesTree, value);
        }

        /// <summary>未ステージの変更一覧の表示モード（リスト/ツリー/グリッド）。</summary>
        public Models.ChangeViewMode UnstagedChangeViewMode
        {
            get => _unstagedChangeViewMode;
            set => SetProperty(ref _unstagedChangeViewMode, value);
        }

        /// <summary>ステージ済みの変更一覧の表示モード（リスト/ツリー/グリッド）。</summary>
        public Models.ChangeViewMode StagedChangeViewMode
        {
            get => _stagedChangeViewMode;
            set => SetProperty(ref _stagedChangeViewMode, value);
        }

        /// <summary>コミット詳細の変更一覧の表示モード（リスト/ツリー/グリッド）。</summary>
        public Models.ChangeViewMode CommitChangeViewMode
        {
            get => _commitChangeViewMode;
            set => SetProperty(ref _commitChangeViewMode, value);
        }

        /// <summary>スタッシュの変更一覧の表示モード（リスト/ツリー/グリッド）。</summary>
        public Models.ChangeViewMode StashChangeViewMode
        {
            get => _stashChangeViewMode;
            set => SetProperty(ref _stashChangeViewMode, value);
        }

        /// <summary>gitの実行ファイルパス。Native.OSのGitExecutableと連動する。</summary>
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

        /// <summary>git cloneのデフォルト先ディレクトリパス。</summary>
        public string GitDefaultCloneDir
        {
            get => _gitDefaultCloneDir;
            set => SetProperty(ref _gitDefaultCloneDir, value);
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

        /// <summary>シェル/ターミナルの種類インデックス。変更時にOSのシェル設定も更新する。</summary>
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

        /// <summary>シェル/ターミナルの実行ファイルパス。</summary>
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

        /// <summary>シェル/ターミナルの起動引数。</summary>
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

        /// <summary>外部マージ/差分ツールの種類インデックス。変更時にツールパスと引数を自動設定する。</summary>
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

        /// <summary>統計グラフのサンプルカラー（ARGB形式のuint値）。</summary>
        public uint StatisticsSampleColor
        {
            get => _statisticsSampleColor;
            set => SetProperty(ref _statisticsSampleColor, value);
        }

        /// <summary>リポジトリノードのツリー構造。グループとリポジトリの階層を表す。</summary>
        public List<RepositoryNode> RepositoryNodes
        {
            get;
            set;
        } = [];

        /// <summary>ワークスペースの一覧。各ワークスペースは開くリポジトリのセットを管理する。</summary>
        public List<Workspace> Workspaces
        {
            get;
            set;
        } = [];

        /// <summary>グローバルカスタムアクションの一覧。全リポジトリで使用可能。</summary>
        public AvaloniaList<Models.CustomAction> CustomActions
        {
            get;
            set;
        } = [];

        /// <summary>OpenAI/AI サービスの接続設定一覧。コミットメッセージ生成等で使用する。</summary>
        public AvaloniaList<Models.OpenAIService> OpenAIServices
        {
            get;
            set;
        } = [];

        /// <summary>最後に更新チェックを行った時刻（Unixエポックからの秒数）。</summary>
        public double LastCheckUpdateTime
        {
            get => _lastCheckUpdateTime;
            set => SetProperty(ref _lastCheckUpdateTime, value);
        }

        /// <summary>設定の変更を許可する。読み取り専用モードを解除する。</summary>
        public void SetCanModify()
        {
            _isReadonly = false;
        }

        /// <summary>gitの実行ファイルが設定済みかつ存在するかどうかを返す。</summary>
        public bool IsGitConfigured()
        {
            var path = GitInstallPath;
            return !string.IsNullOrEmpty(path) && File.Exists(path);
        }

        /// <summary>
        ///     起動時に更新チェックを行うべきかどうかを判定する。
        ///     同日中に既にチェック済みの場合はfalseを返す。
        /// </summary>
        public bool ShouldCheck4UpdateOnStartup()
        {
            if (!_check4UpdatesOnStartup)
                return false;

            var lastCheck = DateTime.UnixEpoch.AddSeconds(LastCheckUpdateTime).ToLocalTime();
            var now = DateTime.Now;

            if (lastCheck.Year == now.Year && lastCheck.Month == now.Month && lastCheck.Day == now.Day)
                return false;

            LastCheckUpdateTime = now.Subtract(DateTime.UnixEpoch.ToLocalTime()).TotalSeconds;
            return true;
        }

        /// <summary>現在アクティブなワークスペースを取得する。見つからない場合は先頭をアクティブにする。</summary>
        public Workspace GetActiveWorkspace()
        {
            foreach (var w in Workspaces)
            {
                if (w.IsActive)
                    return w;
            }

            var first = Workspaces[0];
            first.IsActive = true;
            return first;
        }

        /// <summary>リポジトリノードを指定の親ノード（またはルート）に追加し、ソートする。</summary>
        public void AddNode(RepositoryNode node, RepositoryNode to, bool save)
        {
            var collection = to == null ? RepositoryNodes : to.SubNodes;
            collection.Add(node);
            SortNodes(collection);

            if (save)
                Save();
        }

        /// <summary>ノードコレクションをグループ優先・名前順にソートする。</summary>
        public void SortNodes(List<RepositoryNode> collection)
        {
            collection?.Sort((l, r) =>
            {
                if (l.IsRepository != r.IsRepository)
                    return l.IsRepository ? 1 : -1;

                return Models.NumericSort.Compare(l.Name, r.Name);
            });
        }

        /// <summary>指定されたIDのリポジトリノードを再帰的に検索する。</summary>
        public RepositoryNode FindNode(string id)
        {
            return FindNodeRecursive(id, RepositoryNodes);
        }

        /// <summary>リポジトリパスに対応するノードを検索し、存在しなければ新規作成して追加する。</summary>
        public RepositoryNode FindOrAddNodeByRepositoryPath(string repo, RepositoryNode parent, bool shouldMoveNode, bool save = true)
        {
            var normalized = repo.Replace('\\', '/').TrimEnd('/');

            var node = FindNodeRecursive(normalized, RepositoryNodes);
            if (node == null)
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

        /// <summary>リポジトリノードを別の親ノードに移動する。</summary>
        public void MoveNode(RepositoryNode node, RepositoryNode to, bool save)
        {
            if (to == null && RepositoryNodes.Contains(node))
                return;
            if (to != null && to.SubNodes.Contains(node))
                return;

            RemoveNode(node, false);
            AddNode(node, to, false);

            if (save)
                Save();
        }

        /// <summary>リポジトリノードをツリーから再帰的に削除する。</summary>
        public void RemoveNode(RepositoryNode node, bool save)
        {
            RemoveNodeRecursive(node, RepositoryNodes);

            if (save)
                Save();
        }

        /// <summary>ノードの名前変更後にその親コレクションを再ソートして保存する。</summary>
        public void SortByRenamedNode(RepositoryNode node)
        {
            var container = FindNodeContainer(node, RepositoryNodes);
            SortNodes(container);
            Save();
        }

        /// <summary>無効な（存在しない）リポジトリノードを自動的に除去する。</summary>
        public void AutoRemoveInvalidNode()
        {
            RemoveInvalidRepositoriesRecursive(RepositoryNodes);
        }

        /// <summary>設定をpreference.jsonファイルに保存する。読み込み中・読み取り専用時はスキップする。</summary>
        public void Save()
        {
            if (_isLoading || _isReadonly)
                return;

            var file = Path.Combine(Native.OS.DataDir, "preference.json");
            using var stream = File.Create(file);
            JsonSerializer.Serialize(stream, this, JsonCodeGen.Default.Preferences);
        }

        /// <summary>preference.jsonから設定を読み込む。ファイルが存在しないか読み込み失敗時はデフォルト値を返す。</summary>
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
            catch
            {
                return new Preferences();
            }
        }

        /// <summary>
        ///     システムのUIカルチャからデフォルトのロケールを自動検出する。
        ///     完全一致→言語コード一致の順で検索し、見つからなければen_USを返す。
        /// </summary>
        private static string DetectDefaultLocale()
        {
            var supported = new HashSet<string>(StringComparer.Ordinal)
            {
                "de_DE", "en_US", "es_ES", "fil_PH", "fr_FR", "id_ID", "it_IT", "ja_JP",
                "ko_KR", "pt_BR", "ru_RU", "ta_IN", "uk_UA", "zh_CN", "zh_TW",
            };

            var culture = System.Globalization.CultureInfo.CurrentUICulture;

            // Try exact match: "ja-JP" → "ja_JP"
            var exact = culture.Name.Replace('-', '_');
            if (supported.Contains(exact))
                return exact;

            var lang = culture.TwoLetterISOLanguageName;

            // Chinese variants need special handling
            if (lang == "zh")
            {
                var name = culture.Name;
                if (name.Contains("Hant") || name.Contains("TW") || name.Contains("HK") || name.Contains("MO"))
                    return "zh_TW";
                return "zh_CN";
            }

            // Find first locale matching the language code
            foreach (var locale in supported)
            {
                if (locale.StartsWith(lang + "_", StringComparison.Ordinal))
                    return locale;
            }

            return "en_US";
        }

        /// <summary>gitの実行ファイルパスが未設定または存在しない場合に自動検出する。</summary>
        private void PrepareGit()
        {
            var path = Native.OS.GitExecutable;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                GitInstallPath = Native.OS.FindGitExecutable();
        }

        /// <summary>シェル/ターミナルが未設定の場合に利用可能なものを自動検出する。</summary>
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

        /// <summary>外部差分/マージツールのコマンドライン引数が未設定の場合にデフォルト値を設定する。</summary>
        private void PrepareExternalDiffMergeTool()
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

        /// <summary>ワークスペースの初期化。空の場合はデフォルトを作成し、復元不要なものはクリアする。</summary>
        private void PrepareWorkspaces()
        {
            if (Workspaces.Count == 0)
            {
                Workspaces.Add(new Workspace() { Name = "Default" });
                return;
            }

            foreach (var workspace in Workspaces)
            {
                if (!workspace.RestoreOnStartup)
                {
                    workspace.Repositories.Clear();
                    workspace.ActiveIdx = 0;
                }
            }
        }

        /// <summary>指定IDのノードをコレクション内で再帰的に検索する。</summary>
        private RepositoryNode FindNodeRecursive(string id, List<RepositoryNode> collection)
        {
            foreach (var node in collection)
            {
                if (node.Id == id)
                    return node;

                var sub = FindNodeRecursive(id, node.SubNodes);
                if (sub != null)
                    return sub;
            }

            return null;
        }

        /// <summary>指定ノードが所属する親コレクションを再帰的に検索する。</summary>
        private List<RepositoryNode> FindNodeContainer(RepositoryNode node, List<RepositoryNode> collection)
        {
            foreach (var sub in collection)
            {
                if (node == sub)
                    return collection;

                var subCollection = FindNodeContainer(node, sub.SubNodes);
                if (subCollection != null)
                    return subCollection;
            }

            return null;
        }

        /// <summary>指定ノードをコレクションから再帰的に検索して削除する。</summary>
        private bool RemoveNodeRecursive(RepositoryNode node, List<RepositoryNode> collection)
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

        /// <summary>無効なリポジトリノードを再帰的に検出して削除する。</summary>
        private bool RemoveInvalidRepositoriesRecursive(List<RepositoryNode> collection)
        {
            bool changed = false;

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

        private static Preferences _instance = null;                                                                        // シングルトンインスタンス
        private static readonly string s_detectedLocale = DetectDefaultLocale();                                              // 起動時に検出されたロケール

        /// <summary>システムから自動検出されたデフォルトロケール。</summary>
        internal static string DetectedLocale => s_detectedLocale;

        private bool _isLoading = true;                                                                                       // 読み込み中フラグ（保存を抑制）
        private bool _isReadonly = true;                                                                                      // 読み取り専用フラグ
        private string _locale = "en_US";                                                                                     // 現在のロケール
        private string _theme = "Default";                                                                                    // 現在のテーマ名
        private string _themeOverrides = string.Empty;                                                                        // テーマカスタマイズJSON
        private string _defaultFontFamily = s_detectedLocale == "ja_JP" ? "Yu Gothic UI" : "Inter";                           // デフォルトフォント
        private string _monospaceFontFamily = s_detectedLocale == "ja_JP" ? "UDEV Gothic JPDOC" : "JetBrains Mono";           // 等幅フォント
        private double _defaultFontSize = 13;                                                                                 // デフォルトフォントサイズ
        private double _editorFontSize = 13;                                                                                  // エディタフォントサイズ
        private int _editorTabWidth = 4;                                                                                      // エディタタブ幅
        private double _zoom = 1.0;                                                                                           // UIズーム倍率
        private LayoutInfo _layout = new();                                                                                   // レイアウト情報

        private int _maxHistoryCommits = 20000;                                                                               // 履歴最大コミット数
        private int _subjectGuideLength = 50;                                                                                 // サブジェクト行ガイド長
        private bool _useFixedTabWidth = true;                                                                                // タブ幅固定
        private bool _useAutoHideScrollBars = true;                                                                           // スクロールバー自動非表示
        private bool _useGitHubStyleAvatar = true;                    // GitHubスタイルアバター使用
        private bool _showAuthorTimeInGraph = false;                    // グラフに作者日時を表示
        private bool _showChildren = false;                             // 子コミット参照の表示

        private bool _check4UpdatesOnStartup = true;                   // 起動時更新チェック
        private double _lastCheckUpdateTime = 0;                       // 最終更新チェック時刻
        private string _ignoreUpdateTag = string.Empty;                 // 無視する更新タグ

        private bool _showTagsInGraph = true;                           // グラフにタグを表示
        private bool _useTwoColumnsLayoutInHistories = false;           // 履歴2カラムレイアウト
        private bool _displayTimeAsPeriodInHistories = false;           // 相対時間表示
        private bool _useSideBySideDiff = false;                        // サイドバイサイド差分
        private bool _ignoreWhitespaceChangesInDiff = false;            // 空白変更の無視
        private bool _useSyntaxHighlighting = false;                    // シンタックスハイライト
        private bool _enableDiffViewWordWrap = false;                   // 差分の行折り返し
        private bool _showHiddenSymbolsInDiffView = false;              // 不可視文字の表示
        private bool _useFullTextDiff = false;                          // 全テキスト差分
        private int _lfsImageActiveIdx = 0;                             // LFS画像差分モード
        private int _imageDiffActiveIdx = 0;                            // 画像差分モード
        private bool _enableCompactFoldersInChangesTree = false;        // フォルダ折りたたみ

        private Models.ChangeViewMode _unstagedChangeViewMode = Models.ChangeViewMode.List;   // 未ステージ表示モード
        private Models.ChangeViewMode _stagedChangeViewMode = Models.ChangeViewMode.List;     // ステージ済み表示モード
        private Models.ChangeViewMode _commitChangeViewMode = Models.ChangeViewMode.List;     // コミット変更表示モード
        private Models.ChangeViewMode _stashChangeViewMode = Models.ChangeViewMode.List;      // スタッシュ変更表示モード

        private string _gitDefaultCloneDir = string.Empty;              // デフォルトクローン先
        private int _shellOrTerminalType = -1;                          // シェル/ターミナル種別
        private uint _statisticsSampleColor = 0xFF00FF00;               // 統計サンプルカラー
    }
}
