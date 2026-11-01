# 上流同期記録（2026-11-01）

対象は `sourcegit-scm/sourcegit` の `5c53601a`（前回 v2026.19）から `c0f40a2e`（master / v2026.23）まで。merge を除く203コミットを照合した。作業開始時の Komorebi HEAD は `84d633c2`。すべてローカル未コミットであり、ブランチ作成・push・リリース・バージョン更新は行っていない。

## 判定と採用範囲

| 区分 | 件数 |
| --- | ---: |
| 今回移植・集約適用 | 81 |
| 既存実装で同等の動作を維持 | 17 |
| 後続変更・現在の翻訳状況で置き換え | 49 |
| 独自設計と非互換、純外観、配信元固有のため不採用 | 56 |

以下の `applied` は手動移植・複数コミットの集約、または既存同等（理由欄に明記）を含む。上流ファイルの全面置換やSHA単位のcherry-pickではない。全行の Komorebi 側コミットは未確定。

主な採用内容:

- Git監視の packed-refs / overflow / stash reflog、単一statusプロセス、SSHキー指定時のprune、Bitbucket URL、ターミナル引数・フォルダー名・Alacritty。
- pushRemoteと任意push URL、worktreeへのブランチ指定、グループ自動選択、検索・フォーカス・stage/unstageショートカット、Blame選択、タブ状態とタイトル更新。
- 統計の著者別円グラフ。既存のLiveCharts時系列グラフ、独自テーマ、サイドバーと統一ツールバー、Viewの再利用を維持。
- リポジトリ所有権エラーでの明示的な信頼確認。診断文中の偽コマンドから信頼範囲を拡大しないパス照合、中断後のオープン停止、既存Bookmark保持を追加。
- 4種のAIプロバイダーへ追加HTTPヘッダーを適用。APIキーと同じ暗号化・暗号文保持・移行を使い、Contentヘッダーも扱う。
- カタルーニャ語を追加し、18辞書・各1343キーへ統一。上流翻訳398件を文意一致で集約し、以前英語で補完されていた値も翻訳。必須入力・通知・検索・再起動タイトル等の直書き表示をリソースへ接続。

統合検証で見つかった検索テンプレート生成前の結果表示漏れと、正規表現文字を含むpush URLの消去失敗も修正した。push設定コマンドの失敗を保存成功として扱わない。

## 検証と限界

| 検証 | 結果 |
| --- | --- |
| Releaseビルド | 警告0・エラー0 |
| Komorebi.Tests | 1455成功・失敗0・skip0 |
| 同梱AvaloniaEdit demo / solution build | 警告0・エラー0 |
| 同梱AvaloniaEdit NUnit | 215成功・失敗0・skip0 |
| UIWorkflow.E2E | 11シナリオ成功。全18言語の実辞書と引数、検索の即時言語切替、初回設定の空入力、信頼・中断・Bookmark、円グラフ、push URL保持/消去も含む |
| Recovery.E2E | 6シナリオ成功。4プロバイダーの実ローカルHTTP、OpenAIの429再送、ヘッダー暗号化・読込不能時の保持・復旧も含む |
| CloneLongPaths.E2E | 14シナリオ成功 |
| localization-check.js | 18言語すべてキー充足。参照キー未定義0・数値placeholder整合 |
| dotnet format / git diff --check | 成功 |

再現コマンドは各E2EのREADMEとAGENTS.mdを参照。最終成果物:

- UI: `tests/UIWorkflow.E2E/bin/Release/net10.0/artifacts/5e0dcacc62904ca3a92284ba7fb94f9c/`
- Recovery: `tests/Recovery.E2E/bin/Release/net10.0/artifacts/3525024897604c56a462ae49fb87d96e/`
- Clone: `tests/CloneLongPaths.E2E/bin/Release/net10.0/artifacts/25e3d75f68a94cf197c69ac04d4cd8a9/`

各ディレクトリの `result.json`、実fixture、画像・実行ログを保持する。途中の失敗・中断実行も同じartifacts配下に保持している。途中のUI検証1件はテスト側の同期waitで停止したため、UIスレッドを処理する待機へ修正して再実行した。

