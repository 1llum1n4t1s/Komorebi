# 過去分を含む上流取り込み記録（2026-09-06）

対象: 共通祖先 `01feffa170ba2329e41531e527061d2a746fc41f` から `upstream/master = 5c53601a`（v2026.19）までの非mergeコミット673件。共通祖先より前は共有済みの履歴。

直前の読取調査を受け、依頼された `/gogo` の二軸判定で機能・修正を手動移植した。SHA単位のcherry-pickや673件すべての新規取り込みを意味しない。直前バッチの未コミット差分も保持。翻訳は後日精査とし、新機能に必要なキーのみ英語で補完した。

## 判定集計

| 判定 | SHA数 |
| --- | ---: |
| 反映済み | 205 |
| 対象外 | 96 |
| 却下 | 82 |
| 最小採用 | 78 |
| 後日精査 | 69 |
| 差し替え採用 | 53 |
| 採用 | 37 |
| 採用済み | 21 |
| 個別管理 | 16 |
| 部分採用・保留 | 7 |
| 未判定 | 7 |
| 採用・実機未検証 | 2 |

## グループ別の根拠

問題が成立した対象は、同じAPI・呼出し元・状態分岐へ横展開した。とくに参照名、パッチ生成、プロセス中断、非同期詳細VMの寿命は複数経路を修正。既存実装で保護されている経路は維持した。機能追加は今回の明示依頼として評価し、単なるスタイル変更と区別した。

| ID | SHA数 | 判定 | 根拠・採用範囲 | 主な接続先 |
| --- | ---: | --- | --- | --- |
| EXISTING | 177 | 反映済み | 後続修正・独自実装を含め既存コードに対応あり。全行一致や個別の実機検証を意味しない。 | `src` |
| WIP | 21 | 採用済み | 直前バッチの20件を保持。bc3ee043は後続修正に包含。 | `docs/UPSTREAM-SYNC.md` |
| META | 96 | 対象外 | 上流の版番号・CI・配布文書。Komorebiの署名・R2配布契約を維持。 | `.github/workflows` |
| LOCALES | 69 | 後日精査 | 依頼どおり過去の翻訳・追加言語69件は未取り込み。新機能に必要なキーだけ英語で全17言語へ補完。 | `src/Resources/Locales` |
| DEPENDENCY | 16 | 個別管理 | Avalonia 12系・vendored AvaloniaEditの実効依存を維持。上流11系への巻き戻しは行わない。直前バッチの整合修正は保持。 | `src/Komorebi.csproj` |
| STYLE | 44 | 却下 | 名前空間・コメント・単なる抽出や改名は不具合を示さない。Upstream-Faithful Policyに従い既存書式を維持。 | `AGENTS.md` |
| SAFETY | 17 | 反映済み | 独自のガード・非同期処理・安全なコマンド経路が存在し、同じ修正の重複適用は不要。 | `src/Commands/Command.cs` |
| AI | 7 | 部分採用・保留 | Apply操作は既存。SDKとAnthropic経路・リポジトリ検証は維持。モデル一覧取得はAzureのdeployment名などプロバイダー契約が異なり、全プロバイダーでの適合検証が未了。 | `src/AI/Service.cs` |
| AI_REASONING | 6 | 反映済み | SDKおよび独自Anthropic戦略による応答処理を維持。上流単一HTTP経路へ置換する必要はない。 | `src/AI/OpenAISdkStrategy.cs` |
| APP | 5 | 却下 | 起動診断・Velopack・独自通知など既存の起動契約に合わせる。上流起動処理への一括置換は互換性を損なう。 | `src/App.axaml.cs` |
| HISTORY_LIFETIME | 15 | 最小採用 | 複数選択のSHAによる復元・20件超のリセット・古い詳細VMの破棄を移植。ContentControlとVMキャッシュを維持し、閉じた後の非同期更新を抑止。 | `src/ViewModels/Histories.cs` |
| STANDALONE | 14 | 最小採用 | コミット詳細と比較の別ウィンドウ、詳細の折りたたみを移植。独立VMを閉じる際に破棄し、ロード中の終了競合を修正。 | `src/Views/CommitDetailStandalone.axaml.cs` |
| COMPARE_COMMITS | 2 | 採用 | 左右だけに存在するコミット一覧とcherry-pick操作。読み込み中の左右交換を抑止。 | `src/ViewModels/Compare.cs` |
| HEAD_ACTIONS | 2 | 却下 | 専用のreword/drop等を削除してinteractive rebaseへ統合する必然性はない。既存のHEAD操作とガードを維持。 | `src/ViewModels/Histories.cs` |
| GRAPH | 6 | 差し替え採用 | 全体・現在・選択・first-parentのハイライトを導入。実際のIsMergedと表示用フラグを分離し、既存の最適化済みパーサーを維持。 | `src/Models/CommitGraph.cs` |
| CHART | 13 | 却下 | 独自LiveCharts描画を維持。上流の自作チャートへの置換は描画基盤の変更であり、欠陥修正としては不要。ブランチ絞り込みは別途採用。 | `src/ViewModels/Statistics.cs` |
| STATISTICS_FILTER | 2 | 採用 | 統計の対象ブランチを選択可能にした。非同期結果の世代を照合し、古い結果の上書きを防止。 | `src/Commands/Statistics.cs` |
| GITFLOW | 13 | 差し替え採用 | gitflow-nextの検出と設定、開始点、keep/rebaseを移植。従来版との互換性・引数引用・設定再読込時の初期化を維持。 | `src/Models/GitFlow.cs` |
| RAW_DIFF | 9 | 最小採用 | 非UTF-8の生バイトと改行末尾を保持して部分ステージ／逆適用。既存の3種パッチ生成経路を修正し、上流のPatchGenerator抽出だけは省略。 | `src/Models/DiffResult.cs` |
| FILEMODE | 1 | 最小採用 | モード変更の説明ツールチップを追加。文字列のモード・既存バッジを維持し、整数化と専用描画クラスは導入不要。 | `src/ViewModels/DiffContext.cs` |
| MACOS_TITLE | 7 | 未判定 | 独自タイトルバーとAvalonia 12でtraffic lightsの位置・全画面を実機検証できていない。Objective-Cフレーム操作は未導入。macOSで通常／全画面／復帰を確認して再開。 | `src/Native/MacOS.cs` |
| BOOKMARK | 3 | 却下 | 既存ComboBoxで色選択可能。独自描画への置換は機能欠落の修正ではなく、標準のキーボード操作を変える理由もない。 | `src/Views/EditRepositoryNode.axaml` |
| FONTS | 3 | 却下 | システムフォントとロケール別フォールバックが製品の既定。上流のJetBrainsフォント同梱・リソース分割は適用しない。 | `src/Models/InstalledFont.cs` |
| AVALONIA_PROPERTIES | 3 | 却下 | NullオブジェクトやStyledからDirectへの一括変更はRider警告／構造整理。現在の12系でビルド・テストが通り、必要な通知だけを機能側に実装。 | `src/Views/Histories.axaml` |
| MICA_REMOVED | 2 | 反映済み | 上流のMica削除相当は既存に反映。 | `src/Views/ChromelessWindow.cs` |
| UPDATE | 2 | 却下 | Velopack・R2配信を維持。上流独自更新へ戻すと出荷済み更新契約を破る。 | `src/App.axaml.cs` |
| CLI_PATH | 2 | 採用 | --historyの相対パスをslash区切りへ正規化。Git自体はWindowsのbackslashでも動くが、詳細ファイルの文字列フィルターには正規化が必要。 | `src/App.axaml.cs` |
| MERGE_TEST | 2 | 最小採用 | merge-tree --write-treeの下限をGit 2.38に合わせ、不要なmerge-base事前照会を除去。失敗と競合の判定は維持。 | `src/ViewModels/Merge.cs` |
| WRAPPING | 4 | 差し替え採用 | メッセージTextPresenterの幅を実際のScrollViewer viewportへ合わせ、旧幅による折り返しずれを抑止。Avalonia 12実画面の選択・IMEは未検証。 | `src/Views/CommitMessageToolBox.axaml.cs` |
| IPC | 4 | 最小採用 | ユーザーと正規化したDataDirから短いハッシュのpipe名を生成。ロックファイルの寿命は維持し、Unixでのunlink競合を避ける。 | `src/Models/IpcChannel.cs` |
| XDG | 4 | 最小採用 | 新規Linux環境は絶対パスのXDG_CONFIG_HOMEへ。AppImageと既存の2保存先を優先し、旧設定の暗黙移動を廃止。config/cache/stateの完全分割は未導入。 | `src/Native/Linux.cs` |
| CANCEL | 7 | 差し替え採用 | 通信ポップアップ6種から実プロセスへ中断を伝播。Windowsはプロセスツリーkill、Unixは専用process groupを使える場合TERM後kill。終了確認後に完了しキャンセルを成功扱いしない。 | `src/Native/CommandCancellation.cs` |
| SSH_HELPER | 5 | 最小採用 | キー一覧・ED25519/RSA生成を追加。既存キーの上書き拒否、ArgumentList、生成中の重複／閉じる操作抑止、所有モーダルのエラー表示を実装。既存SSHKeyPickerを維持。 | `src/ViewModels/SSHKeyGenerator.cs` |
| CONFLICT_BINARY | 3 | 差し替え採用 | バイナリ／テキスト／解決済み／照会失敗を分離。git失敗を解決済みと扱わず、未解決テキストだけマージ可能。 | `src/Commands/QueryConflictFileState.cs` |
| REFNAME | 3 | 差し替え採用 | Git規則に沿う共通参照名検証を導入し、branch/tag/gitflow/worktree/pushへ横展開。空欄の自動命名と既存衝突チェックを保持。 | `src/Models/RefName.cs` |
| TOOLBOX | 2 | 採用 | JetBrains Toolboxの製品コードによる検出と追加アイコンを導入。 | `src/Models/ExternalTool.cs` |
| STREAM | 1 | 採用 | SaveRevisionFileの入力ストリームを早期例外時にも破棄。ほかの読取経路は既存usingで保護済み。 | `src/Commands/SaveRevisionFile.cs` |
| FILE_PICKER | 1 | 採用 | 全ファイル指定3か所を*へ統一し、拡張子のないファイルも対象化。 | `src/Views/Preferences.axaml.cs` |
| SELECTION | 2 | 採用 | フォルダー選択と展開を同期し、ファイル検索結果を選択。空のRowsを選択しないガードを追加。 | `src/Views/RevisionFileTreeView.axaml.cs` |
| HEX | 11 | 差し替え採用 | バイナリ16進ビューを追加。範囲・EOF・負値・縮小ファイルを扱い、一時ファイルとストリームを所有権に沿って破棄。テーマ色とシステムフォントへ適応。 | `src/Models/BinaryFile.cs` |
| GRAMMARS | 3 | 採用 | Erlang・OCaml lex/yacc・Swiftの構文定義5ファイルとライセンスを追加。 | `src/Models/TextMateHelper.cs` |
| AI_OPTIONS | 2 | 採用 | 現在ブランチを生成プロンプトへ追加。OpenAI系に任意のreasoning effort設定を追加し、既定は未指定、Anthropicには送らない。 | `src/AI/OpenAISdkStrategy.cs` |
| NO_VERIFY | 2 | 採用 | push/rebaseに利用者が選ぶno-verifyを追加。既定falseを維持。実際のpush/rebaseは実行していない。 | `src/Commands/Push.cs` |
| REMOTE_TAGS | 1 | 採用 | リモート追加時のfetch without tagsを追加。 | `src/ViewModels/AddRemote.cs` |
| FILTER_BAR | 3 | 差し替え採用 | 履歴フィルターの折りたたみ・要約・無効参照表示を導入。統合ツールバー構造と既存の参照名サニタイズを保持。 | `src/ViewModels/Repository.cs` |
| COMMIT_GUIDE | 3 | 最小採用 | 本文80桁ガイドと等幅フォントを導入。実フォントサイズを用い、既存の件名終端表示は保持。 | `src/Views/CommitMessageToolBox.axaml.cs` |
| MACOS_INTEGRATION | 2 | 採用・実機未検証 | 追加ターミナルとフォルダーactivationを移植。Windows上のビルド確認のみで、macOSでのopen操作は未検証。 | `src/App.axaml.cs` |
| NAVIGATION | 2 | 採用 | マウス戻る／進むと、親の隣にサブモジュールを開く処理を追加。既存タブは再利用。 | `src/ViewModels/Launcher.cs` |
| SUBMODULE_DIFF | 5 | 最小採用 | サブモジュール参照の一括照会・未初期化時fallbackを追加。mode160000で識別し、画像のworktree/index/revisionと-Rの選択も修正。 | `src/Commands/QuerySubmoduleRevision.cs` |
| STATUS_CANCELLATION | 1 | 差し替え採用 | statusの既存共通読取経路で実プロセスをキャンセル。Repository更新のtokenを接続。書込ワークフローの呼出しは既定のまま。 | `src/Commands/QueryLocalChanges.cs` |
| COMPACT_REFS | 2 | 採用 | 単一リモート時の名前省略・共通アイコンを追加。既存のヒットボックスと線形処理を維持。 | `src/Views/CommitRefsPresenter.cs` |
| BLAME_UI | 3 | 最小採用 | SHAのヒットボックスを描画時に記録。実際の移動後座標に合わせ、古い矩形を消去。残りのテンプレート整理は不要。 | `src/Views/Blame.axaml.cs` |
| CHILDREN | 1 | 却下 | 子コミット表示の削除は既存機能を減らす。ShowChildren設定と表示を保持。 | `src/ViewModels/Preferences.cs` |
| RECENT_MESSAGES | 1 | 却下 | 保存場所の変更に既存データ移行がない。RepositorySettingsの履歴上限・正規化は実装済みで、履歴を失う置換は不要。 | `src/Models/RepositorySettings.cs` |
| DELETE_TRACKING | 1 | 却下 | 追跡リモート削除の確認と選択は既存経路に存在。確認UIの再構成だけでは追加の欠陥修正にならない。 | `src/ViewModels/DeleteBranch.cs` |
| REMOTE_AUTOFETCH | 1 | 最小採用 | リモート単位の除外を追加。大文字小文字を区別するリモート名を保持し、全リモート／既定リモートという既存設定を尊重。 | `src/ViewModels/Repository.cs` |
| COPY_FEEDBACK | 2 | 採用 | SHAコピー成功時だけチェック表示。再コピー・DataContext切替・Unloaded時のタイマーと非同期結果を処理。 | `src/Views/CommitBaseInfo.axaml.cs` |
| COPY_SHA | 2 | 採用 | 親SHAとファイル履歴の複数SHAコピーを追加。クリック時の選択を確定。 | `src/Views/FileHistories.axaml.cs` |
| WELCOME_KEYS | 2 | 最小採用 | Left/Rightによるツリー移動を追加。Enter/Deleteは既存ListBoxのスコープで保持。 | `src/Views/Welcome.axaml.cs` |
| WORKTREE_SEARCH | 2 | 差し替え採用 | 検索可能なブランチ選択と名前入力時の追跡候補選択を導入。新規／既存を再切替した際に空選択になる問題も修正。 | `src/ViewModels/AddWorktree.cs` |
| HOTKEYS | 5 | 最小採用 | Clone/Open、branch/tag作成、詳細折りたたみ／別窓のキーとヘルプを整合。修飾キー完全一致で競合を解消。 | `src/Views/Launcher.axaml.cs` |
| CLOSE_KEYS | 2 | 採用 | 確認ダイアログのEscape、サブウィンドウCtrl/Cmd+W。Launcherのタブ閉じる挙動は維持。 | `src/Views/ChromelessWindow.cs` |
| TOOLBAR | 1 | 却下 | 統合WelcomeToolbar／Repositoryツールバーは明示された独自設計。上流の分割構造へ戻さない。 | `src/Views/Repository.axaml` |
| TAB_SCROLL | 2 | 反映済み | タブスクロールは独自実装で処理済み。 | `src/Views/LauncherTabBar.axaml.cs` |
| GRID_HEADER | 1 | 却下 | 標準DataGridを利用し、上流の独自resizerは存在しないため修正対象外。 | `src/Views/Histories.axaml` |
| THEME_OVERRIDES | 1 | 却下 | JSONによる既存テーマ上書きの契約を維持。 | `src/Models/ThemeOverrides.cs` |
| MOVE_GROUP | 2 | 採用 | 移動先一覧の高さ制限と初期選択を追加。 | `src/Views/MoveRepositoryNode.axaml` |
| SCAN_PROGRESS | 1 | 採用 | 検出済みリポジトリ登録中の進捗表示を追加。共通走査はcallbackを省略可能にして保持。 | `src/ViewModels/ScanRepositories.cs` |
| POPUP_FOCUS | 3 | 最小採用 | 選択肢の矢印キー操作とポップアップ内Tab循環を追加。初期ラジオ同期とキャンセル可能な操作を保持。 | `src/Views/DealWithLocalChangesMethod.axaml.cs` |
| MULTI_BRANCH_DELETE | 1 | 採用 | 複数ローカルブランチ削除にforce選択を追加。既定false、リモート削除には混ぜない。 | `src/ViewModels/DeleteMultipleBranches.cs` |
| SSH_AGENT | 1 | 最小採用 | 明示SSHキー使用時のみAddKeysToAgent=yes。既存の-Fによる鍵分離とグローバルfallback／legacy sentinelを維持。 | `src/Commands/Command.cs` |
| REMOTE_SWITCH | 1 | 採用 | 空URL時の初期表示を非表示に修正。CodeCommit例外を保持。 | `src/Views/RemoteProtocolSwitcher.axaml.cs` |
| INLINE_LIMIT | 1 | 採用 | インライン差分の最大チャンク数を4から16へ。既存の長大行制限は保持。 | `src/Models/DiffResult.cs` |
| PREVIEW_SYNTAX | 1 | 最小採用 | ファイルプレビューは常時構文強調。使用中のconverterは保持し、XMLでない.slnをXML扱いする部分は省略。 | `src/Views/RevisionFileContentViewer.axaml.cs` |
| FILTER_PLACEHOLDER | 1 | 反映済み | 統合ツールバーのWorkingCopy検索欄に既存PlaceholderTextがある。 | `src/Views/Repository.axaml` |
| NOTIFICATIONS | 1 | 却下 | App.RaiseExceptionとモーダルAlertの独自経路を維持。 | `src/App.axaml.cs` |
| COMMIT_INPUT | 1 | 採用 | コミット入力にCtrl+Insertコピーを明示。既存の補完キー操作を保持。 | `src/Views/CommitMessageToolBox.axaml.cs` |

