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

`Directory.Build.props` enforces strict build-time analysis:
- `EnforceCodeStyleInBuild=true` — IDE diagnostics (e.g. `IDE0005` unused usings) are promoted to build errors. A failing `dotnet build` may simply be an unused `using` — fix it rather than suppressing.
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

# Run specific test class
dotnet test --project tests/Komorebi.Tests/Komorebi.Tests.csproj --filter "FullyQualifiedName~ChangeTests"

# Run specific test method
dotnet test --project tests/Komorebi.Tests/Komorebi.Tests.csproj --filter "FullyQualifiedName~ParseLine_Untracked"

# Localization validation (CI enforced)
bun build/scripts/localization-check.js
```

ローカライズ用依存は `cd build/scripts` で `bun install --frozen-lockfile --ignore-scripts` を実行する。`packageManager` で指定した Bun を使い、Node.js・pnpm は不要。Bun の真の AOT は安定版対応待ちのため、現状は Bun ランタイムで実行する。

Bun 依存の脆弱性は `.github/workflows/dependency-security.yml` が毎日監査する。隔離領域で安全な修正だけを準備し、`build/scripts/package.json` と `build/scripts/bun.lock` に限定した PR を作成する。候補なしの日は prepare だけを実行し、候補がある場合だけ固定 base/head/merge の重い検証へ進む。NuGet 等は既存の Dependabot を維持する。

Bun の通常バージョン更新も Dependabot が毎週月曜09:00（日本時間）に行い、minor/patch は集約し major は個別 PR にする。承認待ちの CI を重複扱いするのは、既知の検証ワークフローについて、信頼済み保守 run の同じ attempt で固定 merge の全検証ジョブが成功した証拠がある場合だけ。未知のチェック・失敗・未完了は保留する。

自動マージは既定OFF。repository variable `BUN_SECURITY_NATIVE_MERGE_ACTIVATED` を明示的に `true` にするまで、日次処理は PR 作成・読取専用検証・設定待ち通知に留め、App status や merge API を書き込まない。実 GitHub App の source、ruleset bypass、draft競合を実機で確認するまで有効化しない。

有効時も、main だけに適用される active・strict=true の required status `kagayoi/automation-merge-guard-v1` と、その source App・ruleset ID・更新日時が固定値と一致しなければ保留する。設定値は `DEPENDENCY_GUARD_APP_CLIENT_ID`、`DEPENDENCY_GUARD_APP_ID`、`DEPENDENCY_GUARD_APP_SLUG`、`DEPENDENCY_GUARD_APP_BOT_ID`、`DEPENDENCY_GUARD_RULESET_ID`、`DEPENDENCY_GUARD_RULESET_UPDATED_AT`、秘密は `DEPENDENCY_GUARD_PRIVATE_KEY`。専用 App の token は対象 repo の statuses:write に限定し、候補コードへ渡さない。署名前に同 token で App の ID・client ID・slug と bot の ID・login を読み、owner の固定値と照合する。GITHUB_TOKEN は merge を担当し、App・GITHUB_TOKEN に bypass がないことを導入前に owner が確認する。

候補は `automation/bun-security-<UUID>` と UUID・`Bun-Security-Automation: v1` を本文に含む commit で区別する。初回 head の親は `[base]`、既存候補の更新は同じ PR・branch に ledger で準備記録を残し、二親 `[oldHead, base]` commit を GraphQL `updateRefs(beforeOid=oldHead, force=false)` で fast-forward CAS する。未知の結果は再送せず保留し、close・draft の同時変更と branch 更新の原子的な連動は保証しない。本人の auto-merge 解除は操作前後に timeline を確認するが、反映遅延や最終読取後から PUT までの同時操作は GitHub API で排除できない。head と固定 merge の tree を一致させ、merge の親 `[base, head]` を確認する。

信頼済み job は同一 run/attempt の固定 merge 検証と PR・本人操作・rule を確認し、nonce と固定 snapshot を ledger に保存・再読後、専用 App で nonce 付き success status を head に発行し、再読した署名証拠を保存する。中断後は同 nonce の status を検索し、再投稿しない。reconcile で merge を確認した run は古い checkout の監査を省き、次回に現 main を監査する。全条件を再確認して PUT intent を記録し、同期 `PUT /pulls/{number}/merge` を `sha=head`・`merge_method=squash` で一度だけ要求する。200後も PR・実commitの親 `[base]`・tree・main祖先を検証する。405/409/422 は確定拒否として記録し、timeout・5xx・不明応答は intent のまま再送せず保留する。status 発行後に他 actor が同じ head を利用して native予約を行わないことは別の信頼前提であり、App source・bypass の実機確認まで activation は OFF のままとする。

固定 issue の bot comment ledger に guard status proof・merge intent・proposal を記録する。管理者は導入時に対象 repository の数値 ID と issue 番号を確定し、本人または Actions bot が作成した専用 open issue の本文を以下の2段落と完全一致させ、repository variable `BUN_SECURITY_LEDGER_ISSUE` にその正整数の issue 番号を設定する。`<repo_id>` と `<issue_number>` は実数値に置き換える。作成や設定は導入承認後に別途行い、未設定・不一致・閉鎖時に自動再初期化しない。

```text
Bunの脆弱性自動修正の処理状態を保持するIssueです。自動処理のため開いたままにしてください。

