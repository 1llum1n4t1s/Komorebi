# Troubleshooting — Komorebi 障害時の調査・復旧ガイド

Komorebi で問題が起きたときに、まず確認すべき場所と復旧手順をまとめています。Issue を立てる前にこのページの該当セクションを確認してください。

## ログファイルの場所

Komorebi はクラッシュ・エラー・主要操作をローカルログに記録します。Issue 添付時はこのログを参考にしてください。

| OS | ログ場所 |
|----|---------|
| Windows | `%LOCALAPPDATA%\Komorebi\logs\Komorebi_YYYYMMDD.log` |
| macOS | `~/Library/Application Support/Komorebi/logs/Komorebi_YYYYMMDD.log` |
| Linux | `~/.local/share/Komorebi/logs/Komorebi_YYYYMMDD.log` |

> ⚠️ **PII 注意**: ログにはリポジトリの絶対パスが含まれることがあり、Windows では `C:\Users\<ユーザー名>\...` のようにユーザー名が露出します。Issue に添付する前に必要なら手動で `<UserName>` などに伏字化してください。

## シナリオ別 復旧手順

### A. タブを閉じた直後にクラッシュした

1. クラッシュレポート (`Komorebi_YYYYMMDD.log` の末尾) を確認
2. スタックトレースに `Repository.Close` / `_isClosed` 関連が出ている場合、走行中の非同期タスク (Fetch/Pull) が解放済みフィールドにアクセスした可能性
3. 再現手順を Issue に添付 — タブを開いてから閉じるまでの操作 + クラッシュ直前に走らせていた操作

### B. AI commit message が生成されない

1. Preferences ダイアログで AI 設定を確認:
   - **Provider** (OpenAI / Azure / Gemini / Anthropic) が正しいか
   - **API Key** が空欄になっていないか (`Use environment variable` 有効時は環境変数名が設定されているか)
   - **Server** URL が `https://` で始まっているか
2. ログに `AI commit message generation failed` がある場合、HTTP ステータスを確認
3. API key が暗号化できない場合の復旧:
   - `%LOCALAPPDATA%\Komorebi\ai-api-key.key` を削除すると次回起動時に新規鍵生成 → preference.json 内の暗号化済み API key は復号できなくなるので **再入力が必要**
   - 念のため `ai-api-key.key.bak` (1 世代前) があれば復元を試みる

### C. Komorebi が起動しなくなった

1. 一時的に **`%APPDATA%\Komorebi\preference.json` を別フォルダに退避** して起動を試す
   - 起動できれば preference.json の破損が原因
   - 復旧: `preference.json.bak` を `preference.json` にリネーム
2. それでも起動しない場合、自動更新の失敗が原因の可能性。**Velopack ロールバック手順**:
   - **Windows**: `%LOCALAPPDATA%\Komorebi\Update.exe --rollback` を実行 (Velopack CLI、要管理者権限ではない)
   - **macOS**: `/Applications/Komorebi.app/Contents/MacOS/UpdateMac --rollback`
   - **Linux** (deb/rpm): パッケージマネージャから 1 つ前のバージョンを再インストール
3. Velopack のリリース履歴は [GitHub Releases](https://github.com/1llum1n4t1s/Komorebi/releases) で確認できます

### D. auto-fetch が遅い / 効かない

1. Preferences で **`Enable Auto-Fetch`** がオンになっているか確認
2. ネットワーク到達性: `git ls-remote <url>` を手動で実行してリモートに繋がるか確認
3. ログに `AutoFetch tick failed` / `AutoFetch failed: <repo>` などのメッセージがあれば、該当リモートのみエラーが発生している
4. グローバル `AutoFetchInterval` が長すぎないか (Preferences で確認)

### E. 画像 diff (PSD/TGA/DDS/TIFF) でクラッシュ

1. クラッシュ直前のファイルパスとサイズをログから確認
2. 画像のヘッダが破損している可能性 — 別ツール (Photoshop, GIMP, Krita 等) で開けるか確認
3. 巨大画像 (Width × 4 が int.MaxValue を超える) は読み込まずスキップする防御コードが入っていますが、それより小さい範囲で stb_image / Pfim / LibTiff 内部のバグに当たる可能性
4. 該当 PSD/TGA を共有可能なら Issue に添付すると修正の助けになります

## 設定ファイルの場所

| ファイル | 用途 |
|---------|------|
| `preference.json` | アプリ全体の設定 (window 位置、最近のリポジトリ、AI 設定など) |
| `preference.json.bak` | 1 世代前のバックアップ (Save 時に自動退避) |
| `ai-api-key.key` | AI API key の暗号化鍵 (DPAPI on Windows / AES-GCM on Linux/macOS) |
| `<gitdir>/komorebi.settings` | リポジトリ固有設定 (per-remote SSH key、issue tracker 等) |
| `<gitdir>/komorebi.uistates` | リポジトリの UI 状態 (展開状態、フィルタ、選択中コミット) |

## Issue 作成時の推奨情報

1. **OS** とバージョン (Windows 11 23H2、macOS 14.5、Ubuntu 24.04 等)
2. **Komorebi バージョン** (About ダイアログから確認)
3. **再現手順** (1, 2, 3... と具体的に)
4. **ログ抜粋** (PII を伏字化したうえで、エラー発生時刻周辺の 50 行程度)
5. **期待動作** と **実際の動作**
6. (該当する場合) スクリーンショット

## 関連ドキュメント

- [`docs/UPSTREAM-SYNC.md`](UPSTREAM-SYNC.md) — upstream (SourceGit) からの cherry-pick 戦略
- [`docs/TRANSLATION.md`](TRANSLATION.md) — 翻訳追加手順
- [`docs/design-notes/logging-migration.md`](design-notes/logging-migration.md) — ログ・観測性の設計ノート
- [`CLAUDE.md`](../CLAUDE.md) — 開発者向けプロジェクトガイド (Common Pitfalls など)
