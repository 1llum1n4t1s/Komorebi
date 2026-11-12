# UIと画像差分のワークフロー検証

```powershell
dotnet run --project tests/UIWorkflow.E2E/UIWorkflow.E2E.csproj -- 'C:\Program Files\Git\cmd\git.exe'
```

.NET 11、Avalonia Headless 12.1.4、Skia、実Gitで、削除画像の差分、同型View再利用後の検索入力、Fetch/Pull/PushのEnter・Space・UIA、Ctrlクリック、Aboutのコミット表示を検証します。

実際のAXAMLと入力イベントを使い、通常操作のハンドラーへ1回だけ到達することも確認します。リポジトリ、アプリ設定、Git設定は `bin/<構成>/net11.0/artifacts/<実行ID>` に隔離します。UI状態の初期化には内部フィールドをreflectionで設定し、自動フェッチや利用者のワークスペースを読み込みません。

全ロケールの実リソースについてキー・空文字・埋め込み引数も検証します。検索パネルは英語・日本語・カタルーニャ語へ同じ View のまま切り替え、検索結果・プレースホルダー・ツールチップと一致件数を確認します。

入力条件を変えた際の検証エラーも確認します。Apply は実 View で存在しないパッチを入力してからクリップボードへ切り替え、Git Flow は重複した master/develop の相手側を編集し、エラーが消えて確認可能になることを検証します。Clone の SSH→HTTPS 切替は、SSHKeyPicker による実 `~/.ssh` の読込みを避けるため VM のみで検証します。いずれも適用・初期化・クローン自体は実行しません。

`GIT_TEST_ASSUME_DIFFERENT_OWNER` で実 Git の所有権拒否を発生させ、ルート・子フォルダー・リンクされた worktree の子フォルダーで、確認画面と登録する信頼先が実ルートに一致し、承認後に開けることを検証します。信頼確認後の中断でノードを登録しないことと既存ブックマークの保持も確認します。複数著者の実コミットから統計画面を開き、円グラフの描画・ホバー解除・著者選択の切替も確認します。

同ディレクトリの `result.json` と `deleted-image-old-side.png`、人工リポジトリを再現可能な成果物として保持します。実デスクトップのウィンドウ・OS支援技術・外部SSH接続は検証範囲に含めません。

リモート設定は独立した push URL の保持・解除に加え、生成直後と連続切替時の SSH キー・`no_push` の保持、保存中の再選択を実 VM と Git 設定で検証します。Push はローカル bare リポジトリを使い、`upstream/main` を追跡する `feature` の `pushRemote=origin` が `origin/feature` へ送られ、`origin/main` を変更しないことを確認します。`simple`・`current` と同一リモートへの `upstream` モードを Git 自身の dry-run 結果とも照合します。
