# Komorebi の設計

Komorebi は SourceGit を基にした、Windows・macOS・Linux 向けの Git GUI クライアントである。Git CLI を通じてローカルリポジトリとリモートを操作し、履歴・差分・作業ツリーを可視化する。利用方法は [README.md](README.md)、開発規約と検証コマンドは [AGENTS.md](AGENTS.md)、実装上の注意は [docs/PITFALLS.md](docs/PITFALLS.md) を参照する。

## コンポーネントと境界

| コンポーネント | 責務・境界 | 主な実装 |
|---|---|---|
| アプリ起動 | 更新フック、データ保存先・ログ初期化、通常 GUI と Git のリベースエディタ起動の分岐 | `src/App.axaml.cs`、`src/App.Commands.cs` |
| UI | Avalonia の View とバインディング、入力・描画・ウィンドウ操作 | `src/Views/`、`src/Converters/` |
| テキストエディタ | テキスト描画・編集、TextMate による文法解析と色付け。Git 差分の解釈と表示モードはアプリ側が担当 | `depends/AvaloniaEdit/src/`、`src/Models/TextMateHelper.cs`、`src/Views/TextDiffView.axaml.cs`、`src/Views/Blame.axaml.cs` |
| アプリ状態 | Launcher のタブ、Repository の履歴・作業コピー・スタッシュ、Popup の検証と操作進捗 | `src/ViewModels/` |
| Git 実行 | 引数・作業ディレクトリ・SSH 環境の構築、プロセス実行、出力解析・中断 | `src/Commands/Command.cs` と各派生コマンド |
| データモデル | コミット・参照・差分・グラフ、ファイル監視、リポジトリ UI 状態 | `src/Models/` |
| OS 連携 | データ保存先、外部ツール・ターミナル等のプラットフォーム差異 | `src/Native/OS.cs` の `IBackend` と各 OS 実装 |
| AI 生成 | サービス設定と差分取得ツールを使ったコミットメッセージ生成 | `src/AI/Agent.cs`、`OpenAISdkStrategy.cs`、`AnthropicHttpStrategy.cs`、`ChatTools.cs` |
| 配信 | 他 OS・standalone の CI パッケージ作成と、CI の Windows 署名（Sigilune）、R2 配信 | `scripts/release-local.ps1`、`.github/workflows/release.yml` |

## UI の構成

`ViewModels` は CommunityToolkit.Mvvm の `ObservableObject`、入力検証を持つ Popup は `ObservableValidator` を基底とする。View は型を指定した compiled binding を使い、Models と Converters を介して状態を表示する。

Launcher は `LauncherPage` のタブと `ActivePage` を管理する。Repository は `Open()` 時に Histories・WorkingCopy・StashesPage の VM を作り、`SelectedViewIndex` に応じて `SelectedView` を切り替える。Preferences は全体設定、InitSetup は初回の言語とデフォルトクローン先を担当する。

タイトルバーはタブとページ切替に限定する。WelcomeToolbar は Clone/Open/Terminal・ワークスペース・全体操作を表示する。Repository 左側のブランチバーにブランチ選択と Fetch/Pull/Push、右側のコンテンツツールバーにビュー切替・検索・ビュー固有操作・設定・ワークスペース・全体操作を置く。全体のキーボードショートカットは `Launcher.axaml.cs` が扱う。Fetch/Pull/Push の `ModifierButton` はクリック、Enter・Space、UIA を共通の Click に接続し、Ctrl 操作も保持する。

## データフローと寿命