Windowsでは隔離したportable設定で実アプリを起動し、日本語の初期設定・必須入力エラー・設定完了を確認した。マウス注入はホストのDwmFlushエラーがあったため、実デスクトップ操作はキーボードとUIAで確認し、ポインター・ホバーはHeadless E2Eで検証した。macOS/Linuxの実画面、外部SSH、外部AI API、18言語すべての母語話者による品質審査は未実施。

初回統合検証時の既存lockfileは別のRID/ランタイムパッチ構成で、SDK 10.0.403からの `--locked-mode` 復元はNU1004になった。今回は依存・バージョン更新を行わず、lockfileとmanifestは維持。検証用に外部の未作成 `NuGetLockFilePath` と `RestorePackagesWithLockFile=false` を指定して復元し、その後 `--no-restore` でビルドした。ローカライズCLIの既存依存も既存pnpm lockから隔離領域へ復元した。

初回清掃では一時ファイル15件をPrunletで清掃した。検証用コピー・依存・ログの一時領域 `C:\Users\IMT\.codex\komorebi-sync-20261101` は、VBCSCompiler（観測PID 42208）のAvalonia analyzer使用中にPrunletが `ambiguous / HRESULT 0x80270028` を返したため、自動再削除していない。コンパイラが解放した後、元パスとWindowsのゴミ箱を照合してから手動清掃する。必要なログ・結果は上記UI成果物へコピー済みで、この一時領域は納品・再検証に不要。結果の原文はUI成果物の `cleanup.json` に保持。

## 2026-11-01 追補: lockfileと一時領域の残件解消

上記の固定復元不整合と一時領域の清掃未確定を解消した。

- `Directory.Build.targets` で依存グラフを分離。既存5つの `packages.lock.json` は非RID Debug用に再生成し、Release用5つ、本体のRelease.NoAOT用1つ、Release.win-x64用1つを追加。構成を切り替えてもDebugの正本を上書きしない。明示的なNuGetLockFilePathは尊重する。
- 本体・単体テスト・3 E2EのDebug/Release、RecoveryのRelease+DisableAOT、元のRelease/win-x64がすべてlocked restore成功。Debug/Releaseの通常ビルドもRestoreLockedMode=trueで警告0・エラー0、1455テスト成功。ビルド前後のlockハッシュも不変。パッケージ指定と製品バージョンは変更していない。
- CIと依存検証もRestoreLockedMode=trueを明示。リリース後のDebug lock強制再生成を廃止し、固定復元の検証だけに変更した。リリーススクリプトはAST構文検証のみで、配信は実行していない。
- VBCSCompilerを `dotnet build-server shutdown --vbcscompiler` で正常停止。元パスとゴミ箱を照合後、Prunletで一時領域を清掃し `recycled` を確認。元パスの消失も確認した。必要な結果・ログはUI成果物へ保持。

以後SDK由来のAOTツールが更新されたり、新たな構成/RIDを使う場合は対応するlockを通常復元で生成・レビューした後に固定復元する。未知の構成の固定復元を自動解除しない。

## レビュー指摘5件の修正（gogo）

元レビューのR1〜R5を先に反証し、すべて成立と判定した。既存差分を保持し、次の範囲を修正・検証済み。

| ID | 採否・修正 | 検証根拠 |
| --- | --- | --- |
| R1 | 差し替え採用。未読込リモートを保存せず、世代で古いloadを破棄。全config書込前に先行保存・loadを待つ。保存中の編集とClose再入も抑止 | 実VM・実Gitで生成直後/連続切替のpushURL・no_push・SSHを保持。実AXAML Windowでload中の入力無効化、完了後の復帰、二重Closeを確認 |
| R2 | 採用。pushRemoteへ別リモートのmergeを転用せず、選択リモート内のupstream完全一致→同名を選択 | 隔離remoteへの実Pushでorigin/feature更新とorigin/main不変。同一remoteのupstreamも維持。Git自身のdry-runでsimple/current/upstream契約を照合 |
| R3 | 採用。Linux用Release lockを追加し、locked時のlock欠落をNuGet実行前に拒否 | 修正前はmissing lockでもexit0/新規生成、修正後はexit1/生成なし。Linux package graphのlocked restore成功・ハッシュ不変 |
| R4 | 採用。status失敗時、bareのみ従来のbranch情報へfallback | 実Gitでbareのstatus exit128を再現。通常/bareのbranchとahead1/behind1、unborn・detached・invalid pathを実Commandで確認 |
| R5 | 採用。background statusの--no-optional-locksを復元 | 実Gitでtracked fileのstat情報を変えてもindexのバイト列が不変。bare fallback後のArgs復元も確認 |

