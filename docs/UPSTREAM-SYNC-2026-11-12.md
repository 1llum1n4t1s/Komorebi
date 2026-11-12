# 上流同期記録（2026-11-12）

対象は `sourcegit-scm/sourcegit` の `c0f40a2e`（前回 v2026.23）から `2735a8ad`（master / v2026.24 の merge）まで。merge を除く51コミットを照合した。作業開始時の Komorebi HEAD は `94a44639`。すべてローカル未コミットで、ブランチ作成・push・リリース・バージョン更新は行っていない。作業開始前からあった workflow・lockfile・csproj・`global.json` などの未コミット変更には触れていない。

## 判定

| 区分 | 件数 |
| --- | ---: |
| 移植（applied、部分適用を含む） | 19 |
| 既存実装で同等（superseded） | 6 |
| Komorebi に該当機能・構造がない（not applicable） | 5 |
| 不採用（declined） | 11 |
| 保留（deferred） | 10 |

`applied` は Komorebi の構造に合わせた手動移植で、SHA 単位の cherry-pick ではない。Komorebi 側のコミットは未確定。

## SHA 別の採否

| 上流 SHA | サマリ | ステータス・内容 |
| --- | --- | --- |
| eb94e47c | zh_TW 翻訳更新 | not applicable — 対象キー `Preferences.Shell.Args.ToolTip` は Komorebi の辞書にない |
| 340aa412 | 翻訳状況ドキュメント更新 | declined — 生成物。Komorebi は自前の `docs/TRANSLATION.md` を CI で生成する |
| 71a0b04f | detached HEAD の新表示 | deferred — 上流は既知プレフィックス以外の ref 行を捨てる（`return null`）ため、rebase 中の `(no branch, rebasing …)` 行が消えて現在ブランチが null になりうる。`Name="HEAD"` への変更はツールバー等の現在ブランチ表示にも影響するため、実 Git の出力と表示箇所を確認してから判断する |
| 7771c8ce | ブランチ名コピーのショートカット | applied — Ctrl/⌘+C。選択がちょうど 1 本のブランチに解決できるときだけコピー。右クリックメニューに合わせ、リモートブランチは `remote/branch`（FriendlyName）をコピーする（上流は Name）。メニューにショートカット表記を追加 |
| 4e49f72d | ブランチ削除時に worktree も削除 | applied — 別 worktree で使用中のローカルブランチに「worktree も削除」を表示。worktree 削除の失敗時はブランチ削除へ進まず、ロック解除後に一覧を更新する。Komorebi 独自の削除後のリモート確認ダイアログは維持 |
| b9bed632 | 翻訳状況ドキュメント更新 | declined — 生成物 |
| 88e054a2 | revision push の null 参照 | superseded — Komorebi は既に `RemoteBranch.Remote` を使っている |
| 4cbe50e3 | API キーを OS 資格情報ストアへ保存 | deferred — Komorebi は 4 プロバイダー共通の暗号化保存（`ProtectedApiKey`・`ProtectedExtraHeaders`、Recovery.E2E で検証）を持つ。OS キーチェーンへの移行は保存方式・移行・復旧の不変条件（DESIGN.md「設定と資格情報」）の変更になるため、設計判断として別途扱う |
| 64c3c64a | 翻訳状況ドキュメント更新 | declined — 生成物 |
| 7d7d0672 | API キー保存のリファクタ | deferred — 4cbe50e3 系 |
| fcc8147f | 資格情報マネージャー使用時の警告 | deferred — 4cbe50e3 系 |
| a396ed08 | 翻訳状況ドキュメント更新 | declined — 生成物 |
| acbb2e9a | 終了時のコマンドログ更新キャンセル | applied — `CommandLog` の UI スレッド Invoke で `TaskCanceledException` を無視。`CommandLogTime` を `DispatcherTimer` に変更 |
| 83f4793d | AI 設定ページの MaxWidth 撤去 | declined — 上流の API キー編集 UI 追加に伴う外観変更。Komorebi の AI 設定画面は別構成 |
| a5cb5b60 | Linux で secret-tool に保存 | deferred — 4cbe50e3 系 |
| 85ec979b | Linux 資格情報保存の修正 | deferred — 4cbe50e3 系 |
| 2933ffd2 | SystemKeychain への改名 | deferred — 4cbe50e3 系 |
| 63c13c2d | ApiKey.CreateEditObject の例外 | deferred — 4cbe50e3 系 |
| 03eb4115 | es 翻訳更新 | applied（部分）— `DeleteBranch.DeleteAttachedWorktree` の訳を採用。API キー編集・Shell 引数ツールチップのキーは Komorebi にない |
| 7ae01936 | 翻訳状況ドキュメント更新 | declined — 生成物 |
| d69b783b | macOS キーチェーン実装の書き換え | deferred — 4cbe50e3 系 |
| b72704cc | API キーのキャッシュ | deferred — 4cbe50e3 系 |
| 6e8d11d2 | モデル一覧を初回利用時に取得 | not applicable — Komorebi にはモデル一覧の自動取得がない（モデル名は設定値を使う） |
| 18387637 | AI 周りのコード簡素化 | not applicable — 6e8d11d2 と上流 API キー実装が前提 |
| 8189114b | ブランチを完全な ref 名で push | applied — Komorebi の `Commands.Push` は単一コンストラクターで SHA も受けるため、`localIsRevision` を追加。ローカル側はブランチのときだけ `refs/heads/`、リモート側は常に `refs/heads/` を付ける。`PushRevision` は SHA のまま。引数境界の単体テストを追加 |
| 8f8aaea1 | push 先の追跡ブランチ検索 | not applicable — Komorebi の Push VM は `branch.<name>.merge` を読まず、pushRemote 設計で upstream・同名ブランチから選ぶ |
| 7fc276f5 | 左サイドバーのレイアウト更新漏れ | not applicable — Komorebi に `IsNormalSidebarVisible` の切替がなく、サイドバーは常時表示 |
| 838e9004 | 未ステージフォルダーの「フルパスをコピー」見出し | superseded — 既に `CopyFullPath` |
| ef017624 | パッチをコミットとして適用（git am） | applied — `Commands.Am`、`AmInProgress`、進行中バーの表示と Skip、競合解決の Mine/Theirs。メール形式の判定は副作用のある validator ではなく `PatchFile` の setter で行う。`final-commit` からメッセージを読み込む。git am は `MERGE_MSG` を使わず完了後も消さないため、am 中にタブを閉じても `MERGE_MSG` へは保存しない（保存すると次の無関係なコミットに古い本文が入る） |
| f2ff0c20 | 翻訳状況ドキュメント更新 | declined — 生成物 |
| 26e0e9a9 | en_US 文言調整 | applied — `3-Way merge` |
| 61e9fcc1 | 進行中状態の判定強化 | applied（部分）— `rebase-apply/applying` を Am、`rebase-merge` または `rebase-apply/rebasing` を Rebase と判定。上流はマーカーのない `rebase-apply` を削除するが、利用者の状態を消さないため削除はしない。あわせて、apply backend の rebase で `rebase-merge/head-name` を読んで例外になっていた既存の不具合を、実在する状態ディレクトリから読むよう修正。apply backend の停止位置は `stopped-sha` がないため `rebase-apply/original-commit` を使う（`orig-head` はリベース前の先端で停止位置ではない） |
| e112b3d9 | パッチファイル欄へのドラッグ＆ドロップ | applied — `.patch`/`.diff` の単一ローカルファイルだけを受け付ける。拡張子は大文字小文字を区別しない |
| 85916642 | ubuntu-latest での CI ビルド失敗 | applied — rpm spec の `chmod -R` をやめ、`package.linux.sh` でパッケージ化前に配布物の権限を揃える |
| 26fef126 | 「現在のブランチに集中」モード | applied — UI 状態に保存。トグルは上流のツールバー位置ではなく、Komorebi の統一ブランチ操作バーで HEAD 移動ボタンの隣に置く。有効化で履歴フィルターを解除し、手動でフィルターを付けると自動で解除 |
| 7b1086d0 | 翻訳状況ドキュメント更新 | declined — 生成物 |
| 110251ed | Welcome のローカル変更数の誤り | superseded — Komorebi は NUL 区切りの status レコードを種類別に数えている |
| 26bc82f4 | ツールバーのアイコン位置 | declined — 上流ツールバー固有の外観調整 |
| 1b080758 | 集中モードで upstream のコミットも表示 | applied — `QueryUpstreamOfCurrentBranch` を追加し、`HEAD` と upstream を辿る。既存の `BuildHistoryParams` 名は維持し、`BuildHistoryParamsForCurrentBranch` を追加。upstream 名は `Quoted()` で渡す |
| 3e608dd5 | 集中モードでは「失われたコミット」を無効化 | applied — 集中モードでは `--reflog` を付けず、メニュー項目を無効化 |
| 6b677688 | StbImageSharp 2.30.16 | superseded — 既に 2.30.16 |
| e32e1865 | ru 翻訳更新 | applied（部分）— 本同期で追加したキーの訳を採用。API キー編集・Shell 引数ツールチップのキーは Komorebi にない |
| b183d528 | 翻訳状況ドキュメント更新 | declined — 生成物 |
| e66cccaf | NumericSort の文字列確保削減 | applied — span 比較へ変更。Komorebi 独自の `char.IsAsciiDigit` と null 処理は維持 |
| 56dd611b | クリップボード COMException でのクラッシュ | applied — `App.CopyTextAsync` で例外を捕捉し、ログに記録したうえで操作中のウィンドウを親にした Alert で通知（独立ウィンドウでも見えるように）。メッセージはローカライズ |
| 4f18ef16 | cherry-pick/revert `--no-commit` 後のメッセージ | applied — 進行中状態がなく入力欄が空なら `MERGE_MSG` を読み込む。進行中のメッセージは `MERGE_MSG` に保存されるため、UI 状態の `LastCommitMessage` には残さない |
| 37bd1fbd | 追加・削除ファイルは片側だけ表示 | applied — Komorebi はファイルモードを文字列で持つため、`TextDiff.IsNewFile`/`IsDeletedFile` を diff ヘッダーから記録。並列表示に切り替えず、存在する側の行番号だけを表示（View 再利用時も毎回設定） |
| b3c24417 | staged のリネーム画像の差分パス | superseded — 既に新側は `_option.Path` を使う |
| b3604295 | `ReadToEndAsync` の安全化 | superseded — Komorebi は stdout/stderr を既に並行に読み、取消経路も独自に持つ |
| 76fe1486 | static ラムダ | applied |
| 814681d6 | v2026.24 リリース | declined — バージョンは Komorebi 独自に管理 |