1. View の操作を ViewModel が受け取り、Git コマンドを構築して実行する。Popup は入力検証と進捗ログを担当し、中断可能な処理では操作スコープのトークンをコマンドに渡す。
2. Git の出力を Models に変換し、Repository が履歴・ブランチ・作業コピー等を更新する。ファイル監視も更新を起動する。非同期更新ではキャンセルを確認し、UI スレッドに結果を反映することで古い取得結果や Close 後の反映を防ぐ。
3. Repository は Histories・WorkingCopy・StashesPage の VM を保持する。Launcher と Repository の `ContentControl + DataTemplate` が選択中の VM を表示する。詳細・比較の独立ウィンドウは対応する VM を表示する。
4. 全体設定は `Preferences` が DataDir の `preference.json` に保存する。API キーの保存契約は[設定と資格情報](#設定と資格情報)に記載する。Git 固有の設定は Git config とコマンド層を通じて扱い、UI 状態とは分離する。
5. AI 生成では変更一覧と現在ブランチ等をプロンプトへ組み込み、`ChatTools` 経由で差分を取得して設定先サービスへ送る。`Agent` はプロバイダー別 Strategy に委譲し、結果をコミットメッセージ欄へ返す。

## 不変条件と採用済み設計判断

- **Git CLI を操作の正本にする。** GUI は Git の結果と終了状態に従う。Git の導入やバージョン差への対応が必要になる一方、既存の認証・リポジトリ設定を利用できる。
- **表示を切り替えても VM の状態を保持する。** View の生成・再利用は ContentControl に任せ、上流と整合する構造を使う。全 View の常駐キャッシュに伴うレイアウト問題を避ける代わりに、保持すべき状態は VM 側が担う。同型 VM 間では View が再利用されるため、DataContext 変更で VM のイベント購読を付け替える。
- **SSH 鍵選択と引数の引用を分離する。** リモート個別 → グローバル → ssh-agent/config の順に解決する。旧 `__NONE__` 値の明示的なフォールバック拒否は読み取り互換として保持する。SSH のシェル用引用と Git 引数用の引用は別の処理である。
- **プロセスの中断を共通化する。** `Native.CommandCancellation` が OS 差を吸収し、キャンセル済みの結果は成功として扱わない。検証手順は [AGENTS.md](AGENTS.md#adding-a-new-git-command) を参照する。
- **バイナリ表示は必要な範囲を読む。** `Models.BinaryFile` はバッファを用い、履歴から抽出した一時ファイルの削除も Dispose に結び付ける。全内容の常時メモリ保持を避ける代わりに、表示中のファイル資源を管理する。
- **起動診断は通常ロガーから独立させる。** ロガー初期化前とログを書けない終了を補足する。出力と単一起動の状態分岐は[起動診断と単一起動](#起動診断と単一起動)に記載する。
- **更新先を固定し、配信物の実体を検証する。** `Preferences.CanonicalUpdateBaseUrl` を正本とし、JSON からの更新 URL 上書きを受け付けない。Velopack の取得・適用経路と R2 の配信経路を接続する。Windows は Certum クラウド署名（Sigilune、TOTP の窓を共有しないよう 1 ジョブで全 RID）のため、`release.yml` の `windows-release` ジョブが `r2-upload` の後に `release-local.ps1` で配信し、`velopack.yml` は Windows 更新フィードを生成しない。アップロード前に最終パッケージの版・CPU・manifest と実体の整合を確認し、CI ではソース SHA、Windows では署名も確認する。`r2-upload` の清掃は Windows 成果物を全世代保護し、Windows 署名リリース側は Windows だけを清掃する。相手の manifest 更新前に新パッケージを誤削除しないための責務分離である。Windows の公開処理は R2 書込みと固定 URL 全件のパージ API の受付成功を確認してから清掃する。公開後の配信内容取得・ハッシュ照合・伝播待ちは行わず、清掃時の他チャンネル manifest 取得は保持対象の保護に使う。実行コマンドと保持条件は [AGENTS.md](AGENTS.md#cicd) を参照する。
- **テーマと翻訳を資源として切り替える。** `Themes.axaml` の色・ブラシと各 locale 辞書を利用し、英語辞書を翻訳キー集合の基準とする。
- **エディタはソースを同梱し、アプリ側の拡張と分離する。** AvaloniaEdit の上流安定版を基に、日本語 IME・描画余白・矩形選択と Avalonia 12 への対応を保持する。NuGet のエディタへ置き換えず、これらの差分を維持するために直接追跡する。`TextMateHelper` は追加文法とアプリのテーマを供給し、同梱側は文書スナップショットと解析・描画資源を管理する。差分・Blame の View は読み込み時に TextMate の `Installation` を作成し、アンロード時に Dispose してイベント購読と解析資源を解放する。統合元の情報は [同梱エディタの README](depends/AvaloniaEdit/README.md)、更新時の制約・検証は [AGENTS.md](AGENTS.md) を参照する。
- **Velopack は参照される更新処理だけを AOT で保持する。** アセンブリ全体を `TrimmerRootAssembly` に指定すると、未使用の旧 COM API まで AOT 解析対象になるため、全体保持は行わない。更新フィードの JSON 読み取り互換性は `LibraryCompatibilityTests` で検証する。

## 差分読込と画像の寿命

`DiffContext` は読み込みごとに要求番号を付け、UI スレッドで最新の要求だけを反映する。`ImageSource` は存在しない作業ツリーファイルを画像なし・サイズ 0 として返すため、削除画像も旧側を表示できる。読込失敗時は最新要求の表示をクリアし、ログと通知へエラーを渡す。反映されない古い結果や途中で失敗した結果の Bitmap は破棄し、別ファイルの古い差分表示や資源の残留を防ぐ。

## クローンの復旧と認証境界

`ViewModels.Clone.Sure()` は宛先を確定し、単一の中断可能な操作内で `Commands.Clone.CloneAsync()` にクローン・再帰的サブモジュール初期化・長いパス復旧を委譲する。失敗または中断時にはリポジトリの登録・表示へ進まない。

Windows の `Filename too long` に限り、実行前に宛先が未使用で、取得済みの `.git` があり、中断されていなければ、一度だけ復旧する。Git for Windows が起動初期に longpaths をキャッシュするため、`core.longpaths=true` をグローバル設定へ保存した上で、コマンドにも `-c core.longpaths=true` を渡す。親 checkout の失敗は `reset --hard HEAD`、サブモジュールの失敗は `submodule update --init --recursive --checkout --force` で取得済みの内容を再展開する。再 clone と既存の非空宛先への破壊的な復旧は行わない。通常成功・無関係なエラー・中断では設定を変更しない。

独立したサブモジュール更新では、親リモート専用の `SSHKey` と CodeCommit の credential helper 抑止を一時的に解除する。別ホストに親の認証設定を持ち出さず、親プロセスの環境とホスト別設定を利用する。コマンドの引数・作業ディレクトリ・認証指定は処理後に復元する。

## リポジトリの信頼境界

起動・追加・オープン時に Git が所有権エラーを返した場合、`Launcher` / `OpenLocalRepository` は `TrustRepository` で対象と理由を表示し、利用者の確認後に `AddSafeDirectory` で Git のグローバル `safe.directory` へ登録する。登録成功後にルートを再照会してリポジトリを追加・表示し、失敗・中断時は先へ進めない。

`Models.SafeDirectories` は選択パスから最も近い `.git`（worktree のファイルを含む）または bare リポジトリの境界を求める。stderr の提案はその境界と一致する場合だけ採用し、`*` や `/*` による範囲拡大を認めない。Windows の UNC パスでは Git の `%(prefix)/` 表現を保持する。診断文字列に埋め込まれた別パスを信頼対象にしないためである。

## 設定と資格情報

`Preferences` の JSON 保存は UI 状態と AI サービス設定を扱う。`AI.Service` の公開 API キーと JSON に書く `ProtectedApiKey` は分離され、`ApiKeyProtector` が AES-GCM で暗号化する。追加 HTTP ヘッダーも `ExtraHeaders` と `ProtectedExtraHeaders` を分離し、Authorization 等を含む設定全体へ同じ保護・暗号文保持・旧平文移行を適用する。鍵は DataDir の `ai-api-key.key` に置き、Windows は DPAPI CurrentUser、Unix はファイル権限で保護する。

既存鍵の I/O・形式・DPAPI 読込失敗では鍵を再生成せず、復号できない暗号文も保持する。これにより一時障害中に設定を保存しても、元の鍵が再び読めるようになった後に復旧できる。鍵は同じディレクトリの一時ファイルへ書き込み・権限制限・flush を済ませてから確定し、旧鍵の移行では置換、新規生成では上書き禁止とする。同時生成時は先に確定した鍵を読むことで、唯一の鍵の破損や別プロセスによる上書きを防ぐ。旧平文の暗号化移行や新しい暗号化に失敗しても平文は書き戻さない。利用者が API キーまたは追加ヘッダーを明示的に空へ変更した場合は、その保持した暗号文も削除する。

## 起動診断と単一起動

`Models.Logger` (SuperLightLogger) は「初期化後」かつ「非同期バッファ経由」でしか書けないため、起動時クラッシュを 2 種類取りこぼす。`src/Models/StartupDiagnostics.cs` がその穴を埋める。`Bind()` 前の同期ログは `%TEMP%/Komorebi`、その後の同期ログとマーカーは通常ログと同じディレクトリに出力する。Windows の通常モードでは `%LOCALAPPDATA%/Komorebi/logs`、それ以外は `<DataDir>/logs` を使う。

Windows の旧ログフォルダーに残った起動マーカーも収集し、稼働中のプロセスのものは次回に残す。

1. **ロガー初期化前の失敗** (Velopack フック / `SetupDataDir` / `Logger.Initialize` 自体) → `StartupDiagnostics.WriteFatal()` が Logger 非依存の同期書き込みで `Komorebi_startup_crash.log` に残す (DataDir 確定前は `%TEMP%/Komorebi`)。
2. **プロセス内で何も書けない死に方** (Native AOT のアクセス違反、ランタイム abort、強制終了、電源断) → 起動中は `logs/startup-<pid>.marker` を置き、`MarkStage()` で到達ステージを同期更新する。UI スレッドが最初のアイドル (`DispatcherPriority.ApplicationIdle`) に到達したら `Stabilizing` へ遷移し、**60 秒の安定化観察期間**を経てから `MarkCompleted()` でマーカーを削除する（起動数秒後のサイレントクラッシュも検出するため）。正規終了経路 (`App.Quit` / `desktop.Exit` / IPC 転送成功後の終了 / リベースエディタ終了) では観察期間中でも即座に `MarkCompleted()` して誤検出を防ぐ。次回起動時に残留マーカーを見つけたら「前回の起動が完了しませんでした（到達ステージ付き）」を通常ログ (Warning) とブートストラップログの両方へ記録する。PID 再利用の誤検出は `startedTicks` (プロセス開始時刻) の一致判定で防ぐ。

ステージは `StartupStage` enum (ProcessStart → VelopackHook → DataDir → LoggerInit → LaunchModeCheck → AvaloniaStart → AppInitialize → FrameworkInitialized → WaitingFirstIdle → Stabilizing → Completed)。`Logger.LogCrash` のレポートにも現在ステージが入る。

`Environment.Exit` は `finally` を走らせないため、正常なリベースエディタ終了では診断の完了と Logger の破棄を明示している。

通常 GUI 起動では `TryLaunchAsNormal` 冒頭で `Models.FontWarmup.Run()` がフォールバックフォントの GlyphTypeface を一括生成する（SkiaSharp 3.x の DirectWrite 読み取り × GC ファイナライザ競合クラッシュの軽減。詳細は [docs/PITFALLS.md](docs/PITFALLS.md)）。

`IpcChannel` はユーザーと DataDir からパイプ名を作り、`process.lock` の排他ロックと現在ユーザー専用の名前付きパイプで単一起動を管理する。状態は `FirstInstance`・`LockUnavailable`・`Failed` を区別する。通常 GUI の `App` はロック競合時に最大 3 回転送を試み、失敗ごとにロックを再取得する。相手が終了した場合は自分が最初のインスタンスとして起動できる。パイプ初期化失敗時は取得済みロックを解放する。

IPC 転送成功だけを正常な二重起動の終了（exit 0）と扱う。転送不能または初期化失敗では `IpcChannel.Startup` の fatal 診断と exit 1 を残し、起動完了マーカーを正常終了として消さない。

上流との機能差と同期時の採否は [docs/UPSTREAM-SYNC.md](docs/UPSTREAM-SYNC.md) に記録される。上流追従の保守性と Komorebi 固有機能の維持を両立するため、採否の作業規約は AGENTS.md に集約する。
