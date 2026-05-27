# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Komorebi** is a fork of [SourceGit](https://github.com/sourcegit-scm/sourcegit), an open-source, cross-platform Git GUI client built with **C#/.NET 10** and **Avalonia UI 12.0.2**. It wraps the git CLI to provide a visual interface for git operations. The fork's GitHub repository is `https://github.com/1llum1n4t1s/Komorebi`.

## Build & Run

```bash
# Restore (includes git submodule for depends/AvaloniaEdit)
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
- `EnforceCodeStyleInBuild=true` — IDE diagnostics (e.g. `IDE0005` unused usings) are promoted to build errors. A failing `dotnet build` may simply be an unused `using` — fix instead of suppressing.
- `GenerateDocumentationFile=true` — required for `IDE0005` to detect unused usings; produces an XML doc next to the assembly.
- `NoWarn=CS1591` — silences "missing XML doc comment" warnings so IDE-style analysis stays the only signal.
- `<Version>` here is the source of truth for both packaging and Velopack.

## Tests

```bash
# Run all tests
dotnet test tests/Komorebi.Tests/Komorebi.Tests.csproj

# Run specific test class
dotnet test tests/Komorebi.Tests/Komorebi.Tests.csproj --filter "FullyQualifiedName~ChangeTests"

# Run specific test method
dotnet test tests/Komorebi.Tests/Komorebi.Tests.csproj --filter "FullyQualifiedName~ParseLine_Untracked"

# Localization validation (CI enforced)
node build/scripts/localization-check.js
```

Test project: `tests/Komorebi.Tests/` — xUnit v3 + Moq, references `src/Komorebi.csproj`.

## Solution Structure

`Komorebi.slnx` (XML-based solution file):
- `src/Komorebi.csproj` — main application
- `tests/Komorebi.Tests/` — xUnit v3 test project
- `depends/AvaloniaEdit/` — git submodule (text editor for diff/blame views)
- `.github/workflows/` — CI/CD workflows
- `build/` — packaging scripts and resources

## Architecture

### MVVM Pattern
- **ViewModels** (`src/ViewModels/`, ~131 files) — inherit `ObservableObject` (CommunityToolkit.Mvvm). Dialog VMs inherit `Popup` base class (which itself inherits `ObservableValidator` for validation support).
- **Views** (`src/Views/`, ~142 code-behind files + ~120 `.axaml`) — Avalonia XAML (`.axaml`) + code-behind (`.axaml.cs`) with compiled bindings (`x:DataType`)
- **Models** (`src/Models/`, ~80 files) — plain data classes for git objects and app state
- **Converters** (`src/Converters/`) — IValueConverters for XAML bindings

### Git Command Layer
`src/Commands/` (~83 files) wraps git CLI invocations:
- `Command.cs` is the base — configures `Process.StartInfo`, handles stdout/stderr capture
- Each subclass sets `Args` and calls `Exec()` or `ExecAsync()`
- Commands are stateless: create, configure, execute

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

**Legacy `__NONE__` sentinel**: 旧バージョンで書き込まれた `__NONE__` は「グローバルフォールバックを明示的にスキップ」として読み取り時に尊重する。新 UI からは書き込まれない（凍結レガシー）。`LegacySSHKeyOptOutSentinel` 定数で管理。

`GIT_SSH_COMMAND` is built in `Command.CreateGitStartInfo` with platform-aware quoting — POSIX single-quote escaping (`SSHKey.Replace("'", "'\\''")`) and `-F '/dev/null'` on Unix, Windows uses double-quote escaping and `-F "NUL"`. The `.Quoted()` helper in `App.Extensions.cs` is separately used for git CLI argument paths (double-quote only) and is **not** reused here.

### AWS CodeCommit Support
Three URL formats are supported:
- **HTTPS**: `https://git-codecommit.{region}.amazonaws.com/v1/repos/{repo}`
- **SSH**: `ssh://git-codecommit.{region}.amazonaws.com/v1/repos/{repo}`
- **GRC** (git-remote-codecommit): `codecommit::{region}://{profile}@{repo}`

Key utilities in `Remote.cs`: `IsCodeCommitProtocol()`, `TryParseCodeCommitHTTPS()`, `TryParseCodeCommitSSH()`, `TryParseCodeCommitGRC()`. `TryGetVisitURL()` and `TryGetCreatePullRequestURL()` convert all three forms to AWS Console URLs. `RemoteProtocolSwitcher` hides for CodeCommit URLs (HTTPS↔SSH auto-conversion not applicable).