<!-- bun-security-ledger-v1:{"version":1,"repo":<repo_id>,"issue":<issue_number>,"state":"idle"} -->
```

ledger の `preparing` は新 branch・PR 作成前、`preparing-update` は既存 branch のCAS前に記録する。応答不明・head不一致は保留する。`proposal` は PR・branch・base・head・依存2 blob を保持し、本人が閉じた直近候補の同内容再作成を防ぐ。現 PR の candidate または base が変われば同じ PR をCAS更新し、本人の draft・auto-merge解除・予約・未解決要求があれば保留する。完全解決時は proposal だけ解除し PR は閉じない。PUT開始前の失敗だけ `aborted-before-request` として記録し、確定拒否後も同じ snapshot・CI attempt は再送しない。statusだけ存在し対応する ledger proof がない場合は孤立証拠として保留する。

通常通知 issue は ledger と分けて本文を上書きせず、main の audit と未merge候補の audit を区別する。main の未対策が残る間は候補が clean でも通知を維持し、merge 後の新 main は次回実監査まで解決済みにしない。最新 main の audit が clean または正規に検証した mitigated、remaining空・followup/upstreamなし・候補変更なし、ledger idle の場合に限り state を閉じ、close 後に issue と main を再取得する。旧 PR の open は通知閉鎖を妨げない。現時点の Komorebi の承認済み mitigation は空である。ledger issue 自体は閉じない。

Status API と issue comment には CAS がない。直前直後の再読と workflow concurrency を維持し、不明時は保留する。通知 close 後の再取得に失敗した場合は job を失敗させ、通知は API が利用できるときに best effort で記録する。他人の issue は自動 reopen しない。

ローカライズ CLI は全 XML の読込・解析・生成が成功するまで書き込まない。書込中の障害まで原子的に復旧するものではない。

`localization-check.yml` は `contents: read` で PR・再利用呼出しを検証する。main への push 時の翻訳整形・文書の自動コミットは、`contents: write` を明示した `localization-update.yml` が担当する。

Test project: `tests/Komorebi.Tests/` — xUnit v3, references `src/Komorebi.csproj`.

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
`src/Commands/` wraps git CLI invocations:
- `Command.cs` is the base — configures `Process.StartInfo`, handles stdout/stderr capture
- Each subclass sets `Args` and calls `Exec()` or `ExecAsync()`
- コマンドは操作ごとに生成する。`Args`・`WorkingDirectory`・認証指定・`ErrorMessage` などの実行状態を持つため、同じインスタンスを並行実行しない。`ErrorMessage` は直近の `ExecAsync()` の秘密値除去済みエラーで、再実行ごとにリセットされる。

**Base class shared utilities** (use these instead of re-implementing):
- `ExecWithSSHKeyAsync(remote)` — fetches SSH key from git config then runs `ExecAsync()` (used by Push/Pull/Fetch)
- `ResolveGitRelativePath(path)` — resolves a potentially-relative git output path against `WorkingDirectory`
- `ParseNameStatusLine(line)` — parses `--name-status` output lines (M/A/D/R/C) into `(path, ChangeState)` tuples

### SSH Key Management
`src/Models/SSHKeyInfo.cs` scans `~/.ssh/` for private keys and provides a unified selection model. `src/Views/SSHKeyPicker.axaml` is a reusable `UserControl` for key selection with entry types: None (global setting), Key, CustomKey, Browse.

**3-tier fallback strategy** (in `Command.ResolveSSHKeyValue` — pure function, unit testable):
1. Per-remote setting (`git config remote.<name>.sshkey`)
2. Global SSH key (`Preferences.Instance.GlobalSSHKey`)
3. ssh-agent / `~/.ssh/config` (when both are empty)

**Legacy `__NONE__` sentinel**: 旧バージョンで書き込まれた `__NONE__` は「グローバルフォールバックを明示的にスキップ」として読み取り時に尊重する。新 UI からは書き込まず、読み取り専用の凍結レガシーとして `LegacySSHKeyOptOutSentinel` 定数で管理する。

`GIT_SSH_COMMAND` is built in `Command.CreateGitStartInfo` with platform-aware quoting — POSIX single-quote escaping (`SSHKey.Replace("'", "'\\''")`) and `-F '/dev/null'` on Unix, Windows uses double-quote escaping and `-F "NUL"`. The `.Quoted()` helper in `App.Extensions.cs` is used separately for git CLI argument paths (double-quote only) and is kept distinct here rather than reused.

### AWS CodeCommit Support
Three URL formats are supported:
- **HTTPS**: `https://git-codecommit.{region}.amazonaws.com/v1/repos/{repo}`
- **SSH**: `ssh://git-codecommit.{region}.amazonaws.com/v1/repos/{repo}`
- **GRC** (git-remote-codecommit): `codecommit::{region}://{profile}@{repo}`

