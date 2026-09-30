# UIと画像差分のワークフロー検証

```powershell
dotnet run --project tests/UIWorkflow.E2E/UIWorkflow.E2E.csproj -- 'C:\Program Files\Git\cmd\git.exe'
```

.NET 10、Avalonia Headless 12.1.3、Skia、実Gitで、削除画像の差分、同型View再利用後の検索入力、Fetch/Pull/PushのEnter・Space・UIA、Ctrlクリック、Aboutのコミット表示を検証します。

実際のAXAMLと入力イベントを使い、通常操作のハンドラーへ1回だけ到達することも確認します。リポジトリ、アプリ設定、Git設定は `bin/<構成>/net10.0/artifacts/<実行ID>` に隔離します。UI状態の初期化には内部フィールドをreflectionで設定し、自動フェッチや利用者のワークスペースを読み込みません。

同ディレクトリの `result.json` と `deleted-image-old-side.png`、人工リポジトリを再現可能な成果物として保持します。実デスクトップのウィンドウ・OS支援技術・外部SSH接続は検証範囲に含めません。