### Remote Configuration
`RepositoryConfigure` (VM + View) provides a unified dialog for managing remotes, including URL editing, per-remote SSH key selection, and per-remote push prohibition. Push prohibition uses `git remote set-url --push <name> no_push` to set an invalid push URL — this is the standard git idiom for preventing pushes to upstream/fork-parent remotes. The `SelectedRemotePushDisabled` property detects this state by comparing push URL with fetch URL.

### Key ViewModels
- `Launcher.cs` / `LauncherPage.cs` — top-level window with tab management. `Launcher.ActivePage` is TwoWay-bound from `LauncherTabBar` (a ListBox) and feeds the page `ContentControl` in `Launcher.axaml`.
- `Repository.cs` — central VM for an open repo (branches, tags, history, working copy). Caches three sub-view VMs (`_histories`, `_workingCopy`, `_stashesPage`) constructed in `Open()` and exposes them via `HistoriesVM`/`WorkingCopyVM`/`StashesPageVM` for content-toolbar bindings. The `SelectedViewIndex` setter swaps `SelectedView` to the matching cached VM.
- `Histories.cs` — commit graph and log
- `WorkingCopy.cs` — staging/unstaging, diff, committing
- `Popup.cs` — base class for all dialog VMs (`Sure()` = confirm action, `[Required]` validation)
- `Preferences.cs` — singleton app settings (serialized to `preference.json`)
- `InitSetup.cs` — first-launch popup for language + default clone directory selection
- `SelfUpdate.cs` — handles Velopack download progress and apply

### View Switching Pattern (Upstream-compliant)
Both tab switching and sub-view switching use `ContentControl + DataTemplate`, matching upstream SourceGit. The fork previously experimented with an `ItemsControl + Panel + IsVisible` view-caching scheme, but it was reverted because forcing View recreation introduced screen flickering and other layout regressions.

- **Tab level** (`Launcher.axaml`): `<ContentControl Content="{Binding ActivePage}">` with a `<DataTemplate DataType="vm:LauncherPage"><v:LauncherPage/></DataTemplate>`.
- **Sub-view level** (`Repository.axaml`): `<ContentControl Content="{Binding SelectedView}">` with three `<DataTemplate>` entries for `Histories`, `WorkingCopy`, `StashesPage` VM types. Toolbar visibility for the active sub-view is still gated by `SelectedViewIndex` + `IntConverters.IsZero`/`IsOne`/`IsTwo`.
- **VM caching, View recycling**: Sub-view VMs are kept alive on `Repository`, so heavy state (history graph, staged changes) survives switches. Avalonia's `ContentPresenter` also recycles the *View* instance when content switches between two instances of the same VM type (see "ContentControl recycles Views" pitfall). State that must follow VM lifecycle should live on the VM, not in code-behind fields.

### Platform Abstraction
`src/Native/`:
- `OS.cs` — static facade with `IBackend` interface
- `Windows.cs`, `MacOS.cs`, `Linux.cs` — platform implementations

### Auto-Update (Velopack)
- Entry point: `VelopackApp.Build().Run()` must be first line in `Main()` (`App.axaml.cs`)
- `App.Check4Update()` uses `UpdateManager` + `SimpleWebSource` pointed at `Preferences.UpdateBaseUrl` (= `https://komorebi.nephilim.jp`, Cloudflare R2 カスタムドメイン) as the **primary** update feed
- 配信元 URL は `Preferences.CanonicalUpdateBaseUrl` 定数で 1 箇所管理。`UpdateBaseUrl` プロパティは `[JsonIgnore]` 付きの薄いラッパーで、外部 JSON からの上書き不可
- 通常リリースは CI workflow (`.github/workflows/release.yml`) の `r2-upload` ジョブが Velopack 成果物 (nupkg/RELEASES/releases.json/Setup/AppImage/Portable.zip) と standalone パッケージ (deb/rpm/独自zip) を **R2 単独配信** する (`permissions: contents: read`、通常リリースで GitHub Releases は作らない)
- 旧 `GithubSource` クライアント救済は GitHub Releases に「踏み台 (R2 対応版を含む最初のバージョン)」を **1 つだけ** publish する方式。2 段階更新 (旧 → 踏み台版 → R2 最新) で乗り換えさせる。踏み台 publish は `/transfer-cf` 移行作業時に 1 回だけ実施し、踏み台 Release は **削除せず残す** (継続併用はしない)
- `Models.VelopackUpdate` holds `UpdateManager` + `UpdateInfo`
- `ViewModels.SelfUpdate` handles download progress and `ApplyUpdatesAndRestart()`
- `mgr.IsInstalled` guards against running in dev/unpackaged mode
- Compile flag `DISABLE_UPDATE_DETECTION` skips update checks entirely