## 追加したローカライズ

`Apply.CommitChanges`、`Apply.CommitChanges.Tip`、`Clipboard.CopyFailed`、`DeleteBranch.DeleteAttachedWorktree`、`InProgress.Am`、`Repository.FocusOnCurrentBranch` の6キーを全18辞書に翻訳済みで追加した。zh_CN・es_ES・ru_RU は上流の訳を文意に合わせて採用し、上流の「Dashboard ページ」は Komorebi の画面名に合わせて「履歴」とした。その他の言語の訳は今回作成したもので、母語話者によるレビューは行っていない。

## 検証

| 検証 | 結果 |
| --- | --- |
| Release ビルド（`-p:RestoreLockedMode=true`） | 警告0・エラー0 |
| Komorebi.Tests | 1490成功・失敗0・skip2（Unix 専用のプロセスグループ検証） |
| UIWorkflow.E2E | 17シナリオ成功（Fetch/Pull/Push の入力、push 先、画像差分、View 再利用、全言語の辞書と引数を含む）。成果物: `tests/UIWorkflow.E2E/bin/Debug/net11.0/artifacts/1de39cb71c2d4199b98dff46dd22b85c/` |
| localization-check.js | 18言語すべてキー充足 |
| dotnet format / git diff --check | 成功 |
| 実 Git の状態ファイル | `git am` の競合で `rebase-apply/applying`・`next`・`last`・`final-commit`、`git rebase --apply` の競合で `rebase-apply/rebasing`・`head-name`・`onto`・`orig-head`・`original-commit`（`stopped-sha`・`rebase-merge` なし。`original-commit` は停止中のコミット、`orig-head` は元の先端）、`cherry-pick --no-commit` で `MERGE_MSG` のみ（`CHERRY_PICK_HEAD` なし）、am 中に作られた `MERGE_MSG` は `--abort` で消え `--continue` 完了後は残ることを確認 |

追加テスト: push の refspec（ブランチ・revision）、`git am` の引数境界、追加・削除ファイルの差分フラグ、メール形式パッチの判定、集中モードの git log 引数。

未検証: 実デスクトップでの新しい UI（集中モードのトグル、Apply ダイアログのチェックボックスとドラッグ＆ドロップ、ブランチ削除ダイアログ、`git am` の進行中バー）、macOS/Linux の実画面、Linux パッケージの CI 実行。
