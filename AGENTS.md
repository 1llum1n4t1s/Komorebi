# AGENTS.md

This file provides guidance to Codex and other coding agents working in this repository.

## Project Overview

**Komorebi** is a fork of [SourceGit](https://github.com/sourcegit-scm/sourcegit), an open-source, cross-platform Git GUI client built with **C#/.NET 10** and **Avalonia UI 12.1.4**. It wraps the git CLI to provide a visual interface for git operations. The fork's GitHub repository is `https://github.com/1llum1n4t1s/Komorebi`.

設計の概要・責務とデータフローは [DESIGN.md](DESIGN.md) を参照する。本ファイルは作業規約・実装時の制約・検証手順を扱う。

## Build & Run

```bash
# Restore (depends/AvaloniaEdit is vendored, no submodule init needed)
dotnet restore

# Build
dotnet build

# Run
dotnet run --project src/Komorebi.csproj

# Format check (CI enforced)
dotnet format --verify-no-changes src/Komorebi.csproj

# Release publish (platform-specific, AOT by default)
dotnet publish src/Komorebi.csproj -c Release -o publish -r win-x64

# Build without AOT (faster for local testing)
dotnet publish src/Komorebi.csproj -c Release -o publish -r win-x64 -p:DisableAOT=true

# Build without update detection
dotnet build -p:DisableUpdateDetection=true
```

`Directory.Build.props` は厳格な build 時検査を強制する:
- `EnforceCodeStyleInBuild=true` — IDE 診断を build error にする。未使用 `using` は抑制せず修正する。
- `GenerateDocumentationFile=true` — required for `IDE0005` to detect unused usings; produces an XML doc next to the assembly.
- `NoWarn=CS1591` — silences "missing XML doc comment" warnings so IDE-style analysis stays the only signal.
- `<Version>` here is the source of truth for both packaging and Velopack.
- `RestorePackagesWithLockFile=true` — app/test projects keep separate `packages.lock.json` files. After changing a `PackageReference`, run `dotnet restore --force-evaluate`, review all affected lockfiles, then verify with `dotnet restore --locked-mode`. E2E projects outside the solution require the same restore checks with their explicit project paths.
- `Directory.Build.targets` separates restore graphs: `packages.lock.json` is the non-RID Debug baseline; Release uses `packages.Release.lock.json`, and the app with `DisableAOT=true` uses `packages.Release.NoAOT.lock.json`. An explicit RID adds a suffix (for example `packages.Release.win-x64.lock.json`). Regenerate and locked-restore each affected configuration with `-p:Configuration=Release` / `-p:DisableAOT=true` / `-r <rid>` as appropriate. SDK changes can update the implicit AOT tool packages. Publishing must not overwrite or regenerate the Debug baseline.
- Locked restore also requires the selected lockfile to exist before NuGet runs. Keep the Release locks for all five publish RIDs (`win-x64`, `win-arm64`, `osx-arm64`, `linux-x64`, `linux-arm64`) tracked, and publish with `-p:RestoreLockedMode=true`. Generate cross-compilation locks with `NETCoreSdkRuntimeIdentifier` matching the build host (`win-x64` for Windows, `linux-x64` for Linux, `osx-arm64` for macOS); missing files must fail rather than silently generate an unlocked baseline.

## Tests

```bash
# Run application tests
dotnet test --project tests/Komorebi.Tests/Komorebi.Tests.csproj

# Localization validation (CI enforced)
bun build/scripts/localization-check.js
```

ローカライズ用依存は `cd build/scripts` で `bun install --frozen-lockfile --ignore-scripts` を実行する。`packageManager` で指定した Bun を使い、Node.js・pnpm は不要。Bun の真の AOT は安定版対応待ちのため、現状は Bun ランタイムで実行する。

Bun 依存の脆弱性は `.github/workflows/dependency-security.yml` が毎日監査する。隔離領域で安全な修正だけを準備し、`build/scripts/package.json` と `build/scripts/bun.lock` に限定した PR を作成する。候補なしの日は prepare だけを実行し、候補がある場合だけ固定 base/head/merge の重い検証へ進む。NuGet 等は既存の Dependabot を維持する。

Bun の通常更新は Dependabot で行い、minor/patch は集約、major は個別 PR にする。日程は [.github/dependabot.yml](.github/dependabot.yml) を参照する。承認待ちの CI を重複扱いするのは、既知の検証ワークフローについて、信頼済み保守 run の同じ attempt で固定 merge の全検証ジョブが成功した証拠がある場合だけ。未知のチェック・失敗・未完了は保留する。

自動マージは既定OFF。repository variable `BUN_SECURITY_NATIVE_MERGE_ACTIVATED` を明示的に `true` にするまで、日次処理は PR 作成・読取専用検証・設定待ち通知に留め、App status や merge API を書き込まない。実 GitHub App の source、ruleset bypass、draft競合を実機で確認するまで有効化しない。

有効時も、main だけに適用される active・strict=true の required status `kagayoi/automation-merge-guard-v1` と、その source App・ruleset ID・更新日時が固定値と一致しなければ保留する。設定値は `DEPENDENCY_GUARD_APP_CLIENT_ID`、`DEPENDENCY_GUARD_APP_ID`、`DEPENDENCY_GUARD_APP_SLUG`、`DEPENDENCY_GUARD_APP_BOT_ID`、`DEPENDENCY_GUARD_RULESET_ID`、`DEPENDENCY_GUARD_RULESET_UPDATED_AT`、秘密は `DEPENDENCY_GUARD_PRIVATE_KEY`。専用 App の token は対象 repo の statuses:write に限定し、候補コードへ渡さない。署名前に同 token で App の ID・client ID・slug と bot の ID・login を読み、owner の固定値と照合する。GITHUB_TOKEN は merge を担当し、App・GITHUB_TOKEN に bypass がないことを導入前に owner が確認する。

候補の識別、固定 base/head/merge の検証、CAS 更新、署名付き status と merge intent の記録・照合は [dependency-security.yml](.github/workflows/dependency-security.yml) と [Native-Merge.mjs](build/scripts/security/Native-Merge.mjs)、[Security-Pull-Request.mjs](build/scripts/security/Security-Pull-Request.mjs) を変更前に読む。専用 App の status と ledger の証拠を再読し、同じ head に対する他 actor の native 予約は防げない前提を維持する。タイムアウト・不明応答・不一致は再送せず保留し、確定拒否後も同一 snapshot・CI attempt を再送しない。merge 応答後も PR・commit の親と tree・main 祖先を確認する。
固定 issue の bot comment ledger に guard status proof・merge intent・proposal を記録する。管理者は導入時に対象 repository の数値 ID と issue 番号を確定し、本人または Actions bot が作成した専用 open issue の本文を以下の2段落と完全一致させ、repository variable `BUN_SECURITY_LEDGER_ISSUE` にその正整数の issue 番号を設定する。`<repo_id>` と `<issue_number>` は実数値に置き換える。作成や設定は導入承認後に別途行い、未設定・不一致・閉鎖時に自動再初期化しない。

```text
Bunの脆弱性自動修正の処理状態を保持するIssueです。自動処理のため開いたままにしてください。

<!-- bun-security-ledger-v1:{"version":1,"repo":<repo_id>,"issue":<issue_number>,"state":"idle"} -->
```

ledger は作成・更新前の intent、proposal、status proof と merge intent を保持し、未設定・不一致・孤立証拠・不明応答は保留する。本人の draft・auto-merge 解除・予約・未解決要求を尊重し、ledger issue は閉じない。詳細な状態遷移と復旧は [Security-Record.mjs](build/scripts/security/Security-Record.mjs)、[Security-Pull-Request.mjs](build/scripts/security/Security-Pull-Request.mjs)、[Maintain-Security.mjs](build/scripts/security/Maintain-Security.mjs) を読む。

通常通知は ledger と分け、main と未 merge 候補の監査を区別する。main の未対策がある間や merge 後の新 main の実監査前は解決済みにしない。最新 main の監査、残件・候補と ledger の状態が解決条件を満たす場合だけ通知を閉じ、issue と main を再取得する。承認済み mitigation は現在空。Status API と issue comment に CAS はないため、前後の再読と workflow concurrency を維持し、不明時は保留する。他人の issue は自動 reopen しない。
ローカライズ CLI は全 XML の読込・解析・生成が成功するまで書き込まない。書込中の障害まで原子的に復旧するものではない。

`localization-check.yml` は `contents: read` で PR・再利用呼出しを検証する。main への push 時の翻訳整形・文書の自動コミットは、`contents: write` を明示した `localization-update.yml` が担当する。



`global.json` は Microsoft.Testing.Platform を選択しているため、テスト実行では上記の `--project` 形式を使う。CI と同じ検証は `dotnet build -c Release -p:RestoreLockedMode=true` の後に `dotnet test --project tests/Komorebi.Tests/Komorebi.Tests.csproj -c Release --no-build` を実行する。

### 実行経路の E2E 検証

次のプロジェクトは `Komorebi.slnx` と通常 CI の対象外なので、対応する実装を変更した場合は個別に実行する。Git を引数に取る検証では、インストール済みの実行ファイルの絶対パスを指定する。

| 変更対象 | 必須コマンド（Windows の例） | 検証範囲・詳細 |
|---|---|---|
| クローン、長いパス復旧、サブモジュール認証 | `dotnet run --project tests/CloneLongPaths.E2E/CloneLongPaths.E2E.csproj -- 'C:\Program Files\Git\cmd\git.exe'` | [実 Git のクローンと復旧](tests/CloneLongPaths.E2E/README.md) |
| API キー保存、IPC のロック・転送・復旧 | `dotnet run --project tests/Recovery.E2E/Recovery.E2E.csproj -c Release -p:DisableAOT=true` | [実暗号化と別プロセス](tests/Recovery.E2E/README.md) |
| 画像差分、View 再利用、Fetch/Pull/Push の入力、About | `dotnet run --project tests/UIWorkflow.E2E/UIWorkflow.E2E.csproj -- 'C:\Program Files\Git\cmd\git.exe'` | [実 AXAML・Headless 入力と画像](tests/UIWorkflow.E2E/README.md) |
| ローカライズ検証 CLI の失敗時終了 | `bun tests/Localization.E2E/Run.mjs` | 読込失敗時の exit 1 と書込抑止。本番 CI と同じ Bun CLI を子プロセスで検証する。`--bun <実行ファイル>`、`--artifacts <絶対パス>` で検証対象・成果物の親ディレクトリを指定可 |

ローカライズ E2E ハーネス変更時は `bun test tests/Localization.E2E/Run.test.mjs` も実行し、書込み例外を握りつぶす CLI の書込み試行が検出されることを確認する。テストの成果物の親ディレクトリは `KOMOREBI_LOCALIZATION_ARTIFACTS` で指定できる。

各実行の `result.json`、ログ、fixture・画像を成果物として保持し、結果と再現手順を報告する。設定と資格情報は検証用に隔離される。GUI の起動・OS 支援技術・外部 SSH 接続まで検証したとは扱わず、必要な実デスクトップ確認は別途行う。成果物の具体的な保存先は各 README と実行出力を参照する（ローカライズ検証は `tests/CloneLongPaths.E2E/bin/localization-artifacts/<実行ID>/`）。

### 同梱エディタの検証

同梱エディタを変更した場合は、ルートの `Komorebi.slnx` に含まれない demo と NUnit テストも別途検証する（いずれも net10.0）。

```bash
dotnet build depends/AvaloniaEdit/AvaloniaEdit.sln -c Release
dotnet test --project depends/AvaloniaEdit/test/AvaloniaEdit.Tests/AvaloniaEdit.Tests.csproj -c Release --no-build
```

`depends/AvaloniaEdit` の更新では日本語 IME、描画余白、矩形選択の文字列 DataFormat、Avalonia 12 対応を維持する。統合元は [同梱エディタの README](depends/AvaloniaEdit/README.md)、アプリとの責務境界と寿命は [DESIGN.md](DESIGN.md) を参照する。

## Solution Structure

`Komorebi.slnx` (XML-based solution file):
- `src/Komorebi.csproj` — main application
- `tests/Komorebi.Tests/` — xUnit v3 test project
- `depends/AvaloniaEdit/` — vendored (directly tracked, not a git submodule; text editor for diff/blame views)
- `.github/workflows/` — CI/CD workflows
- `build/` — packaging scripts and resources

## 実装時の規約

構造・責務・データフローは [DESIGN.md](DESIGN.md) を正本とする。以下は実装変更時に守る手順と制約である。

### Git Command Layer

Git CLI の共通処理は [Command.cs](src/Commands/Command.cs) を変更時に読む。コマンドは操作ごとに生成し、実行状態を持つ同じインスタンスを並行実行しない。`ErrorMessage` は直近の `ExecAsync()` の秘密値除去済みエラーで、再実行ごとにリセットされる。SSH 実行・git 相対パス・`--name-status` には既存の `ExecWithSSHKeyAsync`・`ResolveGitRelativePath`・`ParseNameStatusLine` を使う。
### SSH Key Management
SSH 鍵の選択・走査を変更するときは [SSHKeyInfo.cs](src/Models/SSHKeyInfo.cs) と [SSHKeyPicker.axaml](src/Views/SSHKeyPicker.axaml) を読む。None（global）・Key・CustomKey・Browse を維持する。

**3-tier fallback strategy** (in `Command.ResolveSSHKeyValue` — pure function, unit testable):
1. Per-remote setting (`git config remote.<name>.sshkey`)
2. Global SSH key (`Preferences.Instance.GlobalSSHKey`)
3. ssh-agent / `~/.ssh/config` (when both are empty)

**Legacy `__NONE__` sentinel**: 旧バージョンで書き込まれた `__NONE__` は「グローバルフォールバックを明示的にスキップ」として読み取り時に尊重する。新 UI からは書き込まず、読み取り専用の凍結レガシーとして `LegacySSHKeyOptOutSentinel` 定数で管理する。

`GIT_SSH_COMMAND` は `Command.CreateGitStartInfo` の OS 別 quoting を維持する。有効な明示鍵を使う場合、Unix は POSIX single quote と `-F /dev/null`、Windows は double quote と `-F NUL` を使う。鍵が未指定または無効なら親の `GIT_SSH_COMMAND` を尊重し、なければ SSH の既定設定を使う。git CLI path 用 `.Quoted()` を SSH に流用しない。

### AWS CodeCommit Support

CodeCommit の HTTPS・SSH・GRC URL を維持する。変更時は [Remote.cs](src/Models/Remote.cs) の各解析関数と AWS Console URL への変換を確認する。CodeCommit では `RemoteProtocolSwitcher` を表示しない。
### Remote Configuration
`RepositoryConfigure` (VM + View) provides a unified dialog for managing remotes, including fetch/push URL editing, per-remote SSH key selection, and per-remote push prohibition. Push prohibition uses `git remote set-url --push <name> no_push` to set an invalid push URL. `SelectedRemotePushDisabled` detects only `no_push`; a custom push URL different from the fetch URL is valid and must survive unrelated edits.

### リポジトリの信頼確認

所有権エラーからの起動・追加・オープンでは `Models.SafeDirectories` と `TrustRepository` を使う。stderr の提案だけで別パスやワイルドカードを信頼せず、ファイルシステム上のリポジトリ境界を確認する。変更時は `SafeDirectoriesTests` を検証し、中断・登録失敗後にリポジトリを開かない契約を維持する。設計は [DESIGN.md](DESIGN.md#リポジトリの信頼境界) を参照する。

### View の再利用と入力

- `ContentControl + DataTemplate` の切替と Repository の VM 保持を維持する（[設計](DESIGN.md#データフローと寿命)）。View の `DataContext` 変更時は旧 VM のイベント購読を解除し、新 VM に付け直す。VM に属する状態を code-behind に保持しない。
- Fetch/Pull/Push は `ModifierButton` の `Click` 経由で処理する。ポインターの `Tapped` だけに依存せず、Enter・Space・UIA でも同じ操作へ一度だけ到達させる。修飾キーは `ClickModifiers` から取得する。

### 起動・IPC の変更

診断の出力先・ステージ・単一起動の責務は [DESIGN.md](DESIGN.md#起動診断と単一起動) を参照する。

- 起動シーケンスに重い処理を挟むときは対応する `StartupDiagnostics.MarkStage()` を追加する。`TryLaunchAsNormal` の `FontWarmup.Run()` を維持する。
- 正常終了では観察期間中でも `MarkCompleted()` を呼ぶ。`Environment.Exit` は `finally` を実行しないため、正常なリベースエディタ終了・IPC 転送成功の終了では `MarkCompleted()` と `Logger.Dispose()` を明示する。
- ロック未取得を正常な二重起動と断定しない。転送成功だけを exit 0 とし、転送不能・IPC 初期化失敗の診断と exit 1 を維持する。

### Auto-Update (Velopack)
- Entry point: `Main()` (`App.axaml.cs`) では起動診断の開始・ステージ記録に続けて `VelopackApp.Build().Run()` を呼び、DataDir・Logger・Avalonia の初期化より前に更新フックを処理する。
- `App.Check4Update()` uses `UpdateManager` + `SimpleWebSource` pointed at `Preferences.UpdateBaseUrl` (= `https://komorebi.kagayoi.com`, Cloudflare R2 カスタムドメイン) as the **primary** update feed
- 配信元 URL は `Preferences.CanonicalUpdateBaseUrl` 定数で 1 箇所管理する。`UpdateBaseUrl` プロパティは `[JsonIgnore]` 付きの薄いラッパーで、外部 JSON からの上書きを不可にする
- 通常リリースは **R2 単独配信**（GitHub Releases は作らない）。**win-x64 / win-arm64 はローカル署名リリース (`scripts/release-local.ps1`)、osx-arm64 / linux-x64 / linux-arm64 + standalone パッケージは CI (`.github/workflows/release.yml` の `r2-upload` ジョブ)** の役割分担（詳細は後述「CI/CD」）
- 旧 `GithubSource` クライアント救済は GitHub Releases に「踏み台 (R2 対応版を含む最初のバージョン)」を **1 つだけ** publish する方式。2 段階更新（旧 → 踏み台版 → R2 最新）で乗り換えさせる。踏み台 publish は `/transfer-cf` 移行作業時に 1 回だけ実施し、踏み台 Release は **削除せず残す**（継続併用はしない）
- `App.Check4Update()` constructs the `UpdateManager` and delegates update checks, progress UI, download, and apply/restart to `VelopackUpdateDialog.UpdateDialogWindow.ShowAsync()`
- `Models.UpdateDialogStrings` supplies localized dialog text; ignored versions remain in `Preferences.IgnoreUpdateTag`
- Compile flag `DISABLE_UPDATE_DETECTION` skips update checks entirely

### Localization

`src/Resources/Locales/en_US.axaml` を基準に全言語のキーを揃え、`build/scripts/localization-check.js` で検証する。キーは `Text.Category.Name` 形式。言語追加時は `Models/Locales.cs` の `Locale.Supported` と `App.axaml` の `ResourceInclude` を更新する。`App.SetLocale()` で辞書を切り替え、初回は `Preferences.DetectedLocale` を初期値として `InitSetup` で利用者が確認・変更する。
### Theme System

色追加時は [Themes.axaml](src/Resources/Themes.axaml) に `Color.*` と `Brush.*` を定義し、AXAML から `{DynamicResource Brush.MyName}` で参照する。色を直書きせず、`Models/ThemeOverrides.cs` による利用者の上書きを維持する。
### Alert Dialog for Modal-Context Errors

既に開いているモーダル内のエラーは親画面のバナーに隠れるため `App.RaiseException` でなく `await new Alert().ShowAsync(this, message, isError: true)` を使う。実装・タイトルのローカライズ・長文表示は [Alert.axaml](src/Views/Alert.axaml) と code-behind を参照する。
### Window State Persistence Pattern

`FileHistories`・`Blame` は `ViewModels.LayoutInfo` に幅・高さ・位置・状態を保存する。constructor では寸法だけ復元し、`App.ShowWindow` の中央配置を初回・画面切断時の fallback にする。`OnOpened` で `TryRestoreWindowPosition` が接続中画面内と判定した位置と最大化状態を復元してから `PositionChanged` を購読する。サイズ・位置は Normal 時のみ、状態は Minimized 以外を保存する。中央表示から復元位置への一瞬の移動は許容する。

`Launcher` は例外で constructor で寸法と有効な位置を復元（無効なら CenterScreen）し、位置変更を購読する。`OnOpened` は最大化・フルスクリーン状態だけを復元する。
### AI 資格情報の保存

保存方式と復旧時の不変条件は [DESIGN.md](DESIGN.md#設定と資格情報) を参照する。`Service.ProtectedApiKey`・`ProtectedExtraHeaders` と source-generated JSON 経由の実際の保存・復号を `Recovery.E2E` で検証する。既存鍵の読込失敗を鍵の再生成で置き換えず、暗号化・移行失敗時に平文を保存しない。

### Adding a New Popup Dialog
1. Create `src/ViewModels/MyDialog.cs` inheriting `Popup`, override `Sure()` for confirm logic
2. Create `src/Views/MyDialog.axaml` + `.axaml.cs` with `x:DataType="vm:MyDialog"`
3. View is auto-resolved by naming convention (`ViewModels.MyDialog` → `Views.MyDialog`) via `PopupDataTemplates.cs`
4. Show via `_launcher.ActivePage.Popup = new ViewModels.MyDialog();`

### Adding a New Git Command
1. Create `src/Commands/MyCommand.cs` inheriting `Command`
2. Set `WorkingDirectory`, `Context`, and `Args` in the constructor
3. For short commands: call `ReadToEnd()` or `ReadToEndAsync()` and parse stdout
4. For long-running commands (fetch/push/pull): call `ExecAsync()` which streams output
5. For SSH-authenticated remotes: use `ExecWithSSHKeyAsync(remote)` instead of direct `ExecAsync()`
6. For `--name-status` output: use `ParseNameStatusLine(line)` instead of writing custom regex

中断可能な Popup の処理では `BeginCancellableOperation()` を `using` で保持し、その `Token` をコマンドの `CancellationToken` へ渡す。中断後は成功時の後続処理へ進めず、プロセス終了は共通の `Native.CommandCancellation` 経路に委ねる。変更時は `CommandCancellationTests` と `ReadToEndCancellationTests` を検証する。

## Common Pitfalls

プロジェクト固有の落とし穴の詳細は [docs/PITFALLS.md](docs/PITFALLS.md) を参照。

## Code Style

書式は [.editorconfig](.editorconfig) と CI の `dotnet format --verify-no-changes` を正本とする。C# は Allman 形式、`var` と collection expressions を優先し、private フィールドは `_camelCase`、private static フィールドは `s_camelCase`、定数は `PascalCase` とする。
## CI/CD

CI の構成は [.github/workflows](.github/workflows) を参照する。`release/**` push で macOS / Linux と standalone を build・package・配信し、GitHub Releases は作らず R2 単独配信する。Windows の未署名フィードがローカル署名成果物を上書きしないよう `velopack.yml` の matrix に win-x64 / win-arm64 を含めない。
### Windows リリース (ローカル実行)

- `/vava` で `Directory.Build.props` のバージョンを確定した後、`pwsh scripts/release-local.ps1` で win-x64 / win-arm64 の build・署名・R2 upload・cleanup を行う。アップロードしない検証は `-SkipUpload` を使う。前提条件と対象を絞る `-Runtimes` はスクリプト冒頭を正本とする。`vpk pack --runtime` の明示指定を維持し、生成する CPU と更新チャンネルを一致させる。アップロード後は固定 URL の取得内容を比較し、不一致の場合だけキャッシュをパージする。全配信ファイルの SHA-256 一致まで確認してから旧版清掃へ進む。HTTP 200 だけを配信完了条件にしない。
- ローカルスクリプトのアップロード前検証（manifest・nupkg・portable の版・CPU・署名・hash）を維持する。`-SkipUpload` でもこの検証を通す。
- 製品ページの配信は兄弟リポジトリの `../vps-web/deploy/deploy-lp.ps1` を使う。公開ホスト・更新ファイルの既存経路を維持する。

### macOS / Linux + standalone パッケージ (CI)

`release/**` ブランチへの push で `release.yml` が `build.yml` → `package.yml` → `velopack.yml` → `r2-upload` を順に呼ぶ。アップロード前に `python3 build/scripts/verify-release-artifacts.py --packages packages --version <version> --source-sha <40桁SHA>` で最終配布物を検証する（Linux 上で `rpm`・`rpm2cpio`・`squashfs-tools`・`libarchive-tools` が必要）。版・ソース SHA・CPU・manifest の hash/size と各パッケージの実体が一致しない場合は配信しない。

旧版清掃は所有する配信経路に限定する。CI は Windows の全世代の nupkg・standalone zip・固定名 installer を保護し、Windows はローカル署名リリースだけが清掃する。ローカル側は未ビルドのチャンネルの manifest 参照も保持し、取得・形式検証に失敗したら清掃を中止する。並行配信中の相手の新パッケージを削除しない理由は [DESIGN.md](DESIGN.md#不変条件と採用済み設計判断) を参照する。

> ℹ️ `package.yml` の win standalone zip (`komorebi_*.zip`) は引き続き CI で生成される未署名バイナリ。署名対象に含めたい場合はローカルスクリプトへの移植が必要。

Linux builds run directly on `ubuntu-latest` runner (no Docker container). arm64 cross-compilation adds ports.ubuntu.com sources with dynamic codename detection. RPM packaging skips `brp-strip` for cross-arch binaries (`--define "__strip /bin/true"`).


## Key Dependencies

依存の正本は [src/Komorebi.csproj](src/Komorebi.csproj) と [Directory.Build.props](Directory.Build.props)。Avalonia 本体と DataGrid は指定バージョンが異なるため、更新時は各パッケージの提供版と互換性を確認する。Velopack の更新ではフィード互換性を検証する。LiveChartsCore は Avalonia 12 対応版を選び、更新時に `LibraryCompatibilityTests` でチャート生成を検証する。CJK フォントは同梱せず `InstalledFont.GetLocaleDefaults()` のシステムフォールバックを使い、Inter は NuGet から提供する。
## Upstream-Faithful Policy

`sourcegit-scm/sourcegit` の定期 cherry-pick を維持する。実バグ・回帰は upstream との差異をコメントして修正し、単なるローカルのスタイル変更は upstream へ提案する。Komorebi 固有の設計（通知、ツールバー、SSH key picker、CodeCommit、AI provider、logger、名前空間・コメント・collection expression）は保持する。同期時は適用・却下・保留と理由を SHA 単位で記録し、繰り返す bot 指摘はまとめて回答する。同期手順は [docs/UPSTREAM-SYNC.md](docs/UPSTREAM-SYNC.md) を参照する。
## ドメイン移行（2026-07 開始・期限 2027/05/31）

屋号を **Kagayoi** に統一したため、配信ドメインを `nephilim.jp` から `kagayoi.com` へ移行中。方針の全体像はユーザーグローバルの `AGENTS.md` §屋号とドメイン を参照する。

- **旧ドメイン `nephilim.jp` はレジストラで廃止申請済みで 2027/05/31 に失効する**（延長しない）。それまでに出荷済みバイナリを新ドメインへ移行しきる。
- 旧ホストの Worker route / custom domain は**期限まで消さない**。消すと出荷済みアプリの自動更新が止まる。
- `nephilim.jp` の Redirect Rules は `/` だけを 301 する。`releases.*.json` / `*.nupkg` / `*-Setup.exe` は転送せず R2 が配信を続ける。
- 配信は `komorebi.kagayoi.com`（R2 `komorebi-updates`）。旧 `komorebi.nephilim.jp` は route に併記して残してある。

### ローカルリリースの実行

R2 配布は PowerShell 7.2 以降と同梱の `scripts/R2Distribution.Common.ps1` を使う。配布に Node.js・pnpm・Wrangler は不要（製品の JS ビルド要件は別途維持）。署名・転送・検証の正本は同梱の `scripts/R2Transfer.Core.cs` とし、PS helper は公開関数の互換入口とする。更新時は中核と使用する入口を同じディレクトリへ同梱する。固定名は `no-cache, must-revalidate`、バージョン付き成果物は不変として 1 年キャッシュし、payload を先、assets・RELEASES・releases を最後に送信する。

リポジトリルートで事前確認を行い、失敗時は「署名証明書が見つからないよ。SimplySign Desktop を起動してトークンでログインしてから /vava を再実行してね」。公開依頼時に成功を確認してから実行する。

```powershell
pwsh -NoProfile -Command 'if (-not (Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -like "CN=Open Source Developer Yuichiro Shinozaki*" -and $_.NotAfter -gt (Get-Date) })) { exit 1 }'
if ($LASTEXITCODE -eq 0) { pwsh -NoProfile -File scripts/release-local.ps1 } else { throw '署名証明書が見つからないよ。SimplySign Desktop を起動してトークンでログインしてから /vava を再実行してね' }
```