Key utilities in `Remote.cs`: `IsCodeCommitProtocol()`, `TryParseCodeCommitHTTPS()`, `TryParseCodeCommitSSH()`, `TryParseCodeCommitGRC()`. `TryGetVisitURL()` and `TryGetCreatePullRequestURL()` convert all three forms to AWS Console URLs. `RemoteProtocolSwitcher` hides for CodeCommit URLs (HTTPS↔SSH auto-conversion not applicable).

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
- XAML resource dictionaries in `src/Resources/Locales/` (18 languages)
- Supported: ca_ES, de_DE, en_US, es_ES, fil_PH, fr_FR, id_ID, it_IT, ja_JP, ko_KR, la, pt_BR, ru_RU, sa, ta_IN, uk_UA, zh_CN, zh_TW
- `en_US.axaml` is the reference locale — all other locales must match its key set
- `build/scripts/localization-check.js` validates translations in CI
- Keys follow `Text.Category.Name` convention (e.g., `Text.InitSetup.Message`)
- `Models/Locales.cs` defines the `Locale.Supported` list used in UI dropdowns
- `App.SetLocale()` swaps the active `ResourceDictionary` at runtime
- Each locale must be registered in `App.axaml` as `<ResourceInclude x:Key="xx_YY">`
- First-launch: `InitSetup` initializes the language from `Preferences.DetectedLocale`, then lets the user confirm or change it together with the default clone directory

### Theme System
`src/Resources/Themes.axaml` defines 5 built-in themes (Default/Light/Dark/White/OneDark) as `ResourceDictionary` entries with `ThemeVariant` keys. Each theme defines `Color.*` resources that `Brush.*` `SolidColorBrush` resources reference via `DynamicResource`. User-customizable color overrides are applied via `Models/ThemeOverrides.cs` which loads a JSON file and merges overrides into the active resource dictionary at runtime. When adding new themed colors, define both the `Color` and `Brush` in `Themes.axaml` and reference them with `{DynamicResource Brush.MyName}` in AXAML — use resources rather than hardcoding color literals.

### Alert Dialog for Modal-Context Errors
`src/Views/Alert.axaml` is a small child modal dialog for displaying errors that occur **inside** an already-open modal dialog (e.g., file picker failures in the Preferences dialog). Use this instead of `App.RaiseException(...)` in modal-dialog code-behind, because the standard inline notification banner is rendered on the parent Launcher window and gets hidden behind the modal.

Usage: `await new Alert().ShowAsync(this, message, isError: true);` — titles are localized via `Launcher.Error` / `Launcher.Info` keys. The dialog is resizable (`CanResize=True` + `MinWidth/MinHeight`) and wraps long messages in a `ScrollViewer`.

### Window State Persistence Pattern
Stand-alone windows persist width/height/position/state across sessions via `ViewModels.LayoutInfo` properties. `FileHistories` and `Blame` use this pattern:

1. **Constructor**: set `Width` / `Height` from `LayoutInfo` (safe — no `Screens` dependency). Leave `Position` unset in the constructor: `App.ShowWindow(...)` will overwrite it with an active-screen-centered value before calling `Show()`, which serves as a deterministic first-launch fallback.
2. **OnOpened**: call `TryRestoreWindowPosition(x, y, w, h)` (protected helper on `ChromelessWindow`) — returns true if the saved `PixelRect` fits entirely within a connected screen's working area, and sets `Position` accordingly, overriding the centering from step 1. Also restore `WindowState = Maximized` if previously maximized. If `TryRestoreWindowPosition` returns false (first launch, or saved coords on a disconnected monitor), no action is needed — the step-1 centering remains as fallback. Subscribe `PositionChanged` after restoring position and state, so initial centering cannot overwrite the saved position.
3. **OnSizeChanged** / **OnPositionChanged**: save to `LayoutInfo` only when `WindowState == Normal` (avoid saving maximized/snapped sizes).
4. **OnPropertyChanged(WindowStateProperty)**: save state only when `!= Minimized` (otherwise a taskbar-minimize would cause the next launch to start minimized).

This order means a returning user with a valid saved position may see a one-frame flash at the centered position before `OnOpened` snaps to the saved coordinates (Avalonia does not expose the required `Screens` state before `OnOpened` for this cross-monitor restore path). The flash is acceptable in exchange for correct fallback on first launch.

`Launcher` is the exception: its constructor restores size and a valid saved position directly (or sets `WindowStartupLocation.CenterScreen`), subscribes `PositionChanged` there, and `OnOpened` restores only maximized/full-screen state. Keep that separate path when changing window persistence.

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