最終Releaseビルドは警告0・エラー0、単体1460件が成功（失敗・skipなし）、UIWorkflow E2Eは12項目すべて成功、format・diff --checkも成功。修正担当と別のfresh文脈で静的な独立検証を行い、追加の保存順序修正も含めR1〜R5通過を確認した。

最新E2E・実fixture・画像・ログ: `tests/UIWorkflow.E2E/bin/Release/net10.0/artifacts/b5a2531ff6c3484195ac7598e3f31cb3/`。R3はWindows SDKでLinuxのSDK RID/target RIDを指定したpackage graph検証であり、native Linux buildは実行していない（接続先Linuxにはdotnet未導入）。UIはHeadlessの実Window/AXAMLで検証し、今回の修正についてnative desktop・外部SSH・公開Pushは実行していない。

検証途中のCLI引数誤り・旧テスト先行起動の結果は最終合格へ流用せず、呼出し・順序を直して再実行した。初回E2Eで検出したconfig保存競合も修正し、以後の実行で解消を確認。必要なGit fixtureとログを最新E2E成果物へ保持し、一時領域 `C:\Users\IMT\.codex\komorebi-gogo-20261101` はPrunletで清掃（recycled）。コミット・push・リリースは未実施。

## SHA別の判定

| 上流SHA | サマリ | 状態 | 理由 |
| --- | --- | --- | --- |
| `b9ce510c` | fix: hide suggestion box when the parent `TextBox` is invisible (#2662) | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `a4f3fa70` | refactor: selection change by folder will never show the diff results (#2661) | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `29b9db98` | refactor: context menu for selected changes (#2661) | declined | 既存の文脈メニュー・コピー通知・SSH解決・コンバーターを維持し構造だけの置換を避ける |
| `0e83ccd2` | enhance: extract reusable CopyButton control with copy feedback (#2668) | declined | 既存の文脈メニュー・コピー通知・SSH解決・コンバーターを維持し構造だけの置換を避ける |
| `ac1ce9b5` | refactor: block navigation after staging/unstaging/discarding a block(chunk) (#2659) | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `02799ba6` | ux: repository page layout (#2660) | declined | 統一ツールバー・ContentControlの寿命と既存レイアウトを維持 |
| `14cf80ef` | ux: enable `+tnum` font feature for datetime column in search result | declined | 独自テーマ・レイアウトを維持。純粋な外観変更は取り込まない |
| `e42fa32b` | enhance: group selector while cloning/opening repository (#2663) | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `1206ba6e` | fix: wrong CPU architecture while packaging macOS `x64` package (#2670) | declined | macOS x64を配信しておらず、ARM64 runnerでARM64を生成する既存経路を維持 |
| `3ddffca6` | localization: update Spanish translation (#2671) | applied | 英語正本の文意が一致する翻訳を集約適用。独自キーは保持 |
| `f0864387` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `dd476335` | fix: wrong result in diffing unstaged with staged binary files | applied | 既存同等: 既存の独自実装で同等の動作を維持 |
| `a343c087` | ux: add copy tooltip to notification copy button in LauncherPage (#2666) | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `066b8d28` | fix: app crash due to `VisualLinesInvalidException` exception raised in `AvaloniaEdit` (#2672) | applied | 既存同等: 同梱AvaloniaEditを上流12.0.0へ統合済み。旧バージョン向け修正は重複適用しない |
| `750aa726` | ux: label of `TERMINATE` button was cropped (#2675) | applied | 既存同等: 既存の独自実装で同等の動作を維持 |
| `e23c0a23` | localization: update Russian translate (#2676) | applied | 英語正本の文意が一致する翻訳を集約適用。独自キーは保持 |
| `6fa5332e` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `7384cc12` | enhance: only cache recently used 256 avatars | declined | 既存の並行安全な1024件FIFOキャッシュを維持。上流の256件LRUへ置換しない |
| `0140e3fb` | localization: update Russian translate (#2677) | applied | 英語正本の文意が一致する翻訳を集約適用。独自キーは保持 |
| `25c638a6` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `e9d7d56b` | enhance: steal focus after popup showing | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `bec580eb` | feature: supports to collapse/expand the left-sidebar of repository page (#2368) | superseded | 後続060c05cbで上流も常時サイドバーへ変更。独自サイドバーを維持 |
| `17f96bf7` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `3f4f0ac9` | feature: pushing branch will respect git's `branch.<name>.pushRemote` configuration (#2281) | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `582c1a22` | refactor: repository remote | declined | 既存の文脈メニュー・コピー通知・SSH解決・コンバーターを維持し構造だけの置換を避ける |
| `9a8339b5` | feature: support to change `remote.<name>.pushurl` from context menu of selected remote (#2281) | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `51129808` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `f0296ef9` | ux: show counters in collapsed sidebar icon | superseded | 後続060c05cbで上流も常時サイドバーへ変更。独自サイドバーを維持 |
| `628572a4` | ux: collapsed sidebar hover/selected style | superseded | 後続060c05cbで上流も常時サイドバーへ変更。独自サイドバーを維持 |
| `081ac2e7` | ux: use larger icon in collapsed sidebar | superseded | 後続060c05cbで上流も常時サイドバーへ変更。独自サイドバーを維持 |
| `8554c8c2` | refactor: move some page-related buttons to its own page | declined | 統一ツールバー・ContentControlの寿命と既存レイアウトを維持 |
| `75473ddc` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `e828bb11` | ux: remove splitter after sidebar toggle button | superseded | 後続060c05cbで上流も常時サイドバーへ変更。独自サイドバーを維持 |
| `f83cdb17` | ux: add tooltip for collapsed sidebar buttons | superseded | 後続060c05cbで上流も常時サイドバーへ変更。独自サイドバーを維持 |
| `4fcfc8e5` | enhance: stop searching commits after collapsed sidebar | superseded | 後続060c05cbで上流も常時サイドバーへ変更。独自サイドバーを維持 |
| `62581b45` | enhance: expand sidebar when pressing `Ctrl+Shift+F` | superseded | 後続060c05cbで上流も常時サイドバーへ変更。独自サイドバーを維持 |
| `54613196` | enhance: statistics dialog (#2684) | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `319edbc2` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `7c29f01c` | refactor: blame navigation | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `6934836f` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `10e04f6d` | fix: wrong blame data when the first line of file is an empty line (#2685) | applied | 先頭空行修正は既存。binary blameの早期returnを追加 |
| `2f933185` | ux: update icons | declined | 独自テーマ・レイアウトを維持。純粋な外観変更は取り込まない |
| `8f4619b4` | feat: support custom HTTP headers in AI service configuration (#2687) | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `7f5d7a9e` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `20d7118f` | code_review: PR #2687 | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `688f2e85` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `a26348a5` | ux: avoid history option button being overrlapped by label of first column | applied | 既存同等: 既存の独自実装で同等の動作を維持 |
| `c2dd8835` | localization: remove duplicated key | superseded | 現在の全辞書の重複・キー一致を検証。独自キーを保持 |
| `9d99cadb` | project: upgrade `AvaloniaUI` to `11.3.21` | superseded | 既存Avalonia 12.1.4を維持。旧11.xへの変更は不要 |
| `e0a134e9` | enhance: clicking SHA in `Blame` window will also highlights the content comes from it | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `35cfc455` | fix: wrong submodule info when the revision does not exist in repo | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `b5eabd33` | feature: show opened repository status in `Tabs` command palette | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `9961512f` | ux: dropdown menu group header style in `HISTORY` configure button | declined | 独自テーマ・レイアウトを維持。純粋な外観変更は取り込まない |
| `a8338d07` | ux: update tree view mode icon | declined | 既存のツリー表示アイコンを維持 |
| `d6d69f6b` | ux: show warning icon in collapsed sidebar when we need to resolve conflicts | superseded | 後続060c05cbで上流も常時サイドバーへ変更。独自サイドバーを維持 |
| `a7dbf6c4` | ux: re-design layout for repository page | declined | 統一ツールバー・ContentControlの寿命と既存レイアウトを維持 |
| `e5e767af` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `e503c6d4` | ux: add tooltips for buttons | declined | 独自テーマ・レイアウトを維持。純粋な外観変更は取り込まない |
| `d5de6d07` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `640b33a5` | fix: keep local changes commit controls visible on short windows (#2690) | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `f391ee0c` | feature: support to select `*.diff` file as patch (#2691) | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `dda73126` | localization: update translations for selecting patch tips | applied | 英語正本の文意が一致する翻訳を集約適用。独自キーは保持 |
| `bae5b9ac` | ux: move `Create Branch` button back to the toolbar (#2693) | applied | 既存同等: 既存の独自実装で同等の動作を維持 |
| `6947af86` | ux: move search button to toolbar to reduce mouse movement | applied | 既存同等: 既存の独自実装で同等の動作を維持 |
| `db1143a9` | ux: re-design repository page | declined | 統一ツールバー・ContentControlの寿命と既存レイアウトを維持 |
| `6b904b06` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `90d9e2dd` | ux: re-design repository's page | declined | 統一ツールバー・ContentControlの寿命と既存レイアウトを維持 |
| `1819f6c1` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `ba91ecd1` | ux: move bisect state bar and history filter bar | declined | 統一ツールバー・ContentControlの寿命と既存レイアウトを維持 |
| `72d4c9a1` | localization: update Spanish translation (#2694) | applied | 英語正本の文意が一致する翻訳を集約適用。独自キーは保持 |
| `ae61c95c` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `ab125c72` | ux: change the description for hotkey `Ctrl/⌘+J` | applied | 既存同等: Ctrl+Jの説明は既存Text.CommitDetail.CollapseToBottomで表示。未使用キーの改名は不要 |
| `c9911e3e` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `09e7a1f2` | ux: commit search style | declined | 統一ツールバー・ContentControlの寿命と既存レイアウトを維持 |
| `9d595f67` | localization: update Traditional Chinese translations (#2683) | applied | 英語正本の文意が一致する翻訳を集約適用。独自キーは保持 |
| `e106840e` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `d7a5586b` | ux: re-design `Local Changes` page | declined | 統一ツールバー・ContentControlの寿命と既存レイアウトを維持 |
| `79c8088b` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `d5fd4118` | code_style: remove unnecessary control `LauncherPagesCommandPalettePageStatus` | declined | 未導入の専用controlの削除。既存コンバーターでタブ状態を表示 |
| `104ed5be` | ux: repository page main layout | declined | 統一ツールバー・ContentControlの寿命と既存レイアウトを維持 |
| `39949bb0` | ux: app style | declined | 独自テーマ・レイアウトを維持。純粋な外観変更は取り込まない |
| `8787f09a` | ux: app-style | declined | 独自テーマ・レイアウトを維持。純粋な外観変更は取り込まない |
| `073a3ca2` | style: change basic colors for light theme | declined | 独自テーマ・レイアウトを維持。純粋な外観変更は取り込まない |
| `979b4a07` | ux: theme for `Local Changes` and `Stashes` page | declined | 独自テーマ・レイアウトを維持。純粋な外観変更は取り込まない |
| `c5f7abce` | ux: remove unnecessary borders | declined | 独自テーマ・レイアウトを維持。純粋な外観変更は取り込まない |
| `16acef6b` | ux: use accent color instead of hardcoded `Red` for badge in page switcher | declined | 独自テーマ・レイアウトを維持。純粋な外観変更は取り込まない |
| `68e7502d` | ux: show a dot when current branch is ahead/behind its upstream | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `d5dbc83d` | localization: update Spanish translation (#2695) | applied | 英語正本の文意が一致する翻訳を集約適用。独自キーは保持 |
| `856d83fb` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `9c934bd5` | project: upgrade `AvaloniaUI` to `11.3.22` | superseded | 既存Avalonia 12.1.4を維持。旧11.xへの変更は不要 |
| `67d1b494` | fix: size working copy pane for commit push action (#2696) | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `ab762254` | style: hide the right border for `COMMIT TIME` column | declined | 独自テーマ・レイアウトを維持。純粋な外観変更は取り込まない |
| `a688da1a` | style: file history page | declined | 独自テーマ・レイアウトを維持。純粋な外観変更は取り込まない |
| `4c7e0e51` | style: light theme colors | declined | 独自Light/White等のテーマ配色を維持 |
| `b57556ca` | ux: show tracking status of current branch with its upstream in toolbar | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `9071940d` | style: change the dot color for push/pull | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `bc196738` | refactor: use a single `git status` process to query repository status in `Welcome` page | applied | Git/監視/OS連携の互換部分を移植。認証と中断処理を維持 |
| `04e1d6d1` | ux: use same icon size and margin in dashboard sidebar | declined | 独自テーマ・レイアウトを維持。純粋な外観変更は取り込まない |
| `b5bb3476` | fix: auto scroll to first change block in diff view after selecting a change (#2700) | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `85b57560` | style: filter box centered in sidebar | declined | 独自テーマ・レイアウトを維持。純粋な外観変更は取り込まない |
| `f1ef481a` | doc: update screenshots | declined | 上流の製品紹介・スクリーンショット・サービス案内固有。独自READMEを維持 |
| `fed9b7c9` | localization: update Japanese translation (#2702) | applied | 英語正本の文意が一致する翻訳を集約適用。独自キーは保持 |
| `d21ccfeb` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `4ba30a15` | ux: enable `+tnum` font feature for stash time (#2705) | declined | 独自テーマ・レイアウトを維持。純粋な外観変更は取り込まない |
| `5d4f4205` | version: Release 2026.20 | declined | 通常同期ではKomorebiのバージョンを変更しない |
| `2f9e4ed5` | feature: auto select the next change after discarding one (#2704) | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `bc074ec2` | ux: show matched changes count while searching changes in `LOCAL CHANGES` page (#2703) | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `016b31fb` | docs: document OrcaRouter as an OpenAI-compatible service (#2658) | declined | 上流の製品紹介・スクリーンショット・サービス案内固有。独自READMEを維持 |
| `ac7be73d` | revert: drop hidding `--tags` option for pushing with specific branch | applied | 既存同等: 既存の独自実装で同等の動作を維持 |
| `851f07f7` | enhance: pushing branch | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `e605d339` | style: search method ComboBox style | declined | 独自テーマ・レイアウトを維持。純粋な外観変更は取り込まない |
| `92728a7f` | ux: macOS 27 traffic light position | declined | 独自フレームには上流のnative traffic-light位置調整が存在しない |
| `a986a6a0` | fix: prune remote does not work with configured ssh key (#2713) | applied | Git/監視/OS連携の互換部分を移植。認証と中断処理を維持 |
| `d93b7b89` | fix: Remote name with uppercase letters is forced to lowercase (#2715) | applied | 既存同等: 共通プロセス中断・大文字小文字を保持する設定読込で対応済み |
| `5aed8ec2` | enhance: commit searching (#2711) | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `fa432d4b` | style: use same corner radius | declined | 独自テーマ・レイアウトを維持。純粋な外観変更は取り込まない |
| `58a86bb6` | ux: move clean up button from sidebar back to toolbar | applied | 既存同等: 既存の独自実装で同等の動作を維持 |
| `1e5f4947` | fix: failed to generate SSH key when `~/.ssh` does not exist (#2721) | applied | 既存同等: 既存の独自実装で同等の動作を維持 |
| `060c05cb` | style: always show repository sidebar | applied | 既存同等: 既存の独自実装で同等の動作を維持 |
| `0e0ee6db` | fix: `Ctrl+Shift+D` Open in external mergin tool opening wrong version of file (#2724) | applied | 既存同等: 既存の独自実装で同等の動作を維持 |
| `e29a7802` | refactor: repository sidebar | declined | 統一ツールバー・ContentControlの寿命と既存レイアウトを維持 |
| `0bd39931` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `4fcc69ed` | ux: better tooltip position (#2710) | declined | 独自テーマ・レイアウトを維持。純粋な外観変更は取り込まない |
| `6e897147` | ux: show a tip when no file is selected in `FILES` page | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `c5a13120` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `54fee2d8` | refactor: keep `Set as tracking branch` unchecked by default (#2730) | applied | 既存同等: 既存の独自実装で同等の動作を維持 |
| `8d959c59` | refactor: use `Models.Null` instead of `TargetNullValue` | declined | 既存の文脈メニュー・コピー通知・SSH解決・コンバーターを維持し構造だけの置換を避ける |
| `c9fdddcc` | ux: re-design `Local Changes` view & use the same change view mode for all | declined | staged/unstaged等の個別表示設定を失うため統一モードへ移行しない |
| `fad68d5c` | refactor: auto-select the first conflict in tree/list/grid (#2729) | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `f7f5fe9b` | feature: allow cherry-pick on merged commit (#2725) (#2726) | applied | 既存同等: 既存の独自実装で同等の動作を維持 |
| `0b7262e9` | ux: tooltip position | declined | 独自テーマ・レイアウトを維持。純粋な外観変更は取り込まない |
| `62c3d9fe` | fix: Exec property in flatpak desktop file now points to correct executable (#2732) | applied | FlatpakのExecを実在するkomorebiコマンドへ修正 |
| `aafc5453` | fix: hide console window when launching external tools (#2731) | applied | Git/監視/OS連携の互換部分を移植。認証と中断処理を維持 |
| `564a1c49` | version: Release 2026.21 | declined | 通常同期ではKomorebiのバージョンを変更しない |
| `98e5767f` | ci: sign macOS package (#1833) | declined | GitHub向け署名CIは移植せず、既存R2/Velopack配信・署名経路を維持 |
| `2e024612` | fix: `HasPendingPullOrPush` dirty state not updated after checkout (#2734) | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `b777993a` | code_style: remove unnecessary converters | declined | 既存の文脈メニュー・コピー通知・SSH解決・コンバーターを維持し構造だけの置換を避ける |
| `5db0acf3` | fix: sometimes stashes not updated after running `reflog expire --expire=now --all` | applied | Git/監視/OS連携の互換部分を移植。認証と中断処理を維持 |
| `5fae6996` | enhance: worktree | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `c7db6b3e` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `f7041b9a` | refactor: worktree | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `8be9a660` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `0b253cc5` | enhance: clear stale commit message after an external operation ends (#2741) | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `5b06cd10` | localization: remove unused translations | superseded | 現在の全辞書の重複・キー一致を検証。独自キーを保持 |
| `247c2272` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `bfd8c400` | enhance: pre-fill branch name when checking out a remote branch as worktree | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `0317ba2c` | style: use another way to set global default font size (#2708) | declined | 既存のフォントサイズ継承・bindingを維持 |
| `18d8d7f2` | ux: add a running icon in in-progress bar when there's a command running in background | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `29bc9949` | localization: update Russian translate (#2744) | applied | 英語正本の文意が一致する翻訳を集約適用。独自キーは保持 |
| `694fff5e` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `d51cd410` | enhance: terminate `git status` command gracefully (#2718) | applied | 既存同等: 共通プロセス中断・大文字小文字を保持する設定読込で対応済み |
| `9752df31` | feature: confirm before trusting a repository refused by git (#2746) | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `ec12af36` | code_review: PR #2764 | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `a5e30ec3` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `9bdfd4fb` | localization: update Spanish translation (#2749) | applied | 英語正本の文意が一致する翻訳を集約適用。独自キーは保持 |
| `873b7fcd` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `3c4dd306` | feature: add a pie-chart for top contributors | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `fd9c2ad4` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `6b852ea3` | localization: update Traditional Chinese translations (#2750) | applied | 英語正本の文意が一致する翻訳を集約適用。独自キーは保持 |
| `e302d843` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `98d4d850` | ux: show page skeleton when the samples is empty in `Statistics` page | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `1d330703` | ux: use theme foreground color | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `64017291` | ux: another way to show hovered part in pie-chart | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `be90759d` | ux: allow to deselect selected author in Statistics window (#2751) | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `35136575` | enhance: clear hovering tip after that mouse leaved | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `dbf2837e` | feature: allow to maximize `Statistics` dialog by double-clicking the titlebar | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `b30fe710` | ux: add a tooltip for top authors | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `6ad3c8a0` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `06c7c539` | docs: document Cheaper Inference as an OpenAI-compatible service (#2755) | declined | 上流の製品紹介・スクリーンショット・サービス案内固有。独自READMEを維持 |
| `783f0ba9` | localization: update Traditional Chinese translations (#2756) | applied | 英語正本の文意が一致する翻訳を集約適用。独自キーは保持 |
| `4433baf4` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `dbfde1a0` | ux: pie-chart | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `5706d2cb` | localization: update Spanish translation (#2759) | applied | 英語正本の文意が一致する翻訳を集約適用。独自キーは保持 |
| `dc0754a3` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `b256212a` | enhance: embed AppImage update information and publish zsync files (#2762) | declined | GitHub Releases向けAppImage更新URLはR2単独配信と互換性がない |
| `24bc8a5c` | fix: collapse shadow padding when window touches a screen edge (#2717) | declined | WindowsのPadding=0と独自resizeを維持。上流のshadow insets処理を移植しない |
| `3b952570` | fix: prevent leftmost x-axis label from being clipped in Statistics chart (#2556) | applied | 既存同等: 上流のcustom Chartを使用せず既存LiveChartsの軸表示を維持 |
| `b6473084` | code_style: use named class instead of tuple | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `415e1377` | fix: watcher missed ref updates stored in `packed-refs` or lost on buffer overflow (#2764) | applied | Git/監視/OS連携の互換部分を移植。認証と中断処理を維持 |
| `554bfaa2` | docs: add API Route AI service configuration (#2767) | declined | 上流の製品紹介・スクリーンショット・サービス案内固有。独自READMEを維持 |
| `c245ee63` | feature: support to open repository in system file browser from context menu of repository tab | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `d92e3707` | docs: document Opper as an OpenAI-compatible service (#2769) | declined | 上流の製品紹介・スクリーンショット・サービス案内固有。独自READMEを維持 |
| `f1e381fc` | localization: update Russian translate (#2770) | applied | 英語正本の文意が一致する翻訳を集約適用。独自キーは保持 |
| `e73e2cf7` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `43548d30` | version: Release 2026.22 | declined | 通常同期ではKomorebiのバージョンを変更しない |
| `0746d0b3` | fix: keep folder filter when checking out or creating a branch inside it (#2778) | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `7289ba4b` | feature: support to use `$FOLDER_NAME` when opening terminal (#2760) | applied | Git/監視/OS連携の互換部分を移植。認証と中断処理を維持 |
| `2f971879` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `fc44e7cb` | Add support for Alacritty Terminal integration on MacOS (#2781) | applied | Git/監視/OS連携の互換部分を移植。認証と中断処理を維持 |
| `f8c8897f` | code_style: fix warnings of `dotnet format` | declined | ローカルのコード様式を維持 |
| `a2217ea7` | feature: avoid pie-chart label overlapping | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `c950a6ad` | enhance: do not allow labels overlap the previous one | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `31118bb8` | feature: add hotkeys to stage/unstage all changes | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `4a62393d` | refactor: remove conflicted hotkeys | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `d8a71a36` | refactor: turn on `Set as tracking branch` when the local branch has no upstream | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `dc52ae3a` | fix: window title did not update after the name of workspace/repository changed (#2785) | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `3184f08f` | Add catalan translation (#2791) | applied | カタルーニャ語を追加し独自キーを翻訳、登録と設定画面を接続 |
| `bd97d060` | doc: Update translation status and sort locale files | superseded | 現在の18ロケールから翻訳状況を再生成 |
| `9d168f03` | fix: keep focus on filter textbox when clicking clear button (#2794) | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `e3187f66` | fix: map `ssh.bitbucket.org` remotes to `bitbucket.org` web URLs (#2801) (#2805) | applied | Git/監視/OS連携の互換部分を移植。認証と中断処理を維持 |
| `07bf3c5e` | ux: responsive ui | applied | 既存のVM・View・入力経路に合わせて動作を移植 |
| `b28f9229` | doc: remove Linux package provided by gadfly3173 (#2804) | declined | 上流の製品紹介・スクリーンショット・サービス案内固有。独自READMEを維持 |
| `f3545ef5` | version: Release 2026.23 | declined | 通常同期ではKomorebiのバージョンを変更しない |