### Localization
- XAML resource dictionaries in `src/Resources/Locales/` (17 languages)
- Supported: de_DE, en_US, es_ES, fil_PH, fr_FR, id_ID, it_IT, ja_JP, ko_KR, la, pt_BR, ru_RU, sa, ta_IN, uk_UA, zh_CN, zh_TW
- `en_US.axaml` is the reference locale — all other locales must match its key set
- `build/scripts/localization-check.js` validates translations in CI
- Keys follow `Text.Category.Name` convention (e.g., `Text.InitSetup.Message`)
- `Models/Locales.cs` defines the `Locale.Supported` list used in UI dropdowns
- `App.SetLocale()` swaps the active `ResourceDictionary` at runtime
- Each locale must be registered in `App.axaml` as `<ResourceInclude x:Key="xx_YY">`
- First-launch: `InitSetup` popup lets user choose language + clone directory (bypasses OS auto-detection)

### Theme System
`src/Resources/Themes.axaml` defines 5 built-in themes (Default/Light/Dark/White/OneDark) as `ResourceDictionary` entries with `ThemeVariant` keys. Each theme defines `Color.*` resources that `Brush.*` `SolidColorBrush` resources reference via `DynamicResource`. User-customizable color overrides are applied via `Models/ThemeOverrides.cs` which loads a JSON file and merges overrides into the active resource dictionary at runtime. When adding new themed colors, define both the `Color` and `Brush` in `Themes.axaml`, and use `{DynamicResource Brush.MyName}` in AXAML — never hardcode colors.

### Alert Dialog for Modal-Context Errors
`src/Views/Alert.axaml` is a small child modal dialog for displaying errors that occur **inside** an already-open modal dialog (e.g., file picker failures in the Preferences dialog). Use this instead of `App.RaiseException(...)` in modal-dialog code-behind, because the standard inline notification banner is rendered on the parent Launcher window and gets hidden behind the modal.

Usage: `await new Alert().ShowAsync(this, message, isError: true);` — titles are localized via `Launcher.Error` / `Launcher.Info` keys. The dialog is resizable (`CanResize=True` + `MinWidth/MinHeight`) and wraps long messages in a `ScrollViewer`.

### Window State Persistence Pattern
Stand-alone windows (FileHistories, Blame, Launcher) persist width/height/position/state across sessions via `ViewModels.LayoutInfo` properties. The pattern:

1. **Constructor**: set `Width` / `Height` from `LayoutInfo` (safe — no `Screens` dependency) and subscribe `PositionChanged`. **Do not** set `Position` in the constructor: `App.ShowWindow(...)` will overwrite it with an active-screen-centered value before calling `Show()`, which serves as a deterministic first-launch fallback.
2. **OnOpened**: call `TryRestoreWindowPosition(x, y, w, h)` (protected helper on `ChromelessWindow`) — returns true if the saved `PixelRect` fits entirely within a connected screen's working area, and sets `Position` accordingly, overriding the centering from step 1. Also restore `WindowState = Maximized` if previously maximized. If `TryRestoreWindowPosition` returns false (first launch, or saved coords on a disconnected monitor), no action is needed — the step-1 centering remains as fallback.
3. **OnSizeChanged** / **OnPositionChanged**: save to `LayoutInfo` only when `WindowState == Normal` (avoid saving maximized/snapped sizes).
4. **OnPropertyChanged(WindowStateProperty)**: save state only when `!= Minimized` (otherwise a taskbar-minimize would cause the next launch to start minimized).

This order means a returning user with a valid saved position may see a one-frame flash at the centered position before `OnOpened` snaps to the saved coordinates (Avalonia 11 has no way to resolve `Screens` pre-`OnOpened` for a cross-monitor save). The flash is acceptable in exchange for correct fallback on first launch.

### AI Commit Message Generation
`src/AI/` contains AI integration for generating commit messages. Supported providers: OpenAI, Azure OpenAI, Gemini, Anthropic.
- `Service.cs` — API client configuration (provider, server, model, API key)
- `Provider.cs` — Provider enum (OpenAI, AzureOpenAI, Gemini, Anthropic)
- `Agent.cs` — orchestrates generation with tool use (OpenAI SDK or Anthropic raw HTTP)
- `ChatTools.cs` — tool definitions for file diff retrieval (OpenAI SDK + Anthropic JSON)
- Configured in Preferences under AI settings; requires an API key

### Application Entry Point
`App.axaml.cs` contains `Main()`. The app can also launch as a rebase editor (invoked by git during interactive rebase). `App.axaml.cs` is split across partial classes: `App.Commands.cs`, `App.Extensions.cs`, `App.JsonCodeGen.cs`.

