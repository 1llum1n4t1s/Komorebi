# Upstream Sync Log

Komorebi は [sourcegit-scm/sourcegit](https://github.com/sourcegit-scm/sourcegit) のフォーク。
上流に追従しつつ Komorebi 独自機能を保つため、定期的に cherry-pick batch を実施する。

このドキュメントは「**なぜこのコミットを採用 / スキップしたか**」のチーム共有メモ。
個人の `~/.claude/plans/` から脱却し、リポジトリ内に来歴を集約することで、
将来の開発者・新規参画者が「再評価から始める」コストを削減する。

## 同期方針

- **期間**: 上流の master を最低 1 ヶ月に 1 回フェッチして cherry-pick batch を作る
- **タグ運用**: `upstream/<YYYY-MM-DD>` のタグで上流時点をマーク
- **競合の指針**: 「Komorebi 独自実装は守る、上流バグ修正は採用、純粋スタイル変更は declined」
- **Komorebi 独自実装（competing 注意）**:
  - `App.RaiseException`（vs 上流 `Notification.Send`）
  - 統合 `WelcomeToolbar`（上流 `RepositoryToolbar` は削除）
  - `SSHKeyPicker` + `LegacySSHKeyOptOutSentinel`
  - CodeCommit URL handling（HTTPS / SSH / GRC 3 形式）
  - Anthropic AI provider
  - `SuperLightLogger` 連携
  - file-scoped namespace + 日本語 XML doc コメント
  - collection expression `[]`
  - `[DynamicDependency]` で AOT 安全な View 解決（移行中）

## 形式

各エントリは以下を含めること:

| 項目 | 説明 |
|---|---|
| 上流 SHA | `git log --oneline` 形式の短縮 SHA |
| 上流コミットメッセージ | 1 行サマリ |
| ステータス | `applied` / `declined` / `deferred` / `superseded` |
| Komorebi コミット | 取り込み先の Komorebi 側 SHA（applied のみ） |
| 理由 | declined / deferred の場合、なぜそうしたか |

## ログ

### 2026-07-02 バッチ（`fbe82dbf`..`upstream/master`, 353 commits 精査、v2026.14 まで）

前回 sync 点 `fbe82dbf`（2026-04-29 cherry-pick 分）から upstream `master` 先端（`8b1b6b2b` = v2026.14, 2026-06-29）までを精査。
内訳: 353件中 91件は純翻訳/doc bot/version release/merge のみでスキップ、262件を評価。
うち ux/refactor/code_style/style/resources 系 124件は個別記録を省略（bug 混入なしを軽くスキャン済み、低優先度バッチとして次回まとめて検討）。
残り 137件（fix/feature/enhance/project/perf/revert）を個別トリアージ。

最優先22件は同日中に実装まで完了(16件適用・3件Defer再分類・3件AlreadyPresent再分類、詳細は下表)。
副産物として CLAUDE.md の記載差異(Avalonia 12.0.2→実際は12.0.5、`depends/AvaloniaEdit` はsubmoduleでなくvendored)も修正済み。

#### Accept（cherry-pick 推奨）

**最優先: 実バグ / クラッシュ / ハング / リーク / データ不整合 — 2026-07-02 に実施済み**

実際に適用を試みたところ、6件は Komorebi 側の設計差異により「そもそもバグが発生しない/前提機能が未導入」と判明し
Defer・AlreadyPresent に再分類した(詳細は各項目の結果欄)。残り16件は手動移植 (`git cherry-pick` はほぼ全て
file-scoped namespace化によるコンテキスト不一致で conflict するため、実質は diff を見ながらの手動再現) の上、
main に直接コミット済み。

| 上流 SHA | サマリ | 結果 |
|---|---|---|
| `03e86f9c` | `InvariantGlobalization` 強制無効化 | ✅ 適用 `5acacacf` |
| `671635db` | 既存ブランチ上書き時の重複エントリ表示 | ⏭️ AlreadyPresent — `MarkBranchesDirtyManually()`が毎回`RefreshBranches()`で全ブランチ再取得するため重複が発生しえない |
| `8e17ff08` | `rebase --abort` 後の残留ファイル削除 | ✅ 適用 `002a801c` |
| `52d9270b` | 同一コミット再選択時のスクロール不具合 | ⏸️ Defer — upstream は `HistoriesCommitList:DataGrid` 分離クラス+`SelectedCommitsProperty` が前提。Komorebiはこのrefactor自体未導入(`Histories`が直接`OnPropertyChanged`+`NavigationIdProperty`で実装) |
| `fe57dd75` | macOS 以外で `Ctrl+\`` が使えない | ⏸️ Defer — 前提の `fa1e19bc`(ターミナル起動グローバルホットキー機能)自体が未導入。導入にはWelcome検索UI/ローカライズキー削除を伴う中規模リファクタが必要 |
| `5bbef394` | 無効な remote branch decorator でクラッシュ | ✅ 適用 `5b091793` |
| `d061a506` | Amend 有効時に Stash メニューが無効化されない | ✅ 適用 `8846dc58` |
| `5706b708` + `993c7858` | diff viewer のナビゲーション/hunk操作ホットキーが2つ目以降のインスタンスで無効 | ✅ 適用 `b8e5f7b9`(2件セット) |
| `e5b417f8` | commit graph レイアウト強制更新漏れ | ✅ 適用 `ee7c30f1` |
| `ec6736a6` | `LOCAL CHANGES` で選択テキストクリックするとクラッシュ | ✅ 適用 `13de2d9f` |
| `67b4de09` | マルチディスプレイ環境で `Logs` ダイアログのレイアウトが崩れる | ✅ 適用 `ff820d0e` |
| `f7c61cbb` | ブランチ作成ダイアログが Stash&Reapply の既定設定を無視 | ⏸️ Defer — 前提の `d4ce0b97`(Preferences>GITにチェックボックス追加)が未導入。導入には17言語ぶんのローカライズ追加を伴い単純なバグ修正の範囲を超える |
| `26218a7b` | コミットメッセージエディタの折り返し不具合 | ⏭️ AlreadyPresent — upstream は標準`TextBox`+`TextPresenter`固有バグ(Avalonia issue #5819)対策。Komorebiはコミットメッセージ入力にAvaloniaEditベースの`CommitMessageTextEditor`を使っており`TextPresenter`が存在せず無関係 |
| `dacc7e8e` | AvaloniaEdit `InvalidateLayer` が無条件に `InvalidateMeasure()` を呼ぶ | ✅ 適用 `0e6fc0bf`(6a47cec3とセット) |
| `6a47cec3` | 改行なしインジケータ選択時に構文ハイライト有効だとハング | ✅ 適用 `0e6fc0bf`(dacc7e8eとセット) |
| `c677d621` | `--ignore-blank-lines` オプション追加(空白行のみの変更を無視) | ✅ 適用 `1b4fba82` |
| `3c6e5390` | コミット後にサブモジュールの dirty 表示が更新されない | ✅ 適用 `e84660d9` |
| `baf2ed64` | staged+amend 変更のソート順が通常と異なる | ✅ 適用 `a703b24f` |
| `c6b942e4` | チェックアウト/作成/リネーム後に remote branch tree の展開状態が失われる | ⏭️ AlreadyPresent — `RefreshBranches()`が常にlocal+remote両方を完全再取得してから`BuildBranchTree`を呼ぶ設計のため、upstreamのようなlocalのみの部分リビルド(`BuildBranchTree(locals,[])`)が存在せず対象のバグが発生しない |
| `2fc3ec53` | 内部マージツールでコンフリクト解決時に余分な `\n` が残る | ✅ 適用 `35be464c` |
| `d2b6c13e` | Linux .desktop エントリの引数欠落(`%u` 等) | ✅ 適用 `fa8f46e6`(`komorebi.desktop`の_common/flatpak両方) |

**その他 Accept（機能追加・UX改善、低リスク・小規模）— 2026-07-02 に第2弾として実施済み**

6並列エージェント（worktree分離、領域別グループ G-A〜G-F）で手動移植 → main へ cherry-pick 統合。
計52 upstream コミット中 **46件適用**、6件は実装時に判明した設計差異で SKIP。
移植中に**実バグ1件を発見・修正**（`4d2c5f38`: file-scoped namespace 化の際に対話的リベースのジョブファイル名が
書き込み側 `sourcegit.interactive_rebase` / 読み取り側 `komorebi.interactive_rebase` に分裂し、対話的リベースが常に失敗する状態だった）。

適用済み（46件）:
`576b7fee` `a6800113` `a8ebfc3e` `ff3f81b2` `39668075` `fd709c44` `837f40df`（G-A）
`39fdc1af` `bd32c32b` `d4ce0b97` `f7c61cbb`（G-B、f7c61cbb は前回 Defer → d4ce0b97 とセットで解消）
`0938fd49` `ea51c414` `e6170f8a` `fbf1823b` `12d5fc62` `f9a7fefb` `26ff9215` `ef2854bc` `7e2aabb4` `a30cf17a`（G-C）
`00b95942` `44103f9b` `299622e1` `916160c7` `103dc010` `eebf4320`（前提として追加適用）`9d89012e` `9374af66` `8787ad1d` `adccb460` `d5ba5c05` `380b885c`（G-D）
`dd3e94fc` `eac1c113` `5a91017d` `93594f0e` `aa541333` `2982c197` `b224b0df` `cb6321a4` `04913f54`（G-E）
`823bde34` `dcee40c5` `8427859a` `c5e30e8f` `bb53fbbf` `57a3d694` `6909f220` `7e710d7c` `e761ff91` `e720d9fd`（G-F、e720d9fd は前回 Defer → BranchSelector の既存利用箇所が全て明示 TwoWay バインドと確認して適用）

実装時 SKIP（6件、理由つき再分類）:
- `d85f7093`: `--history` CLI 起動モード自体が未移植で対象コード不存在
- `1661d325`: 前提の `MergeTree` コマンド + conflict 事前テスト UI が未移植
- `0d0234be`: Komorebi は AI モデル auto-fetch 機構を持たず直接入力が既定 = 機能的に対応済み
- `8c3c0bec`: Theme Overrides のファイル選択 UI はフォークで削除済み、親要素なし
- `9e930de8`: `AdjustTrafficLightsForThickTitleBar` 自体が未移植
- `707a6a5a`: Komorebi の統計チャートは LiveChartsCore の自動スケーリングで対象コードなし
- `089eaaca`: Komorebi は upstream 側の Merge エントリ削除を追従していなかったため既に適用後と同一状態

ロケールキーは `dadb2c04` で18キーを17言語へ一括追加（upstream 翻訳が存在する12言語はそれを採用、
未翻訳言語は en_US フォールバック）。旧 bisect キー2件は全ロケールから削除。

#### AlreadyPresent（Komorebi は既に同等以上の対応済み — 確認のみ、対応不要）

| 上流 SHA | サマリ | 備考 |
|---|---|---|
| `7cce752c` | issue tracker リンクの unsafe browser target 対策 | Komorebi は `OS.cs:383-396` + `Windows.cs:244-263` で http/https/**mailto** 許可 + `UseShellExecute` 直叩きを**独自に先行実装済み**（upstream は http/https/ftp）。コメント付きで防御多重化もされている |
| `68c39800` | 自動フェッチ後に `IsAutoFetching` を確実に false へ戻す | Komorebi は `2c80edf5`/`b98f29db` で別レイヤーのデッドロック修正済み、かつ `IsAutoFetching` 側も try/catch/finally で upstream 以上に堅牢 |
| `3b61842d` | zh_TW `BranchTree.AheadBehind` のプレースホルダ修正 | Komorebi の zh_TW は既に `{1}` へ修正済み（独自ローカライズ管理） |
| `e3de9fd6` | ハイライト再計算の revert | 対象の最適化機構が Komorebi 未導入のため無関係 |
| `a0d51fee` | Avalonia 12.0.4 へのアップグレード | **Komorebi は既に Avalonia 12.0.5**（upstream はその後 11.3.18 に後退済み）で無関係 |
| `baa34ae9` | ブランチ名リネーム直後にツールバーが未更新 | `RenameBranch.cs` が独自の Watcher ロック + `MarkBranchesDirtyManually` で対応済み |
| `5a53e56b` | ブランチ作成直後にローカルブランチ数が即時反映されない | `CreateBranch.cs` も同じ `MarkBranchesDirtyManually` 経由で再計算済み |
| `f56ec6c6` | ページタブのスクロール挙動改善 | Komorebi は既に 200px 比例スクロールで upstream より洗練された実装済み |
| `e9a7eeb1` | AvaloniaEdit 追加アップグレード（12.0.0→12.0.4 相当パッチ） | Komorebi vendored 側で `SetCurrentValue` 化・`ColumnRulerPen` 等を独自に反映済み |

#### Defer（規模大 or 設計判断要、次回 sync で再評価）

- **git-flow-next 対応クラスタ**（`ff872070` `b3343626` `8de93c86` `7dea4b73` `904fef45` `2614f37b`）: git-flow / git-flow-next 両対応の機能追加一式。導入するか自体が方針判断
- **Rebase テスト機能**（`03248307`）: `Replay.cs` + `GitVersions.cs` 新設を伴う中規模機能、git バージョン要件あり
- **`sourcegit.node` 永続化機能**（`27468682`）+ 追従修正（`8272da4b`）: worktree/submodule の friendly name・bookmark を `$GIT_DIR/sourcegit.node` に保存。導入するならファイル名の Komorebi ブランディングも要検討。導入する場合のみ `8272da4b`（ディレクトリ削除時のクラッシュガード）もセットで Accept
- **Histories 詳細パネル折り畳み/別ウィンドウ化クラスタ**（`072acc60` `ac2a329e` `219f8e1d` `b3ddde33` `d31bba6a` `450b6a47` `d095fd36` `f96a7501` `890cfa85`）: upstream 内でも設計が数コミットで二転三転しており未成熟。安定するまで様子見
- **`Compare.cs` リファクタ**（`6e8b4005` `b046103e`）: コンストラクタが `repo.FullPath`(string) 保持から `Repository` オブジェクト保持に変更、`IsLoading`→`IsLoadingChanges` リネームも伴う中規模変更。着手前に referencing symbols 洗い出し要
- **`DiffContext.cs` リファクタ**（`e167f8e9`）: 62行規模の削除を伴う、diff viewer 間の表示設定同期変更
- **`ExecuteCustomAction` の `core.commentChar` 変更**（`e92810e7`）: rebase 中コミットメッセージで `#` 始まり行を保持する対応。`cherry-pick`/`revert`/`merge --continue` 含む4コマンドの Editor 種別変更を伴い、Komorebi 独自の `EditorType.RebaseEditor`/`CoreEditor` との相互作用を要検証
- **ファイルモード変更 tooltip 改善**（`81844e83`）: `OldMode`/`NewMode` を `string`→`int` に変える破壊的変更 + 新規 `FileModeChange.cs`（140行）
- **サードパーティ依存の一括アップグレード**（`b0f08263`, `ec7c4287`）: Komorebi 独自バージョン管理と要調整
- **CLI からの非 Git フォルダオープン時のポップアップ**（`4052b68b`）、**commit graph 生成シグネチャ変更**（`09ab27ea`）、**merge 事前検証機能**（`1faa3b1c`）、**検索候補 UI 新機能**（`381b44a4`）: いずれも中規模で設計判断要
- **隠し記号表示に改行コード表示を追加**（`aa8d4a2e`）: `TextDiffView.axaml.cs` への独自 `DrawText` 描画ロジック追加、実装コストは小さいが優先度低め
- **IME 変換中プレビュー修正**（`ff2aa0e9`）: sourcegit 本体は submodule pointer 更新のみ（`depends/AvaloniaEdit` を `cd3abfe2`→`64d65d57` へ）で中身は upstream フォーク `love-linger/AvaloniaEdit` 側。Komorebi vendored コードへの適用要否は同フォークの該当コミットを個別に確認してから判断

#### Skip（対象外・無関係）

- **Chart.cs 関連**（`fda413d5` `5b5c10a4` `df978183` `5cc615d6`）: upstream は自作 `Views/Chart.cs` レンダラー、Komorebi の `Statistics.axaml` は `lvc:CartesianChart`（LiveChartsCore 標準コントロール）を直接使用しておりアーキテクチャが根本的に異なる。対象ファイル自体が存在しない
- **View 離脱後もタイマーが動き続けるリーク**（`c7bab7ad`）: upstream は `DispatcherTimer.RunOnce`/`.Run` 静的ファクトリの返り値 `IDisposable` を破棄し忘れる構造だが、Komorebi の `CommitBaseInfo.axaml.cs`/`CommitTimeTextBlock.cs` はこれらのメソッド自体を使用しない別実装のため該当しない
- `6a9e9267`（`--history <dir>` の path seperator 修正）: Komorebi は `--file-history` のみ実装で `--history <dir>` 自体が存在せず対象外
- `b7e4d32b`: 原因コミット（列カスタムリサイザー化）が Komorebi 未適用のため回帰自体が発生しない
- `8c82fa4d`（`git describe` でバージョン名生成）: Komorebi 独自の Velopack バージョニングと非整合
- `8477bdd0`（Avalonia 11.3.18 downgrade）: Komorebi は Avalonia 12 系継続方針のため無関係。**downgrade 理由は TextEditor の性能・メモリ問題** — Komorebi でも動作が重い/メモリ消費過多の報告があれば同種の問題を疑う価値あり（参考情報として記録）
- `8272da4b`: 前提の `sourcegit.node` 機能自体が Komorebi 未導入（`27468682` を導入する場合のみ Accept に切替）
- ローカライズ/翻訳系全般、`ec7c4287`（AvaloniaEdit 参照更新のみ、Komorebi は別途 vendored）

#### ドキュメント差異（今回の調査で判明、別途要修正）

- **CLAUDE.md の Avalonia バージョン記載が古い**: 「Avalonia 12.0.2」と記載されているが `src/Komorebi.csproj` の実体は **12.0.5**
- **CLAUDE.md の `depends/AvaloniaEdit` 記載が不正確**: 「git submodule」と記載されているが `.gitmodules` が存在せず実態は **vendored（直接トラッキング）**

### 2026-04-XX バッチ（template）

| 上流 SHA | サマリ | ステータス | Komorebi SHA | 理由 |
|---|---|---|---|---|
| _例_: `abc1234` | `Fix push --force-with-lease NRE` | applied | `def5678` | 実バグ修正 |
| _例_: `efg9abc` | `Rename Notification.Send → ShowToast` | declined | — | Komorebi では `App.RaiseException` を採用しているため |
| _例_: `1234567` | `Refactor: extract IBranchRepository` | deferred | — | アーキテクチャ判断要、次回 sync 時に再評価 |

### 過去ログ（移行）

過去のバッチログは `~/.claude/plans/goofy-finding-ullman.md` 等の個人 plan ファイルに散在していた。
新規バッチからこのファイルにマージしていく。

## 上流取り込み手順

```bash
# 1. 上流 fetch
git remote add upstream https://github.com/sourcegit-scm/sourcegit.git
git fetch upstream

# 2. 上流の master 時点を mark
git tag upstream/$(date +%Y-%m-%d) upstream/master

# 3. 前回 sync 以降のコミット一覧
git log --oneline upstream/<前回タグ>..upstream/<今回タグ>

# 4. 各コミットを評価して cherry-pick
git cherry-pick -x <SHA>

# 5. このファイルに記録
```

## トラブルシューティング

- **大規模 conflict が発生した場合**: 一時的に main から分岐した `sync/<date>` ブランチで作業する
- **特定機能の上流リファクタが大きい場合**: Komorebi 独自実装を上流に PR する道を検討する（メンテナンスコスト圧縮）
- **CVE / セキュリティ修正の取り込み遅延**: 該当パッチを最優先で cherry-pick し、batch 全体を待たずに main にマージする
