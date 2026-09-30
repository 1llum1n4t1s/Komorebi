# クローンの長いパス復旧 E2E

Git for Windows と .NET 10 を使い、GUI が呼ぶ `Commands.Clone.CloneAsync` から実 Git のプロセス、設定変更、ファイル展開まで検証します。

```powershell
dotnet run --project tests/CloneLongPaths.E2E/CloneLongPaths.E2E.csproj -- 'C:\Program Files\Git\cmd\git.exe'
```

検証する失敗と期待動作:

- 親リポジトリの checkout が長いパスで失敗: 設定を有効化し、取得済み HEAD から復旧。
- 入れ子のサブモジュールの checkout が失敗: 同じ HEAD でも再展開し、作業ツリーが clean になる。
- clone の `--recurse-submodules` 自体が失敗: 再 clone せず、サブモジュールも復旧。
- 通常 clone / bare / no-checkout: 長いパス設定を変更しない。
- URL 不正、サブモジュール取得失敗、非空の既存宛先: 設定を変更せず失敗を返し、既存データを保持。
- ファイル名の単一成分が256文字で復旧不能: 一度だけ復旧を試して失敗を返す。
- 開始前または長いパスエラー検出時に中断: 設定変更や復旧を始めない。
- 親リモートの明示SSH鍵・認証helper抑止: 通常更新と長いパス復旧の両方で、サブモジュールには親環境のホスト別設定を渡す。人工キーと実Gitを中継するプロセスで環境を記録し、実SSH接続は行わない。

ユーザーの Git 設定への書き込みを避けるため、`GIT_CONFIG_GLOBAL` と `GIT_CONFIG_NOSYSTEM` を検証プロセス内で指定します。ネットワークは使わず、ローカルの fixture リポジトリをクローンします。

終了時に表示される `bin/<構成>/net10.0/artifacts/<実行ID>/result.json` が再現可能な検証成果物です。同じ場所に各シナリオのコマンドログと fixture を保持します。GUI 上の表示・タブ登録は検証範囲に含めません。