### Toolbar Architecture
The app uses a unified toolbar design (RepositoryToolbar was removed):
- **`Launcher.axaml` title bar**: Page tabs (`LauncherTabBar`) and page switcher button only.
- **`WelcomeToolbar.axaml`**: Shown on Welcome page. Contains Clone/Open/Terminal buttons, workspace selector (`● Name ▾`), and `···` overflow menu (Preferences, AppDataDir, Hotkeys, Update, About, Quit).
- **Repository view (`Repository.axaml`)**: Toolbar is split into two areas:
  - **Left sidebar top**: Branch selector, create branch button, Fetch/Pull/Push buttons (in a 36px branch bar above the filter box).
  - **Content toolbar (right panel Row 0)**: Segmented control (Histories/WorkingCopy/Stashes), search bar, view-specific action buttons, settings gear, workspace selector, and `···` overflow menu.
- Global keyboard shortcuts (Ctrl+,, F1, Ctrl+Q) are handled in `Launcher.axaml.cs` `OnKeyDown()`.

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
- **release.yml** — triggered by push to `release/**` branches: full AOT publish (5 platforms) → packages (zip/deb/rpm/AppImage) → Velopack → GitHub Release
- **build.yml** — reusable workflow for 5-platform AOT publish (used by release.yml only)
- **velopack.yml** — reusable workflow creating Velopack packages for 5 RIDs (win-x64, win-arm64, osx-arm64, linux-x64, linux-arm64)

Linux builds run directly on `ubuntu-latest` runner (no Docker container). arm64 cross-compilation adds ports.ubuntu.com sources with dynamic codename detection. RPM packaging skips `brp-strip` for cross-arch binaries (`--define "__strip /bin/true"`).

Version format: `Directory.Build.props` stores the version in `<Version>` tag (e.g., `1.0.65`). CI reads it directly for both packaging and Velopack.

## Key Dependencies

- **Avalonia 12.0.2** — cross-platform XAML UI (Note: `Avalonia.Controls.DataGrid` is intentionally pinned to 12.0.0 — bump together with main Avalonia after compatibility check)
- **CommunityToolkit.Mvvm** — MVVM source generators
- **SuperLightLogger** — logging (NLog-compatible File Target, async writer)
- **Velopack 1.0.1** — auto-update framework
- **depends/AvaloniaEdit** — git submodule, text editor for diff/blame
- **OpenAI / Azure.AI.OpenAI 2.8.0-beta.1** — AI commit message generation
- **LiveChartsCore 2.0.2** — contribution statistics charts
- **BitMiracle.LibTiff.NET / Pfim** — TIFF / DDS image format support in ImageDiffView
- **Tmds.DBus.Protocol** — Linux desktop DBus integration (notifications, etc.)
- **CRDebugger.Avalonia** (Debug builds only) — Avalonia diagnostics helper

Fonts are **not bundled** — the app uses system fonts with per-locale fallback chains defined in `InstalledFont.GetLocaleDefaults()`. The `Avalonia.Fonts.Inter` NuGet package provides the Inter font for non-CJK locales.

## Upstream-Faithful Policy

Komorebi tracks `sourcegit-scm/sourcegit` via periodic cherry-pick batches. To keep future merges tractable, follow these rules when reviewing AI bot suggestions or writing changes that touch files upstream also maintains:

1. **Accept real bugs / regressions** even when they diverge from upstream — e.g., NPE guards (PR #14 `GetActiveWorkspace()?.DefaultCloneDir`), missed `.ToLocalTime()` in `About` (PR #16), latent conditional logic breakage. Add an inline comment noting the deviation from upstream so future sync can either import an equivalent fix or revert intentionally.
2. **Decline byte-for-byte stylistic suggestions** that only improve the local file — e.g., "Localize this hardcoded error message", "Extract this duplicated helper", "Rename this method for consistency". These turn every cherry-pick into a 3-way merge conflict without net benefit. The appropriate channel is a PR to upstream.
3. **Komorebi-only architectural decisions** are preserved regardless of upstream churn: `App.RaiseException` (vs upstream's `Models.Notification.Send`), unified `WelcomeToolbar` (vs removed `RepositoryToolbar`), SSH key picker (`SSHKeyPicker` + `LegacySSHKeyOptOutSentinel`), CodeCommit URL handling, Anthropic AI provider, SuperLightLogger, file-scoped namespaces + Japanese XML doc comments, collection expressions `[]`.
4. **Cherry-pick batches are tracked in** `plan` documents (e.g., `~/.claude/plans/goofy-finding-ullman.md`) with SHA-level status (applied / declined / deferred). When skipping an upstream commit, record the rationale in the plan so the next sync session doesn't re-evaluate it from scratch.
5. **When bot reviews repeat the same Decline across rounds**, post one consolidated decline comment and rely on CI-green merges. Bots regularly re-raise closed items; do not mistake recurrence for severity escalation.