## 検証と限界

- Windows / .NET 10 の全テスト: 1,709件成功、失敗0、スキップ0（最終コード変更後に再実行済み）。
- `dotnet format --verify-no-changes src/Komorebi.csproj`、17言語のlocalization-check、変更XAMLのText/Brush/Icons/Fonts参照、`git diff --check`: すべて成功。
- CP932の実Git部分ステージ・逆適用をLF/CRLF各ケースで検証。参照名、グラフ、履歴選択、IPC名、実プロセス中断、AI設定の回帰テストを追加。
- GUIの実画面・IME・clipboard操作、異なる版の同時起動IPC、macOS/Linuxの実行、Native AOT、gitflow-next実コマンド、実SSH鍵生成、AI外部サービス呼出しは未実施。Windowsビルド成功をこれらの確認と同一視しない。
- Linuxのconfig/cache/state完全分割、IPCロック保存先の全面変更、AIモデル一覧自動取得、macOSタイトルバー位置は未採用または未判定。既存設定やプロバイダー契約を検証できる環境から再開する。
- commit / push / release / deploy は実施していない。

## 外部契約の確認

- [Git merge-tree 2.38](https://git-scm.com/docs/git-merge-tree/2.38.0): merge試験のバージョン条件。
- [Git参照名の規則](https://git-scm.com/docs/git-check-ref-format): 共通参照名検証。
- [git-flow-next commands](https://git-flow.sh/docs/commands/): コマンド互換性。
- [XDG Base Directory Specification](https://specifications.freedesktop.org/basedir/latest/): 絶対パスと既定の設定ディレクトリ。
- [OpenAI Chat Completions](https://developers.openai.com/api/reference/resources/chat/subresources/completions/methods/create): reasoning effortはモデル依存。既定未指定を維持。

## SHA別の台帳

各行の判定はグループの採用範囲を指す。最小採用・差し替え採用は元コミット全行の反映ではない。

| SHA | 日付 | ID | 判定 | 上流の件名 |
| --- | --- | --- | --- | --- |
| `82cedf85` | 2026-03-30 | EXISTING | 反映済み | feature: supports to exclude modifed/deleted files while discarding local changes (#2226) |
| `3e364f75` | 2026-03-30 | META | 対象外 | doc: Update translation status and sort locale files |
| `66e51ba7` | 2026-03-30 | DEPENDENCY | 個別管理 | project: upgrade `AvaloniaUI` to `11.3.13` |
| `ca9f5e17` | 2026-03-30 | AI | 部分採用・保留 | code_style: move some code from agent to service; rename async method with `Async` suffix; fix typos |
| `0c37957a` | 2026-03-30 | EXISTING | 反映済み | feature: support `git stash branch <branch_name> <stash>` command (#2227) |
| `0460b0de` | 2026-03-30 | EXISTING | 反映済み | ux: layout of change collection view |
| `1f1cd2fb` | 2026-03-30 | META | 対象外 | doc: Update translation status and sort locale files |
| `4dd2847d` | 2026-03-31 | EXISTING | 反映済み | feature: use custom `BranchSelector` instead of `ComboBox` to select remote branches with searching enabled (#2217) |
| `58e3130a` | 2026-03-31 | EXISTING | 反映済み | ux: makes `BranchSelector` looks like normal `ComboBox` |
| `bc6d837c` | 2026-03-31 | STYLE | 却下 | ux: simplify data template for branch name in `BranchSelector` |
| `7ca1c553` | 2026-03-31 | COMMIT_INPUT | 採用 | feature: allow Doubao detecting selection in commit message box on Windows |
| `e684d713` | 2026-04-01 | AI | 部分採用・保留 | refactor: dynamic loading and choosing AI model in assistant dialog (#2228) |
| `a05e7975` | 2026-04-01 | LOCALES | 後日精査 | doc: Update translation status and sort locale files |
| `43827a51` | 2026-04-01 | SAFETY | 反映済み | fix: duplicated repo may be added when scanning repositories on case-insensitive platforms (#2230) |
| `6792614f` | 2026-04-01 | DEPENDENCY | 個別管理 | project: upgrade AvaloniaEdit |
| `26ab0a75` | 2026-04-02 | AI_REASONING | 反映済み | refactor: use blank `User-Agent` header when communicating with open-ai compatible service (#2216) |
| `8ec1527f` | 2026-04-02 | EXISTING | 反映済み | ux: keybindings for `BranchSelector` |
| `b81c67c9` | 2026-04-02 | EXISTING | 反映済み | localization: apply selected datetime format in `About` dialog (#2223) |
| `9e0ba44f` | 2026-04-02 | SAFETY | 反映済み | fix: remote's visit url must remove the account info part (#2235) |
| `68831b22` | 2026-04-03 | APP | 却下 | refactor: notifications |
| `9dd1914e` | 2026-04-03 | APP | 却下 | refactor: move some codes from `App` to `Views.ControlExtensions` |
| `ba997edb` | 2026-04-07 | LOCALES | 後日精査 | localization: update Russian translate (#2240) |
| `488e64d2` | 2026-04-07 | META | 対象外 | doc: Update translation status and sort locale files |
| `3bf2da28` | 2026-04-07 | EXISTING | 反映済み | enhance: auto-select the new HEAD after reword (#2236) |
| `f8b91eca` | 2026-04-07 | STYLE | 却下 | enhance: use `string.Equals` instead of operator `==` |
| `834cc0c5` | 2026-04-06 | LOCALES | 後日精査 | localization: update Spanish translation (#2244) |
| `d2aa9660` | 2026-04-07 | META | 対象外 | doc: Update translation status and sort locale files |
| `03bfa29c` | 2026-04-08 | EXISTING | 反映済み | feature: allow editing of repository name via tab context menu (#2250) |
| `255138f2` | 2026-04-08 | EXISTING | 反映済み | refactor: use `DirectoryInfo` to get absolute path of repository (#2246) |
| `2581f24b` | 2026-04-08 | STYLE | 却下 | code_style: remove unused namespace using |
| `63e13a59` | 2026-04-08 | DEPENDENCY | 個別管理 | build: update packages (CommunityToolkit.Mvvm, OpenAI, LiveChartsCore) (#2242) |
| `6fc741c3` | 2026-04-08 | META | 対象外 | doc: update third-party readme |
| `a3c0b225` | 2026-04-08 | EXISTING | 反映済み | feature: better word division for highlighting (#2251) |
| `42734b90` | 2026-04-09 | EXISTING | 反映済み | fix: staged files do not update after committing with `--amend` enabled successfully (#2253) |
| `bfdd73d2` | 2026-04-09 | EXISTING | 反映済み | feature: auto-fetch now is a global setting instead of per-repo setting (#2050) |
| `13e25971` | 2026-04-09 | DEPENDENCY | 個別管理 | enhance: avoid pre-edit text box being clipped |
| `a46e752e` | 2026-04-13 | META | 対象外 | version: Release 2026.08 |
| `52b51ffb` | 2026-04-13 | EXISTING | 反映済み | enhance: AI-based commit message generator (#2255) |
| `3db91ed5` | 2026-04-13 | EXISTING | 反映済み | feature: supports to choose group and bookmark when cloning remote repository |
| `c1a5e986` | 2026-04-13 | EXISTING | 反映済み | refactor: rewrite `Open Local Repository` feature |
| `d6f45cca` | 2026-04-13 | SAFETY | 反映済み | fix: bad condition to check if auto-fetch is enabled (#2257) |
| `6eaf636b` | 2026-04-13 | META | 対象外 | doc: Update translation status and sort locale files |
| `6f58f479` | 2026-04-13 | EXISTING | 反映済み | feature: add hotkey `Ctrl+Shift+O/⌘+⇧+O` to open local repository (#2256) |
| `f8e8dcab` | 2026-04-13 | META | 対象外 | doc: Update translation status and sort locale files |
| `c4c75c32` | 2026-04-13 | AI_REASONING | 反映済み | revert: use blank `User-Agent` header when communicating with open-ai compatible service |
| `b7796f19` | 2026-04-13 | COPY_FEEDBACK | 採用 | feature: show a checked icon when sha was copied |
| `68a409c0` | 2026-04-13 | EXISTING | 反映済み | enhance: when cloning remote repo or opening local repo, the `Group` is `No Group (Uncategorized)` by default (#2258) |
| `e6ba0534` | 2026-04-14 | EXISTING | 反映済み | feature: reorder fixup commits to its right position when loading commits for interactive rebase (#588) |
| `0d5185b1` | 2026-04-14 | EXISTING | 反映済み | feature: reorder squash commits to its right position when loading commits for interactive rebase (#588) |
| `c5eb0240` | 2026-04-14 | RAW_DIFF | 最小採用 | feature: allow partial stage/unstage/discard for non-UTF8 text in diff view (#2260) |
| `4940714f` | 2026-04-14 | RAW_DIFF | 最小採用 | code_style: refine diff result parsing |
| `1ca4145e` | 2026-04-14 | EXISTING | 反映済み | fix: infinite-loop occurs when interactive rebasing with multiple `fixup!`/`squash!` commits target a single commit (#2261) |
| `5a838ec8` | 2026-04-14 | COPY_FEEDBACK | 採用 | ux: copied icon change duration |
| `b4246416` | 2026-04-14 | EXISTING | 反映済み | feature: supports to view details changes of submodule (only if this submodule is initialized and not available for new/delete submodule change) (#2264) |
| `87768e9d` | 2026-04-14 | META | 対象外 | doc: Update translation status and sort locale files |
| `cff6db8d` | 2026-04-14 | EXISTING | 反映済み | enhance: disable `OPEN DETAILS` button when one of submodule revision is lost |
| `5fd75518` | 2026-04-15 | EXISTING | 反映済み | enhance: show the `Submodule Change Details` window on the same screen of it's parent (#2264) |
| `0bf4f922` | 2026-04-15 | AI | 部分採用・保留 | fix: fetch AI models in background to avoid main window waiting to show (#2267) |
| `2ed3a79e` | 2026-04-15 | APP | 却下 | refactor: move some code from `App` to `Views.ControlExtensions` |
| `07b9c7a1` | 2026-04-15 | EXISTING | 反映済み | feature: show uncommitted changes count for submodule (#2264) |
| `27f5f5e2` | 2026-04-15 | META | 対象外 | doc: Update translation status and sort locale files |
| `460cc689` | 2026-04-15 | EXISTING | 反映済み | enhance: do not show `Git LFS` submenu for submodules (#2264) |
| `6abdf32c` | 2026-04-15 | EXISTING | 反映済み | fix: remove `--push` option because it is not valid parameter for `git-flow-next` (#2269) |
| `f5d5f63b` | 2026-04-15 | LOCALES | 後日精査 | doc: Update translation status and sort locale files |
| `538001a7` | 2026-04-16 | EXISTING | 反映済み | fix: enable `Set as tracking branch` should be visible when push to a new branch (#2273) |
| `8bdba69c` | 2026-04-16 | APP | 却下 | refactor: rewrite the way to quit app |
| `5a35c415` | 2026-04-16 | EXISTING | 反映済み | fix: directly patch the final file when trying to stage/unstage/discard selected hunk with renamed/copied file (#2272) |
| `3c747494` | 2026-04-16 | EXISTING | 反映済み | fix: app will crash when it is quiting from Dock (#2271) |
| `5114c43d` | 2026-04-16 | APP | 却下 | code_style: move `App.FixFontFamilyNames` to `StringExtensions.FormatFontNames` |
| `29cf5fc5` | 2026-04-17 | EXISTING | 反映済み | feature: add `Hide Others` app menu on macOS and correct the behaviour of `Show All` |
| `53cd847b` | 2026-04-17 | META | 対象外 | doc: Update translation status and sort locale files |
| `dfe362f2` | 2026-04-17 | EXISTING | 反映済み | feature: add `Local Branch Selector` and `Remote Branch Selector` control type for custom actions (#2274) |
| `7b875d60` | 2026-04-17 | META | 対象外 | doc: Update translation status and sort locale files |
| `8395efdd` | 2026-04-17 | EXISTING | 反映済み | enhance: replace `${BRANCH}` with current branch name if the custom action's scope is `Repository` (#2274) |
| `9144daeb` | 2026-04-17 | EXISTING | 反映済み | code_style: remove unnecessary function call |
| `fb708065` | 2026-04-17 | EXISTING | 反映済み | feature: add `String Formatter` for `TextBox` in custom action (#2274) |
| `92b010a6` | 2026-04-17 | META | 対象外 | doc: Update translation status and sort locale files |
| `770a9184` | 2026-04-17 | EXISTING | 反映済み | code_style: remove unnecessary function call |
| `8713e586` | 2026-04-17 | EXISTING | 反映済み | fix: left margins of text editor do not update after `FontSize` changed (#2276) |
| `6a118e26` | 2026-04-19 | LOCALES | 後日精査 | localization: update Spanish translation (#2278) |
| `d4be556a` | 2026-04-20 | META | 対象外 | doc: Update translation status and sort locale files |
| `83edc515` | 2026-04-20 | META | 対象外 | version: Release 2026.09 |
| `3176bcc8` | 2026-04-20 | EXISTING | 反映済み | enhance: auto-select branch for `Branch Selector` control in custom action (#2274) |
| `c1d08e29` | 2026-04-21 | SAFETY | 反映済み | fix: crashes when trying to close a repository with a action that is still running (#2289) |
| `cace869b` | 2026-04-21 | AI | 部分採用・保留 | feature: add `Use` back to apply ai generated commit message (#2287) |
| `0ae49c0d` | 2026-04-21 | REMOTE_AUTOFETCH | 最小採用 | feature: supports to disable specific remote from auto-fetch (#2285) |
| `7744a824` | 2026-04-21 | META | 対象外 | doc: Update translation status and sort locale files |
| `48c2c50a` | 2026-04-21 | SAFETY | 反映済み | enhance: do not start new auto-fetch action if the last one is still running (#2285) |
| `0b10709e` | 2026-04-22 | EXISTING | 反映済み | ux: make `About` dialog size to content (#2292) |
| `6feae0bd` | 2026-04-23 | EXISTING | 反映済み | fix: support utf-8 passphrases in askpass (#2293) |
| `a5317dd1` | 2026-04-23 | EXISTING | 反映済み | code_style: fix `dotnet format` check warnnings |
| `95279943` | 2026-04-24 | EXISTING | 反映済み | fix: `Initialize Repository` should apply the bookmark setting from `Open Repository` popup |
| `c54a1d49` | 2026-04-24 | SAFETY | 反映済み | enhance: write preferences data to a temp file first and then rename it to the final file (#2298) |
| `d89eb097` | 2026-04-24 | MICA_REMOVED | 反映済み | feature: drop Windows 11 Mica support |
| `50367b7e` | 2026-04-24 | SAFETY | 反映済み | enhance: write user settings to a temp file first and then rename it to the final file (#2298) |
| `356ab729` | 2026-04-24 | AI_REASONING | 反映済み | fix: disable thinking mode in AI chat (#2299) |
| `a6bbcab1` | 2026-04-27 | EXISTING | 反映済み | enhance: improve Linux package build configuration (#2302) |
| `d8916c53` | 2026-04-27 | EXISTING | 反映済み | fix: `NullReferenceException` occurs when `Complete` is called before `AppendLine` (#2305) |
| `203c51f3` | 2026-04-27 | EXISTING | 反映済み | feature: `ImageSource` supports loading psd files (#2304) |
| `d146f2da` | 2026-04-27 | LOCALES | 後日精査 | localization: update Russian translation (#2306) |
| `34658887` | 2026-04-27 | META | 対象外 | doc: Update translation status and sort locale files |
| `63a06ba2` | 2026-04-27 | EXISTING | 反映済み | feature: show the git source revision in `About` dialog (#2308) |
| `80a522f4` | 2026-04-27 | META | 対象外 | doc: Update translation status and sort locale files |
| `6e53d949` | 2026-04-27 | EXISTING | 反映済み | fix: when reordering commits for interactive rebasing, the subject must equals (instead of starts with) target's subject (#2303) |
| `b2aba44c` | 2026-04-27 | EXISTING | 反映済み | fix: change default seperator to prevent `/` or `:` being replaced by culture in `DateTime.ToString`  (#2307) |
| `45127ba6` | 2026-04-27 | STYLE | 却下 | code_style: run `dotnet format` to fix ci warnings |
| `c3cbc616` | 2026-04-27 | HISTORY_LIFETIME | 最小採用 | refactor: keep selection in `HISTORY` page if user only selected one or two commits (#2297) |
| `b8628cd0` | 2026-04-27 | META | 対象外 | doc: update `README` for `deb` package (#2309) |
| `f11d542f` | 2026-04-27 | LOCALES | 後日精査 | localization: update `Text.About.GitSourceRevision` |
| `4a85eca9` | 2026-04-27 | HISTORY_LIFETIME | 最小採用 | refactor: move `Commits` from DataContext to property |
| `07edf789` | 2026-04-27 | HISTORY_LIFETIME | 最小採用 | refactor: add `HistoriesCommitList` control and move selection code to it |
| `5354033a` | 2026-04-27 | HISTORY_LIFETIME | 最小採用 | code_style: `HistoriesCommitList` should not depend on `ViewModels.Histories` |
| `463f1842` | 2026-04-27 | HISTORY_LIFETIME | 最小採用 | enhance: do not change selection if previous one is empty |
| `c6353ac0` | 2026-04-28 | HISTORY_LIFETIME | 最小採用 | refactor: always create `HISTORY`, `LOCAL CHANGES` and `STASHES` page to prevent losing selection and scroll offsets |
| `5fbfe97f` | 2026-04-28 | HISTORY_LIFETIME | 最小採用 | code_style: remove unnecessary code |
| `a9bb1735` | 2026-04-28 | EXISTING | 反映済み | enhance: leave action to `pick` if there's only `fixup` and `drop` commits to it (#2313) |
| `9c75eb90` | 2026-04-28 | EXISTING | 反映済み | enhance: search position for `fixup!/squash!` commit in reversed order (#2314) |
| `7bca6c9a` | 2026-04-28 | HISTORY_LIFETIME | 最小採用 | refactor: remove binding warnings |
| `2cfcd436` | 2026-04-28 | HISTORY_LIFETIME | 最小採用 | fix: selection lost when switch from repo to welcome page |
| `baf0dd00` | 2026-04-28 | HISTORY_LIFETIME | 最小採用 | fix: wrong commit order to compare which is introduced by commit c3cbc61637 |
| `5dbb5298` | 2026-04-29 | HISTORY_LIFETIME | 最小採用 | enhance: prevent too many selection changed events being raised while apply selection to commit list |
| `fbe82dbf` | 2026-04-29 | EXISTING | 反映済み | refactor: ignore hard-coded `Enter/Space` key event in AvaloniaUI's `ListBox` and make it can be handled by app |
| `261d7234` | 2026-05-04 | META | 対象外 | version: 2026.10 |
| `d3acc780` | 2026-05-06 | AI_REASONING | 反映済み | refactor: do not disable `thinking mode` but send `reasoning_content` back to the Open-AI service instead (#2229) (#2318) |
| `5a91673a` | 2026-05-06 | HISTORY_LIFETIME | 最小採用 | enhance: remember selection if it contains less than 11 commits in `HISTORY` page and rewrite the way to auto-scroll |
| `838c5d1c` | 2026-05-06 | AI_REASONING | 反映済み | refactor: replace Azure-specific API with general `JsonPatch` to get `reasoning_content` in response |
| `e4d1651c` | 2026-05-06 | AI_REASONING | 反映済み | code_style: check null once time |
| `dd3e94fc` | 2026-05-06 | EXISTING | 反映済み | feature: add context menu entry `Copy` to selected REF in commit details panel (#2321) |
| `7f9b73ba` | 2026-05-06 | HISTORY_LIFETIME | 最小採用 | enhance: increase the number of commits when keeping selection |
| `00b95942` | 2026-05-06 | EXISTING | 反映済み | feature: add a new context menu `Compare with <upstream>` to compare selected branch with its upstream directly (#2322) |
| `5a4a6977` | 2026-05-06 | META | 対象外 | doc: Update translation status and sort locale files |
| `89ed3876` | 2026-05-06 | META | 対象外 | doc: update `README` (#2323) |
| `d4ce0b97` | 2026-05-07 | EXISTING | 反映済み | feature: add a checkbox in `Preferences > GIT` to use `Stash & Reapply` by default when checking-out or merging branches |
| `5a03715e` | 2026-05-07 | META | 対象外 | doc: Update translation status and sort locale files |
| `299622e1` | 2026-05-07 | EXISTING | 反映済み | enhance: `Left/Right` arrow key navigation in tree view (#2300) |
| `c3ea81ee` | 2026-05-07 | STYLE | 却下 | localization: change display name of `Compare` in command palette |
| `6c9462b2` | 2026-05-07 | LOCALES | 後日精査 | doc: Update translation status and sort locale files |
| `8e17ff08` | 2026-05-07 | EXISTING | 反映済み | enhance: clean up files related to rebasing after running `git rebase --abort` (#2320) |
| `39fdc1af` | 2026-05-07 | EXISTING | 反映済み | enhance: trim blocking code markdown identifier in AI response |
| `52d9270b` | 2026-05-07 | HISTORY_LIFETIME | 最小採用 | fix: navigating to the same commit with current selected does not work any more |
| `ca1ad2bd` | 2026-05-07 | STYLE | 却下 | ux: makes `BranchSelector` looks like normal `ComboBox` |
| `e720d9fd` | 2026-05-08 | EXISTING | 反映済み | feature: allow searching for remote branches in pull/push (#2283) |
| `0b09bc5c` | 2026-05-08 | AI | 部分採用・保留 | ux: change `USE` button to primary style in `AI Assistant` dialog |
| `44103f9b` | 2026-05-08 | EXISTING | 反映済み | feature: add keybindings `F2` to rename selected local branch (#2294) |
| `823bde34` | 2026-05-08 | EXISTING | 反映済み | enhance: generate a better commit message manually when cherry-picking multiple commits with `-n -x` (#2295) |
| `9635b83a` | 2026-05-08 | EXISTING | 反映済み | refactor: more safe code to decode image resources |
| `a7fb690a` | 2026-05-08 | STYLE | 却下 | ux: change the icon of `Apply Patch` |
| `6c903ca2` | 2026-05-08 | LOCALES | 後日精査 | doc: Update translation status and sort locale files |
| `0a7906a7` | 2026-05-08 | LOCALES | 後日精査 | localization: update Spanish translation (#2329) |
| `ae0e0154` | 2026-05-09 | META | 対象外 | doc: Update translation status and sort locale files |
| `51d4f7aa` | 2026-05-09 | EXISTING | 反映済み | refactor: add `--history <FILE_OR_DIR>` command line and remove the old `--file-history <FILE_PATH>` |
| `d85f7093` | 2026-05-09 | CLI_PATH | 採用 | enhance: trim the end path seperator |
| `072acc60` | 2026-05-09 | STANDALONE | 最小採用 | feature: support to collapse commit details panel (only vertical layout) |
| `ac2a329e` | 2026-05-09 | STANDALONE | 最小採用 | enhance: hiding invisible controls when commit details panel is collapsed |
| `c91cd780` | 2026-05-09 | STANDALONE | 最小採用 | ux: add button tooltips |
| `7d4baf0e` | 2026-05-09 | META | 対象外 | doc: Update translation status and sort locale files |
| `7cdae3b1` | 2026-05-10 | STANDALONE | 最小採用 | ux: should use `IsEnabled` instead of `IsVisible` |
| `839446c2` | 2026-05-11 | STYLE | 却下 | ux: align source revision in about dialog (#2331) |
| `474dc47d` | 2026-05-11 | LOCALES | 後日精査 | localization: update Russian translate (#2332) |
| `f0dfb950` | 2026-05-11 | META | 対象外 | doc: Update translation status and sort locale files |
| `6e4e62df` | 2026-05-11 | STYLE | 却下 | ux: new style for `ToggleButton.line_path` |
| `219f8e1d` | 2026-05-11 | STANDALONE | 最小採用 | feature: supports to open history details panel in a separate window |
| `b2df7bb2` | 2026-05-11 | META | 対象外 | doc: Update translation status and sort locale files |
| `8f993de3` | 2026-05-11 | LOCALES | 後日精査 | localization: add missing translations for Chinese |
| `ebbdccfa` | 2026-05-11 | META | 対象外 | doc: Update translation status and sort locale files |
| `1fd88761` | 2026-05-11 | HEAD_ACTIONS | 却下 | refactor: reuse `interactive rebase` instead of custom `reword/squash/fixup/drop` popup for HEAD commit |
| `3f3f4117` | 2026-05-11 | LOCALES | 後日精査 | doc: Update translation status and sort locale files |
| `b3ddde33` | 2026-05-11 | STANDALONE | 最小採用 | fix: `IsDetailsPanelExpanded` must be enabled by default since commit ac2a329ead |
| `02411b27` | 2026-05-11 | STANDALONE | 最小採用 | ux: layout of floating buttons |
| `ad033b79` | 2026-05-11 | STANDALONE | 最小採用 | ux: replace icons |
| `32320e20` | 2026-05-11 | STANDALONE | 最小採用 | refactor: split `HistoriesDetailsStandalone` into two different windows |
| `d31bba6a` | 2026-05-11 | STANDALONE | 最小採用 | feature: allows to open commit details panel in a separate window in `Interactive Rebase` dialog |
| `450b6a47` | 2026-05-11 | HEAD_ACTIONS | 却下 | enhance: disable manually interactive rebasing on HEAD |
| `aa541333` | 2026-05-11 | EXISTING | 反映済み | enhance: pressing `DEL` when selected multiple tags will open `Delete Multiple Tags` popup |
| `d095fd36` | 2026-05-11 | WELCOME_KEYS | 最小採用 | enhance: better `Left/Right` arrow key navigation in `Welcome` page |
| `62ed9bbd` | 2026-05-11 | WELCOME_KEYS | 最小採用 | code_style: move `Select` from child classes to `ListBoxEx` |
| `f96a7501` | 2026-05-12 | STANDALONE | 最小採用 | feature: add `Ctrl+J/⌘+J` hotkey to expand/collapse details panel in `HISTORY` page (#2335) |
| `97901d50` | 2026-05-12 | META | 対象外 | doc: Update translation status and sort locale files |
| `4a928460` | 2026-05-12 | HISTORY_LIFETIME | 最小採用 | refactor: remove unnecessary invisible buttons and move `KeyDown` handler to `HistoriesCommitList` |
| `2d3e734f` | 2026-05-12 | STYLE | 却下 | ux: lighter window shadow on Linux |
| `58a3f773` | 2026-05-12 | NO_VERIFY | 採用 | feature: support `--no-verify` option to bypass `pre-rebase` hook while rebasing (#2334) |
| `4e975901` | 2026-05-12 | META | 対象外 | doc: Update translation status and sort locale files |
| `1e10bfdf` | 2026-05-13 | EXISTING | 反映済み | ux: scroll to top button style |
| `dcee40c5` | 2026-05-13 | EXISTING | 反映済み | enhance: fallback `${BRANCH}` and `${BRANCH_FRIENDLY_NAME}` to current branch (#2338) |
| `fa1e19bc` | 2026-05-13 | EXISTING | 反映済み | feature: add global hotkey to open terminal (#2337) |
| `6d794413` | 2026-05-13 | LOCALES | 後日精査 | doc: Update translation status and sort locale files |
| `fe57dd75` | 2026-05-13 | EXISTING | 反映済み | fix: we can only use Ctrl+` on macOS |
| `6da1d158` | 2026-05-13 | HOTKEYS | 最小採用 | ux: use `^` instead of `Ctrl` on macOS |
| `473ae3b7` | 2026-05-13 | EXISTING | 反映済み | enhance: improve commit graph render performance |
| `6e8b4005` | 2026-05-13 | COMPARE_COMMITS | 採用 | feature: show left/right only commits in `Compare` window |
| `b76b84e1` | 2026-05-13 | META | 対象外 | doc: Update translation status and sort locale files |
| `b046103e` | 2026-05-14 | COMPARE_COMMITS | 採用 | enhance: cherry-pick commits should be ordered by committer time |
| `916160c7` | 2026-05-14 | EXISTING | 反映済み | feature: supports to compare selected commit with current branch (not revision compare) when it is head of some branch/tag |
| `dc3b0fc0` | 2026-05-14 | META | 対象外 | doc: Update translation status and sort locale files |
| `0d0234be` | 2026-05-14 | AI | 部分採用・保留 | enhance: support to disable auto-fetch available model-ids and specify the model-id to use directly (#2340) |
| `7fbfd277` | 2026-05-14 | META | 対象外 | doc: Update translation status and sort locale files |
| `9a891aff` | 2026-05-14 | STYLE | 却下 | ux: ref badge foreground |
| `a08b104b` | 2026-05-14 | HOTKEYS | 最小採用 | refactor: move `Ctrl+Shift+B/Ctrl+Shift+T` from `HistoriesCommitList` to `Launcher` |
| `6ed57182` | 2026-05-14 | HOTKEYS | 最小採用 | ux: hotkey order |
| `671635db` | 2026-05-15 | SAFETY | 反映済み | fix: remove duplicate branch when overwriting existing branch (#2345) |
| `c5e30e8f` | 2026-05-15 | EXISTING | 反映済み | feature: add repository page hotkey `Ctrl+E/⌘+E` to open in file browser |
| `18a6412e` | 2026-05-15 | STANDALONE | 最小採用 | refactor: `IsOpenAsStandalone` should depend on `DetailContext` |
| `03e86f9c` | 2026-05-15 | EXISTING | 反映済み | fix: force disable `InvariantGlobalization` to avoid crashing when `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT` has been enabled in user's environment (#2347) |
| `64ed89fe` | 2026-05-15 | AVALONIA_PROPERTIES | 却下 | refactor: use `Models.Null` instead of `null` to remove warings in Rider |
| `a132ae80` | 2026-05-15 | GRAPH | 差し替え採用 | refactor: highlighting in commit graph |
| `8409c46c` | 2026-05-16 | TOOLBAR | 却下 | ux: move `Show relative time in graph` to `Preferences` dialog |
| `4d36db5d` | 2026-05-16 | LOCALES | 後日精査 | doc: Update translation status and sort locale files |
| `89c9bc95` | 2026-05-16 | DEPENDENCY | 個別管理 | project: upgrade `Avalonia` to `11.3.15` |
| `54dd0300` | 2026-05-18 | META | 対象外 | version: Release 2026.11 |
| `576b7fee` | 2026-05-18 | EXISTING | 反映済み | project: ignore `*.lscache` files |
| `837f40df` | 2026-05-18 | EXISTING | 反映済み | enhance: delete `sourcegit.interactive_rebase` after abort |
| `99d973f0` | 2026-05-18 | EXISTING | 反映済み | refactor: use `Environment.ProcessPath` instead of `Process.GetCurrentProcess().MainModule!.FileName` |
| `a6800113` | 2026-05-18 | EXISTING | 反映済み | enhance: use `--since=@<unix_timestamp>` instead of `--since="yyyy/MM/dd HH:mm:ss"` |
| `39668075` | 2026-05-18 | EXISTING | 反映済み | enhance: improve some code performance |
| `4052b68b` | 2026-05-18 | EXISTING | 反映済み | feature: show `Initialize Repository` popup when trying to open a folder that is not a git repository from commandline (#2354) |
| `c3799b2d` | 2026-05-18 | STYLE | 却下 | code_style: remove unnecessary attributes and calculation |
| `d2151bad` | 2026-05-18 | STYLE | 却下 | ux: keep icon-based toggle button use the same style |
| `3586151a` | 2026-05-18 | STYLE | 却下 | ux: bookmark combobox border |
| `ea4c67a3` | 2026-05-19 | LOCALES | 後日精査 | localization: Add missing keys to French translation (#2355) |
| `425b570c` | 2026-05-19 | LOCALES | 後日精査 | doc: Update translation status and sort locale files |
| `09ab27ea` | 2026-05-19 | GRAPH | 差し替え採用 | enhance: improve commit graph highlighting performance |
| `fa7da0c6` | 2026-05-18 | LOCALES | 後日精査 | localization: update Spanish translation (#2357) |
| `394150ed` | 2026-05-19 | LOCALES | 後日精査 | doc: Update translation status and sort locale files |
| `aa9290be` | 2026-05-19 | EXISTING | 反映済み | refactor: resize width of `AUTHOR` column in `HISTORY` page. |
| `b7e4d32b` | 2026-05-19 | GRID_HEADER | 却下 | fix: context menu missing caused by commit aa9290bec9 |
| `6a9e9267` | 2026-05-20 | CLI_PATH | 採用 | fix: use Unix-style path seperator when launching sourcegit with cli `--history <dir>` (#2360) |
| `31032235` | 2026-05-20 | COPY_SHA | 採用 | feature: add context menu `Copy SHA` to selected commits in `File History` dialog (#2360) |
| `e3de9fd6` | 2026-05-20 | GRAPH | 差し替え採用 | revert: do not re-calculate graph when selected commits are all highlighted |
| `27468682` | 2026-05-20 | EXISTING | 反映済み | feature: save unmanaged repository (worktrees/submodules) info into `$GIT_DIR/sourcegit.node` (#2349) |
| `3cd24b37` | 2026-05-20 | STYLE | 却下 | ux: change the secondary foreground color for dark theme |
| `7779b91e` | 2026-05-20 | EXISTING | 反映済み | refactor: make built-in merge editor non-modal dialog (#2362) |
| `380b885c` | 2026-05-21 | EXISTING | 反映済み | feature: supports to copy author/committer time from context menu of selected commit (#2366) |
| `6309c6b6` | 2026-05-21 | META | 対象外 | doc: Update translation status and sort locale files |
| `7aa8ff49` | 2026-05-21 | STYLE | 却下 | ux: change the icon for `Check refs that contains this commit` button |
| `b973572a` | 2026-05-21 | EXISTING | 反映済み | ux: always enable `Clear stashes` button just like `Discard all changes` button |
| `2aaf6978` | 2026-05-21 | EXISTING | 反映済み | refactor: compact branch names in graph (#2369) |
| `5bbef394` | 2026-05-21 | EXISTING | 反映済み | fix: crashing when trying to open context menu of a commit that contains a invalid remote branch decorator (#2367) |
| `ec74c6d4` | 2026-05-21 | EXISTING | 反映済み | refactor: commit refs presenter |
| `2d15d8ad` | 2026-05-21 | META | 対象外 | doc: Update translation status and sort locale files |
| `d061a506` | 2026-05-22 | EXISTING | 反映済み | enhance: disable `Stash` context menu for staged files when `Amend` is enabled (#2371) |
| `eac1c113` | 2026-05-25 | EXISTING | 反映済み | enhance: render `fixup!`, `amend!`, `squash!` as keyword in commit subject (#2375) |
| `4b6ba4ad` | 2026-05-25 | LOCALES | 後日精査 | localization: update Russian translate (#2378) |
| `6b72889b` | 2026-05-25 | META | 対象外 | doc: Update translation status and sort locale files |
| `cb6321a4` | 2026-05-25 | EXISTING | 反映済み | feature: add `Assisted-by:` to auto-completion strings in commit message editor |
| `aa999300` | 2026-05-26 | LOCALES | 後日精査 | localization: add `Hebrew` translation (#2383) |
| `979a51f4` | 2026-05-26 | LOCALES | 後日精査 | doc: Update translation status and sort locale files |
| `ae359b64` | 2026-05-26 | STYLE | 却下 | code_style: remove unnecessary attribute |
| `29c32c42` | 2026-05-26 | EXISTING | 反映済み | ux: hide `Customize merge message` for fast-forward merge |
| `bd32c32b` | 2026-05-26 | EXISTING | 反映済み | fix: failed to generate commit message when `reason_content` is an empty string (#2385) |
| `5706b708` | 2026-05-27 | EXISTING | 反映済み | fix: navigation hotkeys in diff viewer only work in FIRST visited instance (#2390) |
| `2647b2d4` | 2026-05-27 | STYLE | 却下 | ux: use `Border` instead of `ContentControl` |
| `1faa3b1c` | 2026-05-27 | MERGE_TEST | 最小採用 | enhance: merge |
| `d22b95a9` | 2026-05-27 | LOCALES | 後日精査 | doc: Update translation status and sort locale files |
| `df35466f` | 2026-05-27 | STYLE | 却下 | code_style: remove unused parameter |
| `10448441` | 2026-05-27 | MERGE_TEST | 最小採用 | refactor: testing merge |
| `b5fd290a` | 2026-05-27 | MICA_REMOVED | 反映済み | code_style: remove deprecated `UseMicaOnWindows11` |
| `b248be52` | 2026-05-27 | EXISTING | 反映済み | ux: remove unused hotkey bindings (#2390) |
| `2fbe2b6f` | 2026-05-27 | WORKTREE_SEARCH | 差し替え採用 | feature: allow search branches when adding a worktree (#2394) |
| `908faf3b` | 2026-05-28 | WORKTREE_SEARCH | 差し替え採用 | code_review: PR #2394 |
| `f1b4378b` | 2026-05-28 | EXISTING | 反映済み | ux: use `BranchSelector` instead of `ComboBox` to select local branch in `Push` pop |
| `8427859a` | 2026-05-28 | EXISTING | 反映済み | enhance: add some checks when auto-selecting remote branch in `Push` |
| `0938fd49` | 2026-05-28 | EXISTING | 反映済み | enhance: cache loaded `IRawGrammar` and `IRawTheme` (#2396) |
| `ea51c414` | 2026-05-28 | EXISTING | 反映済み | enhance: disable copying when user only selected hunk indicator line (#2391) |
| `03248307` | 2026-05-29 | EXISTING | 反映済み | feature: testing for rebase and show testing result in `Rebase` popup |
| `554aa589` | 2026-05-29 | META | 対象外 | doc: Update translation status and sort locale files |
| `5a91017d` | 2026-05-29 | EXISTING | 反映済み | enhance: commit refs presenter |
| `1661d325` | 2026-05-29 | EXISTING | 反映済み | enhance: better way to handle exit code of `git merge-tree --write-tree` command |
| `cc820302` | 2026-05-29 | META | 対象外 | doc: Update translation status and sort locale files |
| `e5b417f8` | 2026-05-29 | EXISTING | 反映済み | enhance: force update commit graph layout to ensure it is rendered correctly |
| `832fe288` | 2026-05-29 | STYLE | 却下 | code_style: remove unnecessary fields |
| `ab1c77d9` | 2026-05-29 | META | 対象外 | doc: update screenshots |
| `d5824534` | 2026-05-31 | EXISTING | 反映済み | refactor: build history querying params (#2401) |
| `2b7a6674` | 2026-06-01 | LOCALES | 後日精査 | localization: update Russian translation (#2405) |
| `ad9badfa` | 2026-06-01 | META | 対象外 | doc: Update translation status and sort locale files |
| `aeca45d2` | 2026-06-01 | META | 対象外 | version: Release 2026.12 |
| `4dbc86ed` | 2026-06-01 | STYLE | 却下 | code_style: remove unused code |
| `bba1f0cd` | 2026-06-01 | META | 対象外 | doc: update THIRD-PARTY-LICENSES.md |
| `993c7858` | 2026-06-01 | EXISTING | 反映済み | fix: hotkey to stage/unstage/discard selected hunk only works in the very FIRST text diff view instance (#2410) |
| `8787ad1d` | 2026-06-01 | EXISTING | 反映済み | enhance: makes sure the commit list is still focused after closing `Select Commit` dialog (via `Alt+Up/Alt+Down`) |
| `b22d7354` | 2026-06-02 | LOCALES | 後日精査 | localization: update Spanish translation (#2413) |
| `0bbe93c4` | 2026-06-03 | META | 対象外 | doc: Update translation status and sort locale files |
| `68c39800` | 2026-06-03 | SAFETY | 反映済み | fix: always reset `IsAutoFetching` to `false` after auto-fetching finished (#2412) |
| `870ca771` | 2026-06-03 | STYLE | 却下 | ux: new theme for page switcher in repository dashboard |
| `bfaea24b` | 2026-06-03 | RAW_DIFF | 最小採用 | code_style: more clear code for new file patch |
| `ce29b44c` | 2026-06-03 | EXISTING | 反映済み | code_style: move static `Models.DiffOption.IgnoreCRAtEOL` to `ViewModels.Preferences.IgnoreCRAtEOLInDiff` |
| `bcdd77da` | 2026-06-03 | STYLE | 却下 | code_style: remove unused code |
| `e167f8e9` | 2026-06-03 | EXISTING | 反映済み | enhance: sync all text diff viewers when `Show All Lines`, `Ignore Whitespace Changes` or `Side-by-Side` changed |
| `fbf1823b` | 2026-06-03 | EXISTING | 反映済み | feature: supports to resize left/right side in side-by-side diff viewer (#2414) |
| `12d5fc62` | 2026-06-03 | EXISTING | 反映済み | fix: left-only stage/unstage/discard button position in side-by-side text diff view (#2414) |
| `a666fa27` | 2026-06-04 | LOCALES | 後日精査 | localization: add `Greek` translation (#2416) |
| `4e0a7231` | 2026-06-04 | RAW_DIFF | 最小採用 | refactor: patch generator |
| `f9a7fefb` | 2026-06-04 | EXISTING | 反映済み | feature: support to copy selected line(s) as patch in text diff viewer |
| `7e409ac8` | 2026-06-04 | META | 対象外 | doc: Update translation status and sort locale files |
| `4f14c65a` | 2026-06-04 | RAW_DIFF | 最小採用 | code_style: `PatchGenerator` |
| `d915d317` | 2026-06-04 | EXISTING | 反映済み | ux: use brighter foreground color for secondary text in `Dark` theme (#2417) |
| `089eaaca` | 2026-06-05 | EXISTING | 反映済み | enhance: add `Merge <branch_name> into <current>` submenu entry back (#2418) |
| `c358527e` | 2026-06-05 | EXISTING | 反映済み | ux: brighter foreground for primary text in `Dark` theme |
| `9374af66` | 2026-06-05 | EXISTING | 反映済み | enhance: checkout as detached HEAD when double-clicking commit in `HISTORY` page while bisecting |
| `26ff9215` | 2026-06-05 | EXISTING | 反映済み | feature: supports to apply patch from clipboard |
| `faee87b2` | 2026-06-05 | LOCALES | 後日精査 | doc: Update translation status and sort locale files |
| `40a3d7c4` | 2026-06-05 | META | 対象外 | doc: update `README.md` about contributing (#2419) |
| `d2b6c13e` | 2026-06-07 | META | 対象外 | fix: add an missing argument of linux package's desktop entry (#2279) |
| `b4e5e4bd` | 2026-06-07 | LOCALES | 後日精査 | localization: update Russian translation (#2420) |
| `da6d6ba8` | 2026-06-07 | META | 対象外 | doc: Update translation status and sort locale files |
| `ca111a80` | 2026-06-07 | LOCALES | 後日精査 | localization: update Korean translations (#2421) |
| `c087935f` | 2026-06-07 | LOCALES | 後日精査 | doc: Update translation status and sort locale files |
| `0f70e561` | 2026-06-08 | FONTS | 却下 | resources: use `JetBrains Mono NL` instead of `JetBrains Mono` as default monospaced font |
| `412a489e` | 2026-06-08 | STYLE | 却下 | ux: built-in `Dark` theme |
| `5d27c70a` | 2026-06-08 | FONTS | 却下 | resource: remove unused font |
| `aa259669` | 2026-06-08 | STYLE | 却下 | ux: remove unnecessary label for patch file selector |
| `0723ef84` | 2026-06-08 | LOCALES | 後日精査 | doc: Update translation status and sort locale files |
| `103dc010` | 2026-06-08 | EXISTING | 反映済み | enhance: button states in `Bisect` toolbar |
| `e70e7de2` | 2026-06-08 | STYLE | 却下 | resources: use a new `Icons.Good` instead of `Icons.Check` for good commit in bisecting |
| `eebf4320` | 2026-06-08 | EXISTING | 反映済み | ux: bisecting status tooltip |
| `0f22ba9f` | 2026-06-08 | META | 対象外 | doc: Update translation status and sort locale files |
| `dc236b3e` | 2026-06-08 | EXISTING | 反映済み | ux: makes all primitive controls apply the same theme |
| `e45f3dab` | 2026-06-08 | EXISTING | 反映済み | ux: apply `Brush.InlineCodeFG` theme in commit subject |
| `87766dd6` | 2026-06-08 | EXISTING | 反映済み | refactor: support to show both `AUTHOR TIME` and `COMMIT TIME` in history graph (#2423) |
| `36f15e01` | 2026-06-08 | LOCALES | 後日精査 | doc: Update translation status and sort locale files |
| `bb53fbbf` | 2026-06-09 | EXISTING | 反映済み | enhance: support to turn on/off `--recursive` option while updating submodules (#2426) |
| `aad9387f` | 2026-06-09 | META | 対象外 | doc: Update translation status and sort locale files |
| `57a3d694` | 2026-06-09 | EXISTING | 反映済み | enhance: respect `Enable --recursive when auto-updating submodules` setting in `Update Submodules` popup |
| `9d89012e` | 2026-06-09 | EXISTING | 反映済み | enhance: bisect |
| `dc9774c3` | 2026-06-09 | META | 対象外 | doc: Update translation status and sort locale files |
| `9e304d47` | 2026-06-09 | EXISTING | 反映済み | ux: bounds of highlighted hunk |
| `8c3c0bec` | 2026-06-09 | THEME_OVERRIDES | 却下 | feature: add a button in `Theme Overrides` textbox to open `sourcegit-theme` repository directly |
| `2fc3ec53` | 2026-06-10 | EXISTING | 反映済み | fix: remove extra '\n' when resolving conflicts using internal merge tool (#2429) |
| `6909f220` | 2026-06-10 | EXISTING | 反映済み | feature: supports to save ignore file pattern to sub-directory (#2424) |
| `a0d51fee` | 2026-04-29 | DEPENDENCY | 個別管理 | project: upgrade `AvaloniaUI` to `12.0.4` |
| `95c2990e` | 2026-06-10 | MACOS_TITLE | 未判定 | macOS: directly reposition traffic lights |
| `93594f0e` | 2026-06-10 | STYLE | 却下 | enhance: enable `SubpixelAntialias` when renderring commit subject |
| `463fd517` | 2026-06-10 | MACOS_TITLE | 未判定 | macOS: use `objc_msgSend_stret` to get button frame on Intel CPU |
| `5783d26e` | 2026-06-10 | STYLE | 却下 | style: new theme for CSD on Linux |
| `02717812` | 2026-06-10 | MACOS_TITLE | 未判定 | code_style: native related code cleanup |
| `9e930de8` | 2026-06-11 | MACOS_TITLE | 未判定 | enhance: reduce the times to call `AdjustTrafficLightsForThickTitleBar` on macOS |
| `ec6736a6` | 2026-06-11 | EXISTING | 反映済み | fix: text diff editor crash when clicking selected text in `LOCAL CHANGES` view |
| `bbf640bf` | 2026-06-11 | MACOS_TITLE | 未判定 | refactor: replace `Border` with `MacOSTrafficLightsSpacer` to hold the area of macOS traffic lights |
| `c7395b6b` | 2026-06-11 | CHART | 却下 | refactor: statistics window |
| `fda413d5` | 2026-06-11 | CHART | 却下 | enhance: only set tooltip of hovered sample when it is necessary |
| `ec7c4287` | 2026-06-12 | DEPENDENCY | 個別管理 | project: upgrade third-party dependencies |
| `d150ee9e` | 2026-06-12 | CHART | 却下 | ux: min width of sample in `Statistics` dialog |
| `5b5c10a4` | 2026-06-12 | CHART | 却下 | enhance: only redraw chart if it is necessary |
| `df978183` | 2026-06-12 | CHART | 却下 | enhance: condition to draw the right-most sample |
| `ef2854bc` | 2026-06-12 | EXISTING | 反映済み | feature: show `EMPTY FILE` instead of `NO CHANGES OR ONLY EOL CHANGES` when added/deleted file is an empty file (#2434) |
| `f5c869f5` | 2026-06-12 | META | 対象外 | doc: Update translation status and sort locale files |
| `7e2aabb4` | 2026-06-12 | EXISTING | 反映済み | enhance: `Ignore Whitespace Changes` button is only visible for text diff |
| `e6170f8a` | 2026-06-12 | EXISTING | 反映済み | enhance: diff view (#2436) |
| `890cfa85` | 2026-06-12 | STANDALONE | 最小採用 | enhance: makes standalone window `Commit Details` and `Revision Compare` detach from the main window (#2437) |
| `2ff3f0bc` | 2026-06-12 | CHART | 却下 | ux: larger min sample width |
| `33b9e0a7` | 2026-06-12 | CHART | 却下 | code_style: move some code about statistic from `Views` to `ViewModels` |
| `5cc615d6` | 2026-06-14 | CHART | 却下 | feature: enable horizontal scrolling in chart |
| `78bcbc69` | 2026-06-14 | META | 対象外 | doc: update third-party packages readme |
| `c6b942e4` | 2026-06-15 | SAFETY | 反映済み | fix: expanded state of remote branches (include remote node) is missing after checking-out/creating/renaming branch |
| `a3815f57` | 2026-06-15 | META | 対象外 | version: Release 2026.13 |
| `ff872070` | 2026-06-15 | GITFLOW | 差し替え採用 | feature: supports both `git-flow` and `git-flow-next` (#2439) |
| `b3343626` | 2026-06-15 | GITFLOW | 差し替え採用 | enhance: parsing git-flow configurations (#2439) |
| `8de93c86` | 2026-06-15 | GITFLOW | 差し替え採用 | enhance: parse `git-flow-next` style configuration only when it is installed |
| `7dea4b73` | 2026-06-15 | GITFLOW | 差し替え採用 | fix: `gitflow.initialized` must be `true` when using `git-flow-next` |
| `904fef45` | 2026-06-15 | GITFLOW | 差し替え採用 | feature: support to finish current git-flow topic in `Git Flow` dropdown |
| `c25a1c60` | 2026-06-15 | META | 対象外 | doc: Update translation status and sort locale files |
| `163e4d80` | 2026-06-15 | EXISTING | 反映済み | ux: validation tips for patch file in `Apply` popup |
| `7f805fc9` | 2026-06-15 | GITFLOW | 差し替え採用 | ux: use `BranchOrTagNameTextBox` to edit git-flow new topic branch name |
| `4e12bb40` | 2026-06-15 | META | 対象外 | doc: Update translation status and sort locale files |
| `baf2ed64` | 2026-06-15 | EXISTING | 反映済み | fix: sort staged+amend changes the same way as ordinary staged changes (#2441) |
| `47d4e502` | 2026-06-15 | GITFLOW | 差し替え採用 | code_style: rename `GitFlowVersion.Classic` to `GitFlowVersion.Legacy` and use `--keep` instead of `-k` for legacy `git-flow` extension |
| `2614f37b` | 2026-06-15 | GITFLOW | 差し替え採用 | feature: support `--rebase` in `git flow <topic> finish <name>` command |
| `d3158dc4` | 2026-06-15 | META | 対象外 | doc: Update translation status and sort locale files |
| `707a6a5a` | 2026-06-15 | CHART | 却下 | enhance: makes sure y-axis is higher than all sample number |
| `a5eed3ac` | 2026-06-15 | LOCALES | 後日精査 | localization: update Traditional Chinese translations (#2443) |
| `92907a28` | 2026-06-15 | GITFLOW | 差し替え採用 | ux: re-design popup for `git flow <topic> start` command |
| `672ecc98` | 2026-06-15 | META | 対象外 | doc: Update translation status and sort locale files |
| `25ac8ec5` | 2026-06-15 | CHART | 却下 | ux: smooth scrolling in chart |
| `2982c197` | 2026-06-15 | EXISTING | 反映済み | feature: add `Checkout` context menu entry for selected tag to checkout the referenced commit (detached) (#2442) |
| `8d8da035` | 2026-06-15 | META | 対象外 | doc: Update translation status and sort locale files |
| `90c3220f` | 2026-06-16 | EXISTING | 反映済み | refactor: rename `CheckoutCommit` to `CheckoutDetached` (#2442) |
| `9e8264df` | 2026-06-16 | LOCALES | 後日精査 | doc: Update translation status and sort locale files |
| `3d00ba76` | 2026-06-16 | LOCALES | 後日精査 | localization: update translations |
| `5c4cb574` | 2026-06-16 | LOCALES | 後日精査 | doc: Update translation status and sort locale files |
| `b224b0df` | 2026-06-16 | EXISTING | 反映済み | feature: add `Merge <tag> to <current` context menu for selected tag |
| `55acfe6e` | 2026-06-16 | META | 対象外 | doc: Update translation status and sort locale files |
| `8c82fa4d` | 2026-06-16 | UPDATE | 却下 | feature: use `git describe` to generate a friendly version name while compiling |
| `f80561e1` | 2026-06-16 | LOCALES | 後日精査 | doc: Update translation status and sort locale files |
| `fd709c44` | 2026-06-16 | EXISTING | 反映済み | enhance: normalize path string in commandline arguments and support to open bare repository from commandline (#2446) |
| `551fc6e1` | 2026-06-16 | BOOKMARK | 却下 | refactor: replace `ComboBox` with a simple custom control `BookmarkSelector` to modify repository's bookmark color |
| `d16a6a6d` | 2026-06-16 | BOOKMARK | 却下 | refactor: use a custom control `Bookmark` to draw repository icon |
| `6a47cec3` | 2026-06-16 | EXISTING | 反映済み | fix: app hungs when seleting `no newline at end of file` indicator in text diff view with syntax highlighting turned on (#2449) |
| `8e1b34ba` | 2026-06-16 | DEPENDENCY | 個別管理 | project: upgrade `AvaloniaEdit` |
| `7eb66815` | 2026-06-16 | STYLE | 却下 | ux: limit the max size of `no newline at end of file` indicator |
| `dacc7e8e` | 2026-06-16 | DEPENDENCY | 個別管理 | project: update `AvaloniaEdit` to prevent syntax highlighting blocks the UI |
| `371422a1` | 2026-06-17 | BOOKMARK | 却下 | code_style: do not re-calculate hitboxes in `BookmarkSelector` |
| `68d336df` | 2026-06-17 | LOCALES | 後日精査 | localization: update context menu labels |
| `c9ed80c7` | 2026-06-17 | GITFLOW | 差し替え採用 | ux: icons for gitflow |
| `381b44a4` | 2026-06-17 | EXISTING | 反映済み | feature: show suggestions when searching commit by author or committer |
| `d5ba5c05` | 2026-06-17 | EXISTING | 反映済み | enhance: directly set the selected commit when searching by SHA |
| `cb19026d` | 2026-06-17 | GITFLOW | 差し替え採用 | code_style: git-flow commands |
| `a8ebfc3e` | 2026-06-17 | EXISTING | 反映済み | perf: replace manual hex formatting with `Convert.ToHexStringLower` (#2453) |
| `59459546` | 2026-06-17 | EXISTING | 反映済み | refactor: remove support to search commits by committer |
| `a44baf7a` | 2026-06-17 | LOCALES | 後日精査 | doc: Update translation status and sort locale files |
| `2e373121` | 2026-06-17 | SAFETY | 反映済み | code_style: use a singleton `HttpClient` instance instead of creating a new one for each request |
| `67b4de09` | 2026-06-18 | EXISTING | 反映済み | fix: `Logs` dialog does not layout properly sometimes with multiple displays |
| `004bea9e` | 2026-06-18 | STYLE | 却下 | ux: style for `No newline at end of file` indicator |
| `aa8d4a2e` | 2026-06-18 | RAW_DIFF | 最小採用 | feature: show line-endings when toggle `Show hidden symbols` in text diff view (#2458) |
| `ff2aa0e9` | 2026-06-20 | DEPENDENCY | 個別管理 | fix: IME pre-edit text preview in commit message textbox (#2464) |
| `3331766d` | 2026-06-21 | EXISTING | 反映済み | refactor: commit message editor |
| `e9a7eeb1` | 2026-06-21 | DEPENDENCY | 個別管理 | project: update `AvaloniaEdit` |
| `c677d621` | 2026-06-22 | EXISTING | 反映済み | [enhancement] Add option '--ignore-blank-lines' when ignoring whitespace changes (#2455) |
| `f56ec6c6` | 2026-06-22 | TAB_SCROLL | 反映済み | enhance: page tab scroll |
| `e90514c7` | 2026-06-22 | STYLE | 却下 | code_style: move onetime-style from `Styles` to the place where it is used |
| `74fe010f` | 2026-06-22 | STYLE | 却下 | code_style: shadow of local variable |
| `62d12324` | 2026-06-22 | AVALONIA_PROPERTIES | 却下 | code_style: use `DirectProperty` instead of `StyledProperty` for non-stylable attributes |
| `d096bc6a` | 2026-06-22 | AVALONIA_PROPERTIES | 却下 | code_style: use `OnPropertyChanged` instead of static constructor |
| `f7bbcff3` | 2026-06-23 | STYLE | 却下 | ux: new icon for empty file |
| `c7bab7ad` | 2026-06-23 | SAFETY | 反映済み | fix: timer leaks after views detached from visual tree |
| `baa34ae9` | 2026-06-23 | SAFETY | 反映済み | fix: branch name in toolbar not updated immediately after renaming current branch (#2470) |
| `d1f1353a` | 2026-06-23 | SAFETY | 反映済み | code_review: PR #2470 |
| `0721743e` | 2026-06-23 | TAB_SCROLL | 反映済み | ux: convert vertical scrolling to horizontal scrolling for launcher tabs |
| `3bd43085` | 2026-06-23 | LOCALES | 後日精査 | translation: Update Russian translate (#2471) |
| `b1b9b681` | 2026-06-23 | META | 対象外 | doc: Update translation status and sort locale files |
| `5a53e56b` | 2026-06-23 | SAFETY | 反映済み | fix: local branches count not updated immediately after creating branch (#2472) |
| `1a2dc871` | 2026-06-23 | SAFETY | 反映済み | code_review: PR #2472 |
| `93fb4fce` | 2026-06-23 | MACOS_TITLE | 未判定 | ux: titlebar button position on macOS in full-screen mode |
| `f7c61cbb` | 2026-06-24 | EXISTING | 反映済み | fix: create branch dialog not respecting stash & reapply default preference (#2473) |
| `7cce752c` | 2026-06-24 | SAFETY | 反映済み | fix: prevent unsafe browser targets from issue tracker links (#2475) |
| `af278323` | 2026-06-23 | LOCALES | 後日精査 | localization: update Spanish translation (#2476) |
| `7a292bf2` | 2026-06-24 | LOCALES | 後日精査 | doc: Update translation status and sort locale files |
| `b0f08263` | 2026-06-24 | DEPENDENCY | 個別管理 | project: upgrade third-party dependencies |
| `81844e83` | 2026-06-24 | FILEMODE | 最小採用 | enhance: show localized description for file-mode change in tooltip |
| `d1a4cf30` | 2026-06-24 | LOCALES | 後日精査 | doc: Update translation status and sort locale files |
| `e9e2be18` | 2026-06-24 | EXISTING | 反映済み | style: fix visual inconsistencies between WorkingCopy and StashesPage (#2477) |
| `337c0496` | 2026-06-24 | MACOS_TITLE | 未判定 | ux: hide tracffic lights spacer on Windows and Linux |
| `a30cf17a` | 2026-06-24 | EXISTING | 反映済み | enhance: support type-changed diffs (#2474) |
| `36472bec` | 2026-06-24 | EXISTING | 反映済み | code_review: PR #2474 |
| `7de831f6` | 2026-06-24 | RAW_DIFF | 最小採用 | code_style: checking order while parsing header line |
| `8477bdd0` | 2026-06-24 | DEPENDENCY | 個別管理 | project: downgrade `AvaloniaUI` to 11.3.18 |
| `09a380e1` | 2026-06-24 | EXISTING | 反映済み | ux: use tabular numbers for commit time (#2479) |
| `26218a7b` | 2026-06-24 | WRAPPING | 差し替え採用 | fix: text wrapping in commit message editor does not work well |
| `8272da4b` | 2026-06-25 | EXISTING | 反映済み | fix: crash when closing an unmanaged repository tab whose directory was removed (#2480) |
| `3b8af026` | 2026-06-25 | EXISTING | 反映済み | ux: do not show drag&drop tip on Linux |
| `5ee6084e` | 2026-06-25 | EXISTING | 反映済み | ux: border of popup/menu |
| `967176e3` | 2026-06-25 | EXISTING | 反映済み | ux: shadow of popups/menus |
| `3708c56c` | 2026-06-25 | EXISTING | 反映済み | ux: enable `+tnum` font feature for commit time |
| `92255720` | 2026-06-25 | EXISTING | 反映済み | refactor: deleting branch |
| `ed1d006d` | 2026-06-25 | LOCALES | 後日精査 | doc: Update translation status and sort locale files |
| `182741ad` | 2026-06-25 | STYLE | 却下 | code_style: cleanup unnecessary code |
| `ff3f81b2` | 2026-06-25 | EXISTING | 反映済み | enhance: unify all `git` commands output user with `$NAME±$EMAIL` format |
| `ce7bc045` | 2026-06-25 | EXISTING | 反映済み | ux: use monospaced font for commit's SHA in commit list |
| `3918ff2b` | 2026-06-25 | EXISTING | 反映済み | ux: use monospaced font for all commit SHA |
| `f30fd59a` | 2026-06-25 | EXISTING | 反映済み | code_style: use static lambda |
| `e761ff91` | 2026-06-25 | EXISTING | 反映済み | enhance: Visual Studio support |
| `04913f54` | 2026-06-25 | EXISTING | 反映済み | enhance: use `Tapped` event instead of `DoubleTapped` to select item in suggestion popup via mouse |
| `3b61842d` | 2026-06-26 | LOCALES | 後日精査 | fix: correct placeholder in zh_TW BranchTree.AheadBehind translation (#2490) |
| `0bc2945f` | 2026-06-26 | EXISTING | 反映済み | code_style: rename `SupportOpenAsFolder` to `SupportOpenFolder` |
| `adccb460` | 2026-06-26 | EXISTING | 反映済み | feature: add `Push` context menu entry for non-current branch in context menu of selected commit (#2485) |
| `da9bf18d` | 2026-06-26 | STYLE | 却下 | code_style: remove unused styles |
| `f3173b4c` | 2026-06-26 | STYLE | 却下 | ux: some theme changes |
| `3c6e5390` | 2026-06-26 | EXISTING | 反映済み | fix: submodule dirty state is not updated after committing |
| `e92810e7` | 2026-06-26 | EXISTING | 反映済み | enhance: do not ignore lines start with `#` in commit message while rebasing (#2491) |
| `77f24c49` | 2026-06-26 | EXISTING | 反映済み | ux: re-design built-in merge conflict editor |
| `2ec66bb8` | 2026-06-26 | LOCALES | 後日精査 | localization: update English translation |
| `7e710d7c` | 2026-06-27 | EXISTING | 反映済み | feature: add a new context menu entry - `Ignore all untracked files in the same folder` for selected file (#2492) |
| `93c94c65` | 2026-06-27 | META | 対象外 | doc: Update translation status and sort locale files |
| `4a68bb02` | 2026-06-27 | EXISTING | 反映済み | ux: remove duplicated icon |
| `e7472e80` | 2026-06-27 | EXISTING | 反映済み | ux: use a simple icon for `Git Ignore` context menu entry |
| `a2b1aab9` | 2026-06-28 | LOCALES | 後日精査 | localization: update Japanese translation (#2493) |
| `946089ca` | 2026-06-28 | LOCALES | 後日精査 | doc: Update translation status and sort locale files |
| `c945485e` | 2026-06-29 | META | 対象外 | version: Release 2026.14 |
| `01f55f7a` | 2026-06-29 | STYLE | 却下 | ux: new style for page switcher in repository |
| `1e255299` | 2026-06-29 | EXISTING | 反映済み | fix: escape BRE special characters when selecting author from suggestions (#2494) |
| `70dec954` | 2026-06-29 | EXISTING | 反映済み | code_review: PR #2494 |
| `5d8a9342` | 2026-06-29 | RECENT_MESSAGES | 却下 | refactor: move recent commit messages from repository settings to ui-states |
| `bdcf335e` | 2026-06-29 | STYLE | 却下 | refactor: reorder context menu items of selected branch |
| `190b4d97` | 2026-06-30 | EXISTING | 反映済み | fix: let the diff view wake up before its first curtain call (#2496) |
| `e6b96d98` | 2026-06-30 | WIP | 採用済み | fix: versions of `git` prior to v2.45.0 will reject a value of `commentChar` that consists of more than a single ASCII byte (#2500) |
| `fe99f4a2` | 2026-07-01 | MOVE_GROUP | 採用 | ux: max height of group list in `Move Repository Node` popup (#2502) |
| `b4201ed9` | 2026-07-01 | EXISTING | 反映済み | fix: null check for RevisionFileSearchSuggestion on key down (#2501) |
| `bb9a3603` | 2026-07-01 | CHART | 却下 | fix: prevent crash in Chart.Render when repository has no commits (#2504) |
| `f3fd2f6c` | 2026-07-01 | CHART | 却下 | refactor: do not render chart if no commit collected in statistics |
| `dd163e55` | 2026-07-01 | EXISTING | 反映済み | fix: hide file manager and terminal options for invalid repositories (#2503) |
| `cb1664f1` | 2026-07-01 | EXISTING | 反映済み | code_review: PR #2503 |
| `80ca7353` | 2026-07-01 | MOVE_GROUP | 採用 | ux: default select the `ROOT` group in `Move Repository Node` popup (#2502) |
| `324e4af0` | 2026-07-01 | HOTKEYS | 最小採用 | refactor: change the `Open Local Repository` hotkey to `Ctrl+L` on Windows/Linux |
| `06e2fdc0` | 2026-07-02 | HOTKEYS | 最小採用 | ux!: change the hotkeys to clone/open repository |
| `4a8276f2` | 2026-07-02 | STANDALONE | 最小採用 | feature: add hotkey `Ctrl+N` to open commit details panel in a seperate window (#2506) |
| `34f4a7c6` | 2026-07-02 | FILE_PICKER | 採用 | refactor: use `*` instead `*.*` to choose file without specified extensions (#2467) |
| `49f6dd52` | 2026-07-03 | EXISTING | 反映済み | ux: style for search panel in text editor |
| `9fea6653` | 2026-07-03 | STYLE | 却下 | ux: dropdown menu position |
| `2aeb335f` | 2026-07-04 | LOCALES | 後日精査 | localization: update Spanish translation (#2510) |
| `a6be30c6` | 2026-07-05 | META | 対象外 | doc: Update translation status and sort locale files |
| `2750812a` | 2026-07-06 | LOCALES | 後日精査 | localization: update Russian translate (#2514) |
| `37d9c47a` | 2026-07-06 | META | 対象外 | doc: Update translation status and sort locale files |
| `673a66f0` | 2026-07-06 | LOCALES | 後日精査 | localization: complete Greek (el_GR) translation (#2515) |
| `2ea100fc` | 2026-07-06 | META | 対象外 | doc: Update translation status and sort locale files |
| `222cd802` | 2026-07-06 | CLOSE_KEYS | 採用 | ux: use `Ctrl/⌘+W` to close sub-windows |
| `7327ccd2` | 2026-07-06 | LOCALES | 後日精査 | localization: update Chinese translations |
| `e6bf4b98` | 2026-07-06 | META | 対象外 | doc: Update translation status and sort locale files |
| `6077e541` | 2026-07-06 | DEPENDENCY | 個別管理 | project: upgrade AvaloniaEdit |
| `68d13477` | 2026-07-06 | WIP | 採用済み | fix: diff view does not update when selecting between an unstage new submodule and other file |
| `2a12e040` | 2026-07-06 | SUBMODULE_DIFF | 最小採用 | ux: submodule diff style |
| `ed9ec472` | 2026-07-07 | SUBMODULE_DIFF | 最小採用 | refactor: improve submodule diff loading |
| `c0dc434f` | 2026-07-07 | SUBMODULE_DIFF | 最小採用 | enhance: do not query file content if revision is an empty tree hash |
| `7c62df1c` | 2026-07-08 | SUBMODULE_DIFF | 最小採用 | refactor: creating diff object for image/binary/submodule |
| `bf7892f8` | 2026-07-08 | META | 対象外 | project: add .tss to .gitignore (#2520) |
| `9d01953f` | 2026-07-09 | STYLE | 却下 | code_style: remove unnecessary spaces at end of line |
| `4033af19` | 2026-07-13 | LOCALES | 後日精査 | localization: update Traditional Chinese translations (#2526) |
| `8f75493d` | 2026-07-13 | META | 対象外 | version: Release 2026.15 |
| `3cd815d4` | 2026-07-13 | STYLE | 却下 | ux: change status icon (#2521) |
| `11748ea1` | 2026-07-14 | CLOSE_KEYS | 採用 | feature: support using `ESC` to cancel confirm dialog |
| `73f83611` | 2026-07-14 | TOOLBOX | 採用 | refactor: use product code to check JetBrains products |
| `66a75872` | 2026-07-14 | EXISTING | 反映済み | ux: reorder of external editors on Windows |
| `64d41169` | 2026-07-14 | SELECTION | 採用 | ux: prevent scroll to last selected item when collapse/expand tree node (#2531) |
| `90aecc6d` | 2026-07-14 | STATISTICS_FILTER | 採用 | feature: branch filtering in `Statistics` window (#2532) |
| `66c2138c` | 2026-07-14 | STATISTICS_FILTER | 採用 | fix: missing check for `FullName` of selected branch while filtering branch in `Statistic` window (#2532) |
| `d235132c` | 2026-07-14 | WIP | 採用済み | enhance: remove history filter only on successful remote branch deletion (#2533) |
| `2e931dd8` | 2026-07-14 | SCAN_PROGRESS | 採用 | ux: show registering progress after scanning repositories (#2534) |
| `ba599a8d` | 2026-07-15 | EXISTING | 反映済み | ux: close `AIAssistant` dialog immediately after applying the generated message (#2535) |
| `7f1688b5` | 2026-07-15 | AI_OPTIONS | 採用 | enhance: include current branch name in commit message generation prompt |
| `d70e4f46` | 2026-07-15 | RAW_DIFF | 最小採用 | fix: extra `CR` marker at the end of the last line in text diff view (#2536) |
| `50531eff` | 2026-07-16 | GITFLOW | 差し替え採用 | feature: support to choose the start point of `git-flow` topic branch (#2538) |
| `f5917d19` | 2026-07-16 | GITFLOW | 差し替え採用 | ux: default focus the new topic branch name |
| `31d14114` | 2026-07-16 | POPUP_FOCUS | 最小採用 | ux: change focus behaviour of `DealWithLocalChangesMethod` control |
| `150426b8` | 2026-07-16 | POPUP_FOCUS | 最小採用 | code_style: remove unnecessary calling for `UpdateRadioButtons` |
| `a7352aa5` | 2026-07-16 | POPUP_FOCUS | 最小採用 | ux: change `KeyboardNavigation.TabNavigation` to `Cycle` for popup panel |
| `93957058` | 2026-07-16 | FILTER_BAR | 差し替え採用 | feature: support to collapse history filters bar (#2540) |
| `f87974b3` | 2026-07-16 | META | 対象外 | doc: Update translation status and sort locale files |
| `7f17ba86` | 2026-07-16 | FILTER_BAR | 差し替え採用 | ux: new style for history filter bar expander |
| `d2d42299` | 2026-07-16 | META | 対象外 | doc: Update translation status and sort locale files |
| `ca6cb3aa` | 2026-07-17 | UPDATE | 却下 | ux: checking update at startup (#2438) |
| `d54a1180` | 2026-07-17 | META | 対象外 | doc: Update translation status and sort locale files |
| `4257e0fc` | 2026-07-17 | LOCALES | 後日精査 | localization: update Traditional Chinese translations (#2541) |
| `dfe3fe0f` | 2026-07-17 | CANCEL | 差し替え採用 | code_style: command cancellation |
| `c30eeadb` | 2026-07-20 | LOCALES | 後日精査 | localization: update Russian translate (#2549) |
| `7100a7b1` | 2026-07-20 | META | 対象外 | doc: Update translation status and sort locale files |
| `66a0b0c1` | 2026-07-20 | LOCALES | 後日精査 | localization: complete Indonesian translations (#2550) |
| `7aa1bb14` | 2026-07-20 | MACOS_INTEGRATION | 採用・実機未検証 | feature: support terminal `Otty`, `cmux` and `mux0` on macOS (#2545) |
| `6dab78b8` | 2026-07-20 | COMMIT_GUIDE | 最小採用 | ux: lines in commit message editor box |
| `e0e2ab6c` | 2026-07-21 | IPC | 最小採用 | fix: use DataDir hash in IPC pipe name to isolate instances with different data dirs (#2552) |
| `59aadc51` | 2026-07-21 | WIP | 採用済み | enhance: fallback to `en_US` when `preferences.json` contains an invalid locale key (#2551) |
| `6744e06f` | 2026-07-21 | SUBMODULE_DIFF | 最小採用 | code_style: use a single command to query submodule info |
| `a935f813` | 2026-07-21 | CHILDREN | 却下 | refactor: drop support to show children in commit details panel |
| `acb95266` | 2026-07-21 | LOCALES | 後日精査 | doc: Update translation status and sort locale files |
| `67e7ecbb` | 2026-07-21 | WIP | 採用済み | fix: only start tab drag on left mouse button press (#2554) |
| `807c69be` | 2026-07-21 | LOCALES | 後日精査 | localization: update Spanish translation (#2555) |
| `0e06ce0a` | 2026-07-22 | META | 対象外 | doc: Update translation status and sort locale files |
| `614ffb02` | 2026-07-22 | COPY_SHA | 採用 | feature: allow to copy parent SHA in commit details panel |
| `ba56baf0` | 2026-07-23 | MULTI_BRANCH_DELETE | 採用 | feature: add an option to enable `--force` when deleting multiple branches |
| `6f670b19` | 2026-07-23 | MACOS_INTEGRATION | 採用・実機未検証 | feature: support `open -a SourceGit <path>` on macOS (#2553) |
| `87a506ca` | 2026-07-23 | STYLE | 却下 | code_style: remove unnecessary code |
| `cee9115f` | 2026-07-25 | RAW_DIFF | 最小採用 | refacto: diff result parsing (#2562) |
| `4b27bd30` | 2026-07-25 | FILTER_PLACEHOLDER | 反映済み | ux: add a placeholder for filter-change TextBox (#2565) |
| `bca63c48` | 2026-07-25 | META | 対象外 | doc: Update translation status and sort locale files |
| `cd614c8c` | 2026-07-26 | CONFLICT_BINARY | 差し替え採用 | feature: support to detect conflict file state with binary file and hide `MERGE` button for non-text file conflict (#2566) |
| `55255324` | 2026-07-26 | CONFLICT_BINARY | 差し替え採用 | code_style: remove unnecessary `_canMerge` and use `_state` instead. |
| `264e8fe3` | 2026-07-26 | LOCALES | 後日精査 | localization: update Chinese translations (#2570) |
| `877c1802` | 2026-07-26 | WIP | 採用済み | enhance: prefer exact match over suffix match when auto-selecting tracking branch for worktree (#2569) |
| `50a99e7b` | 2026-07-26 | LOCALES | 後日精査 | localization: update Russian translate (#2571) |
| `9a9b62c7` | 2026-07-26 | META | 対象外 | doc: Update translation status and sort locale files |
| `f547ea86` | 2026-07-27 | WIP | 採用済み | fix: account for amend when generating AI commit message (#2573) |
| `9ada4e12` | 2026-07-27 | AI | 部分採用・保留 | code_style: reduce token usage by remove `repo` parameter from tool call |
| `1ff1a4bb` | 2026-07-27 | META | 対象外 | version: Release 2026.16 |
| `a036d650` | 2026-07-30 | IPC | 最小採用 | fix: crash due to name of pipeline too long (#2576) |
| `edcf9148` | 2026-07-30 | WIP | 採用済み | fix(add-worktree): exclude current branch and branches with existing worktree (#2581) |
| `467d550b` | 2026-07-30 | META | 対象外 | docs: Update README.md with new linux instructions (#2577) |
| `d907a7a1` | 2026-07-30 | WIP | 採用済み | refactor: use `--rebase=false` when pulling with `Use rebase instead of merge` unchecked (#2582) |
| `3a2a4e04` | 2026-07-30 | COMMIT_GUIDE | 最小採用 | feature: change the `FontFamily` of commit message editor to monospace font and show column guide length indicator (80 chars) (#2586) (#2587) |
| `d3ff51b3` | 2026-07-30 | COMMIT_GUIDE | 最小採用 | ux: keep the `FontFamily` of placeholder in commit message editor to default |
| `750c5fd8` | 2026-07-30 | REMOTE_TAGS | 採用 | feature: allow adding remotes without fetching tags (#2580) |
| `4b4c22f4` | 2026-07-30 | CONFLICT_BINARY | 差し替え採用 | enhance: disable `textconv` when trying to test conflict file state (#2566) |
| `24fa13f7` | 2026-07-31 | WIP | 採用済み | fix: build folder path from Backend.Path and silence null-revision bindings in revision file tree (#2595) |
| `975be037` | 2026-07-31 | BLAME_UI | 最小採用 | code_review: PR #2595 |
| `342ccbb6` | 2026-07-31 | META | 対象外 | version: Release 2026.17 |
| `f153843d` | 2026-07-31 | WIP | 採用済み | fix: off-by-one in commit message subject length calculation (#2596) |
| `13a95cd0` | 2026-07-31 | WRAPPING | 差し替え採用 | fix: the last auto-wrap character gets cropped in commit message editor (#2597) |
| `18f178e0` | 2026-07-31 | WRAPPING | 差し替え採用 | enhance: resize text presenter when scrollbar appeared/disappeared |
| `fb2a9901` | 2026-08-03 | LOCALES | 後日精査 | localization: complete German translations (#2600) |
| `4ade74dd` | 2026-08-03 | META | 対象外 | doc: Update translation status and sort locale files |
| `4a970e67` | 2026-08-03 | LOCALES | 後日精査 | fix: remove stray placeholder in de_DE MergeConflictEditor.Title translation (#2601) |
| `22dbdf72` | 2026-08-03 | GRAMMARS | 採用 | feature: add syntax highlighting for `erlang` and `OCaml` (#2598) (#2602) |
| `1b8ce0e5` | 2026-08-03 | WRAPPING | 差し替え採用 | fix: when fallbacks to `Bounds.Width`, respect the padding of `TextBox` |
| `78ffc2bf` | 2026-08-03 | NOTIFICATIONS | 却下 | ux: pressing `ESC` will also clear notifications |
| `f7a4e66c` | 2026-08-03 | WIP | 採用済み | fix: fast checking local change list with `Amend` enabled must make sure the staged list does not changed too (#2563) |
| `5f611d95` | 2026-08-03 | STREAM | 採用 | code_style: make sure that all `Stream` objects will be disposed immediately after operation finished |
| `5f55e545` | 2026-08-04 | HEX | 差し替え採用 | feature: add a built-in hex-viewer to preview content of a binary file (#2593) |
| `56424e51` | 2026-08-04 | META | 対象外 | doc: Update translation status and sort locale files |
| `81b7cd4d` | 2026-08-04 | HEX | 差し替え採用 | enhance: make sure highlighted index is less than the file size |
| `2465f5f0` | 2026-08-04 | HEX | 差し替え採用 | feature: allows to goto input offset in hex-viewer |
| `40182d7e` | 2026-08-04 | META | 対象外 | doc: Update translation status and sort locale files |
| `401e6866` | 2026-08-04 | HEX | 差し替え採用 | code_style: it's not necessary to adding `0x` prefix when parsing a hex address |
| `b5e4b2cd` | 2026-08-04 | HEX | 差し替え採用 | enhance: scrolling accumulation |
| `3a0f6658` | 2026-08-04 | HEX | 差し替え採用 | refactor: use `git cat-file -s` instead of `git ls-tree <revision> -l -- <file>` to query filesize |
| `41b6f000` | 2026-08-04 | HEX | 差し替え採用 | feature: supports to view the content of binary file in right-side (NEW) of diff view |
| `fcf64417` | 2026-08-04 | META | 対象外 | doc: Update translation status and sort locale files |
| `c02570e4` | 2026-08-04 | HEX | 差し替え採用 | refactor: unify the behaviour to open the built-in hex-editor |
| `38d58f59` | 2026-08-04 | HEX | 差し替え採用 | refactor: move `ViewModels.BinaryFile` to `Models.BinaryFile` |
| `2a15e49b` | 2026-08-05 | HEX | 差し替え採用 | enhance: auto hide scrollbar in hex-viewer |
| `7695c807` | 2026-08-05 | TOOLBOX | 採用 | fix: detect IntelliJ IDEA and Android Studio from JetBrains Toolbox (#2606) |
| `1812df04` | 2026-08-05 | HEX | 差し替え採用 | enhance: allow closing hex-viewer by pressing `ESC` |
| `6dd54ccd` | 2026-08-05 | DELETE_TRACKING | 却下 | refactor: use old-style design to ask user to confirm deleting the tracking remote |
| `90ff73b1` | 2026-08-05 | LOCALES | 後日精査 | doc: Update translation status and sort locale files |
| `8190a8ff` | 2026-08-06 | STATUS_CANCELLATION | 差し替え採用 | enhance: force kill the previous `git status` process (#2358) |
| `82831eff` | 2026-08-06 | LOCALES | 後日精査 | localization: update Russian translate (#2607) |
| `8d7cde85` | 2026-08-06 | META | 対象外 | doc: Update translation status and sort locale files |
| `d31983f6` | 2026-08-06 | LOCALES | 後日精査 | code_style: remove unused translation |
| `674fd959` | 2026-08-06 | LOCALES | 後日精査 | doc: Update translation status and sort locale files |
| `16b965fb` | 2026-08-07 | WIP | 採用済み | fix: side-by-side diff scroll desync on hidden-tab rebuild and tab switch (#2611) |
| `4f11a7d7` | 2026-08-07 | AI_OPTIONS | 採用 | feature: support to customize `reasoning_effort` for AI chat (#2609) |
| `9961e364` | 2026-08-07 | META | 対象外 | doc: Update translation status and sort locale files |
| `b09b6d1b` | 2026-08-10 | WIP | 採用済み | fix: copy folder path when a folder is selected (#2614) |
| `c96e5a49` | 2026-08-10 | LOCALES | 後日精査 | localization: update Russian translate (#2615) |
| `b1bd30c8` | 2026-08-10 | META | 対象外 | doc: Update translation status and sort locale files |
| `f71b8d2a` | 2026-08-10 | GRAMMARS | 採用 | Add OCamllex and OCamlyacc syntax highlighting support (#2616) |
| `253fc498` | 2026-08-10 | GRAMMARS | 採用 | refactor: use external `swift` grammar instead of the built-in one in `TextMateSharp` (#2610) |
| `f54bcde7` | 2026-08-10 | DEPENDENCY | 個別管理 | project: upgrade AvaloniaUI to `11.3.19` |
| `6824bbca` | 2026-08-10 | FONTS | 却下 | code_style: move `FontFamily` resources to a new file `Fonts.axaml` |
| `ca1328e4` | 2026-08-11 | DEPENDENCY | 個別管理 | project: upgrade AvaloniaUI to `11.3.20` |
| `1d0ac637` | 2026-08-11 | SSH_AGENT | 最小採用 | enhance: enable `AddKeysToAgent` for `ssh` command |
| `6a32ba19` | 2026-08-11 | CHART | 却下 | ux: chart label style |
| `5410422a` | 2026-08-12 | WIP | 採用済み | enhance: when selecting `Parent Folder` in `Clone` dialog, respect the pre-filled path (#2618) |
| `ffea3e10` | 2026-08-12 | REMOTE_SWITCH | 採用 | ux: hide remote protocol switch control when input URL is empty |
| `f88fc0a7` | 2026-08-13 | INLINE_LIMIT | 採用 | Increase limit on how many inline-chunks are allowed per line. Also use named constants. (#2619) |
| `34321699` | 2026-08-16 | LOCALES | 後日精査 | localization: update Spanish translation (#2625) |
| `89e49b72` | 2026-08-17 | META | 対象外 | doc: Update translation status and sort locale files |
| `9ecb440f` | 2026-08-17 | META | 対象外 | version: Release 2026.18 |
| `e614c889` | 2026-08-17 | REFNAME | 差し替え採用 | ux: use `BranchOrTagNameTextBox` in `Push to a NEW Branch` dialog |
| `1218e116` | 2026-08-18 | NO_VERIFY | 採用 | feature: add `--no-verify` option to push command (#2630) |
| `f4a6217a` | 2026-08-18 | META | 対象外 | doc: Update translation status and sort locale files |
| `aba413d1` | 2026-08-17 | REFNAME | 差し替え採用 | feat: use the same rules as git for branch and tag name validation (#2626) |
| `b68825a3` | 2026-08-18 | STYLE | 却下 | ux: keep the most dangerous option `Force Push` as the last one |
| `d43ce179` | 2026-08-18 | REFNAME | 差し替え採用 | enhance: check new branch name in `Push to a NEW branch` dialog |
| `8317794e` | 2026-08-18 | CANCEL | 差し替え採用 | feature: support to ternimate running clone/fetch/pull/push command on Windows |
| `ea8f7ddb` | 2026-08-18 | META | 対象外 | doc: Update translation status and sort locale files |
| `36c0e216` | 2026-08-19 | WIP | 採用済み | fix: `IsLFSEnabled` does not support submodule/worktree (#2635) |
| `415564bb` | 2026-08-19 | CANCEL | 差し替え採用 | enhance: do not wait for the cancel operation finished (the UI will auto wait for `Sure` complete) |
| `4fca32f1` | 2026-08-20 | WIP | 採用済み | fix: split shell exec pattern for terminal file picker on Windows (#2637) |
| `3b9f55f2` | 2026-08-20 | CANCEL | 差し替え採用 | feature: using `setsid` to support terminating clone/fetch/pull/push on Linux gracefully |
| `1fd81089` | 2026-08-20 | FILTER_BAR | 差し替え採用 | refactor: history filter icon |
| `7a58d350` | 2026-08-21 | BLAME_UI | 最小採用 | ux: new blame style |
| `f9ec08b3` | 2026-08-21 | PREVIEW_SYNTAX | 最小採用 | enhance: always enable syntax highlighting in file preview (just like blame) |
| `3d281f28` | 2026-08-24 | GRAPH | 差し替え採用 | feature: add new graph-highlighting option "Selected Commits (only first-parent)" (#2646) |
| `f4ef0ee9` | 2026-08-24 | META | 対象外 | doc: Update translation status and sort locale files |
| `8ab62dab` | 2026-08-24 | LOCALES | 後日精査 | localization: update translations for new `Selected Commit (First-parent Only)` highlighting mode |
| `57c494a2` | 2026-08-24 | META | 対象外 | doc: Update translation status and sort locale files |
| `717a7df7` | 2026-08-24 | GRAPH | 差し替え採用 | refactor: rewrite the code to calculate highlighting state of a commit |
| `0e129cbc` | 2026-08-24 | GRAPH | 差し替え採用 | enhance: calculate the merge state for history commits in background thread instead of UIThread |
| `98ba6428` | 2026-08-24 | WIP | 採用済み | enhance: remove extra empty line in `git blame` result |
| `e308279e` | 2026-08-24 | XDG | 最小採用 | fix: maintain compatibility with older versions |
| `aad04037` | 2026-08-24 | XDG | 最小採用 | feature: use XDG standard directories on Linux |
| `7220b141` | 2026-08-24 | META | 対象外 | doc: Update translation status and sort locale files |
| `0c342cb5` | 2026-08-24 | XDG | 最小採用 | fix: make sure the target directories exists before migartion |
| `2464fa40` | 2026-08-24 | XDG | 最小採用 | fix: preview does not work in design mode |
| `a984953a` | 2026-08-24 | SSH_HELPER | 最小採用 | feature: add a built-in `SSH Key Helper` (#2647) |
| `5e6bc57f` | 2026-08-24 | META | 対象外 | doc: Update translation status and sort locale files |
| `3d61055d` | 2026-08-24 | STYLE | 却下 | code_style: remove warnings in Rider |
| `45b66569` | 2026-08-25 | IPC | 最小採用 | refactor: process lock file storage path |
| `9ca439dc` | 2026-08-25 | EXISTING | 反映済み | ux: re-order menu items on macOS |
| `9bfe28de` | 2026-08-25 | NAVIGATION | 採用 | feature: opening submodule next to its parent page |
| `e3c93841` | 2026-08-25 | COMPACT_REFS | 採用 | ux: use `cloud` icon instead of a `+` prefix for remotes in compact commit decorator |
| `1445b53f` | 2026-08-26 | COMPACT_REFS | 採用 | ux: when the repository only has one remote, hide the remote name in compact decorator |
| `bc3ee043` | 2026-08-26 | WIP | 採用済み | code_style: fast check when the local changed file list does not change |
| `faf98d87` | 2026-08-26 | IPC | 最小採用 | enhance: remove process lock file after the first instance exited |
| `89ce9b10` | 2026-08-26 | CANCEL | 差し替え採用 | refactor: ignore errors when the process already exited |
| `de8e2111` | 2026-08-27 | SELECTION | 採用 | enhance: auto-select the file after picking one from suggestion dropdown |
| `8fbda46a` | 2026-08-26 | CANCEL | 差し替え採用 | feature: support terminate clone/fetch/pull/push gracefully on macOS |
| `3d8d6f21` | 2026-08-27 | SSH_HELPER | 最小採用 | enhance: startup location when selecting private SSH key |
| `141b2993` | 2026-08-27 | WIP | 採用済み | ux: add `Ctrl+Insert` copy shortcut in diff view (#2657) |
| `8d0e2d37` | 2026-08-27 | SSH_HELPER | 最小採用 | code_style: cleanup code about SSH key helper |
| `864c42b6` | 2026-08-28 | SSH_HELPER | 最小採用 | ux: confirm dialog for deleting SSH key should be shown on top of SSH key helper window |
| `b14de177` | 2026-08-28 | SSH_HELPER | 最小採用 | feature: support to generate RSA (4096-bits) SSH key |
| `1a3e930b` | 2026-08-28 | META | 対象外 | doc: Update translation status and sort locale files |
| `2ad7d7b4` | 2026-08-28 | NAVIGATION | 採用 | feature: support using mouse `XButton1` to go to previous page and `XButton2` to go to the next page (#2643) |
| `9fb864b2` | 2026-08-28 | BLAME_UI | 最小採用 | ux: commit info in `Blame` view |
| `ce072094` | 2026-08-28 | CANCEL | 差し替え採用 | feature: support to terminate `Fetch <upstream> into <local>` |
| `5bbf965d` | 2026-08-30 | WIP | 採用済み | fix: crash when trying to checkout a remote branch with a local branch has no upstream (#2664) |
| `c59f7bf0` | 2026-08-31 | META | 対象外 | version: Release 2026.19 |