Enforced via `.editorconfig` and `dotnet format` in CI:
- 4-space indent for C#, 2-space for XAML/XML/JSON
- `var` preferred everywhere
- Braces on new line (Allman style)
- Private fields: `_camelCase`; private static: `s_camelCase`; constants: `PascalCase`
- No `this.` qualifier
- Collection expressions `[]` preferred over `new List<T>()` / `new Dictionary<K,V>()` (C# 12+). Use `List<T> x = []` instead of `var x = new List<T>()`

## CI/CD

- **format-check.yml** — `dotnet format --verify-no-changes` on push/PR to `main`
- **localization-check.yml** — validates locale files against `en_US`
- **ci.yml** — lightweight: `dotnet build` + `dotnet test` on ubuntu-latest (single runner, no AOT publish)
- **release.yml** — triggered by push to `release/**` branches: full AOT publish (5 platforms) → packages (zip/deb/rpm/AppImage) → Velopack (osx/linux のみ) → R2 単独配信 (GitHub Releases は作らない)
- **build.yml** — reusable workflow for 5-platform AOT publish (used by release.yml only。win-* は `package.yml` の standalone zip 用に残置)
- **velopack.yml** — reusable workflow creating Velopack packages。**win-x64 / win-arm64 は matrix から除外済み** — 未署名 win フィードがローカル署名リリースの成果物を R2 上で上書きしないようにするため
### Windows リリース (ローカル実行)

- `/vava` で `Directory.Build.props` のバージョンを確定した後、`pwsh scripts/release-local.ps1` で win-x64 / win-arm64 の build・署名・R2 upload・cleanup を行う。アップロードしない検証は `-SkipUpload` を使う。前提条件と対象を絞る `-Runtimes` はスクリプト冒頭を正本とする。`vpk pack --runtime` の明示指定を維持し、生成する CPU と更新チャンネルを一致させる。アップロード後は固定 URL の取得内容を比較し、不一致の場合だけキャッシュをパージする。全配信ファイルの SHA-256 一致まで確認してから旧版清掃へ進む。HTTP 200 だけを配信完了条件にしない。
- ローカルスクリプトのアップロード前検証（manifest・nupkg・portable の版・CPU・署名・hash）を維持する。`-SkipUpload` でもこの検証を通す。
- 製品ページの配信は兄弟リポジトリの `../vps-web/deploy/deploy-lp.ps1` を使う。公開ホスト・更新ファイルの既存経路を維持する。

### macOS / Linux + standalone パッケージ (CI)

`release/**` ブランチへの push で `release.yml` が `build.yml` → `package.yml` → `velopack.yml` → `r2-upload` を順に呼ぶ。アップロード前に `python3 build/scripts/verify-release-artifacts.py --packages packages --version <version> --source-sha <40桁SHA>` で最終配布物を検証する（Linux 上で `rpm`・`rpm2cpio`・`squashfs-tools`・`libarchive-tools` が必要）。版・ソース SHA・CPU・manifest の hash/size と各パッケージの実体が一致しない場合は配信しない。

旧版清掃は所有する配信経路に限定する。CI は Windows の全世代の nupkg・standalone zip・固定名 installer を保護し、Windows はローカル署名リリースだけが清掃する。ローカル側は未ビルドのチャンネルの manifest 参照も保持し、取得・形式検証に失敗したら清掃を中止する。並行配信中の相手の新パッケージを削除しない理由は [DESIGN.md](DESIGN.md#不変条件と採用済み設計判断) を参照する。

> ℹ️ `package.yml` の win standalone zip (`komorebi_*.zip`) は引き続き CI で生成される未署名バイナリ。署名対象に含めたい場合はローカルスクリプトへの移植が必要。

Linux builds run directly on `ubuntu-latest` runner (no Docker container). arm64 cross-compilation adds ports.ubuntu.com sources with dynamic codename detection. RPM packaging skips `brp-strip` for cross-arch binaries (`--define "__strip /bin/true"`).

Version format: `Directory.Build.props` stores the semantic version in the `<Version>` tag. CI reads it directly for both packaging and Velopack.

## Key Dependencies

- **Avalonia 12.1.4** — cross-platform XAML UI。`Avalonia.Controls.DataGrid` は現在 12.1.2。本体とは指定バージョンが異なるため、更新時は各パッケージの利用可能なバージョンと互換性を確認する。
- **CommunityToolkit.Mvvm** — MVVM source generators
- **SuperLightLogger** — logging (NLog-compatible File Target, async writer)
- **Velopack 1.2.0** — auto-update framework (`VelopackUpdateDialog.Avalonia` 経由の推移的依存)
- **depends/AvaloniaEdit** — vendored (directly tracked, not a git submodule), text editor for diff/blame。TextMateSharp / TextMateSharp.Grammars は 2.0.4。保守時の制約と追加検証は本書の Tests 節を参照する。
- **OpenAI 2.14.0 / Azure.AI.OpenAI 2.9.0-beta.1** — AI commit message generation
- **LiveChartsCore.SkiaSharpView.Avalonia 2.1.0-dev-798** — contribution statistics charts。Avalonia 12 対応版を選び、更新時は `LibraryCompatibilityTests` でチャート生成の互換性を検証する。
- **BitMiracle.LibTiff.NET / Pfim** — TIFF / DDS image format support in ImageDiffView
- **Tmds.DBus.Protocol** — Linux desktop DBus integration (notifications, etc.)
- **AvaloniaUI.DiagnosticsSupport** (Debug builds only) / **CRDebugger.Avalonia** — Avalonia diagnostics helpers

CJK フォントは同梱せず、`InstalledFont.GetLocaleDefaults()` のロケール別フォールバックでシステムフォントを使う。非 CJK 向けの Inter は `Avalonia.Fonts.Inter` NuGet パッケージから提供する。

## Upstream-Faithful Policy

Komorebi tracks `sourcegit-scm/sourcegit` via periodic cherry-pick batches. To keep future merges tractable, follow these rules when reviewing AI bot suggestions or writing changes that touch files upstream also maintains:

1. **Accept real bugs / regressions** even when they diverge from upstream — e.g., NPE guards (PR #14 `GetActiveWorkspace()?.DefaultCloneDir`), missed `.ToLocalTime()` in `About` (PR #16), latent conditional logic breakage. Add an inline comment noting the deviation from upstream so future sync can either import an equivalent fix or revert intentionally.
2. **Decline byte-for-byte stylistic suggestions** that only improve the local file — e.g., "Localize this hardcoded error message", "Extract this duplicated helper", "Rename this method for consistency". These turn every cherry-pick into a 3-way merge conflict without net benefit; route them as a PR to upstream instead.
3. **Komorebi-only architectural decisions** are preserved regardless of upstream churn: `App.RaiseException` (vs upstream's `Models.Notification.Send`), unified `WelcomeToolbar` (vs removed `RepositoryToolbar`), SSH key picker (`SSHKeyPicker` + `LegacySSHKeyOptOutSentinel`), CodeCommit URL handling, Anthropic AI provider, SuperLightLogger, file-scoped namespaces + Japanese XML doc comments, collection expressions `[]`.
4. **Cherry-pick batches are tracked in** `plan` documents (e.g., `~/.Codex/plans/goofy-finding-ullman.md`) with SHA-level status (applied / declined / deferred). When skipping an upstream commit, record the rationale in the plan so the next sync session doesn't re-evaluate it from scratch.
5. **When bot reviews repeat the same Decline across rounds**, post one consolidated decline comment and rely on CI-green merges. Bots regularly re-raise closed items; treat recurrence as noise rather than severity escalation.

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
