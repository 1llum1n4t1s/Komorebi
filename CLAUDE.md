# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Komorebi** is a fork of [SourceGit](https://github.com/sourcegit-scm/sourcegit), an open-source, cross-platform Git GUI client built with **C#/.NET 10** and **Avalonia UI 12.0.1**. It wraps the git CLI to provide a visual interface for git operations. The fork's GitHub repository is `https://github.com/1llum1n4t1s/Komorebi`.

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
- **ViewModels** (`src/ViewModels/`, ~128 files) — inherit `ObservableObject` (CommunityToolkit.Mvvm). Dialog VMs inherit `Popup` base class (which itself inherits `ObservableValidator` for validation support).
- **Views** (`src/Views/`, ~272 files) — Avalonia XAML (`.axaml`) + code-behind (`.axaml.cs`) with compiled bindings (`x:DataType`)
- **Models** (`src/Models/`, ~74 files) — plain data classes for git objects and app state
- **Converters** (`src/Converters/`) — IValueConverters for XAML bindings

### Git Command Layer
`src/Commands/` (~82 files) wraps git CLI invocations:
- `Command.cs` is the base — configures `Process.StartInfo`, handles stdout/stderr capture
- Each subclass sets `Args` and calls `Exec()` or `ExecAsync()`
- Commands are stateless: create, configure, execute

**Base class shared utilities** (use these instead of re-implementing):
- `ExecWithSSHKeyAsync(remote)` — fetches SSH key from git config then runs `ExecAsync()` (used by Push/Pull/Fetch)
- `ResolveGitRelativePath(path)` — resolves a potentially-relative git output path against `WorkingDirectory`
- `ParseNameStatusLine(line)` — parses `--name-status` output lines (M/A/D/R/C) into `(path, ChangeState)` tuples

### SSH Key Management
`src/Models/SSHKeyInfo.cs` scans `~/.ssh/` for private keys and provides a unified selection model. `src/Views/SSHKeyPicker.axaml` is a reusable `UserControl` for key selection with entry types: None (global setting), Key, CustomKey, Browse.

**2-tier fallback strategy** (in `Command.ResolveSSHKeyValue` — pure function, unit testable):
1. Per-remote setting (`git config remote.<name>.sshkey`)
2. Global SSH key (`Preferences.Instance.GlobalSSHKey`)
3. ssh-agent / `~/.ssh/config` (when both are empty)

**Legacy `__NONE__` sentinel**: 旧バージョンで書き込まれた `__NONE__` は「グローバルフォールバックを明示的にスキップ」として読み取り時に尊重する。新 UI からは書き込まれない（凍結レガシー）。`LegacySSHKeyOptOutSentinel` 定数で管理。

`GIT_SSH_COMMAND` is built with shell injection prevention via `.Quoted()`.

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
- `App.Check4Update()` uses `UpdateManager` + `GithubSource` to check GitHub releases
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

### Process stdout/stderr must be read concurrently
`ReadToEnd()` and `ReadToEndAsync()` in `Command.cs` read stdout and stderr in parallel to avoid deadlocks. When adding new process-spawning code, **never** read stdout fully before starting to read stderr — if the stderr buffer fills (4KB–64KB), the process blocks waiting for stderr to be consumed, while the caller blocks waiting for stdout to finish.

### Temporary files need try/finally cleanup
When using `Path.GetTempFileName()` for git `--file=` or `-F` options, always wrap in `try/finally` to ensure deletion even if `ExecAsync()` throws. See `Commit.cs` and `Tag.cs` for the pattern.

### Independent git commands should run in parallel
When a ViewModel needs results from multiple independent git commands, use `Task.WhenAll` instead of sequential awaits. Examples: `BlameCommandPalette.cs`, `Compare.cs`, `Histories.cs`.

### HttpClient must be reused
`AvatarManager.cs` uses a static `HttpClient` instance. Never create `new HttpClient()` per request — it causes socket exhaustion.

### Watcher lock scope must not include MarkWorkingCopyDirtyManually
When using `LockWatcher()` in a `Popup.Sure()` override, the lock must be released **before** calling `MarkWorkingCopyDirtyManually()`. If the lock is held during the call, FileSystemWatcher events that were buffered during the lock get delivered after unlock, causing `_updateWC` to be re-set. This triggers a second `RefreshWorkingCopyChanges()` that cancels the first one's `CancellationToken`, resulting in the UI not updating. Use a block-scoped `using (_repo.LockWatcher()) { ... }` around the command execution only, then call `MarkWorkingCopyDirtyManually()` outside the block. See `Discard.cs` for the correct pattern.

### CommitGraph performance considerations
`CommitGraph.Parse()` processes potentially tens of thousands of commits. It uses `HashSet` for O(1) color recycling checks and `Dictionary` for O(1) parent lookups. When modifying this code, avoid introducing `List.Find()`, `List.Contains()`, or `List.Remove()` in inner loops.

### Histories SHA lookup uses dictionary
`Histories.cs` maintains a `_commitBySha` dictionary (rebuilt when `Commits` is set) for O(1) commit lookups by SHA. When adding new commit-search logic in Histories, use `_commitBySha.TryGetValue()` instead of `_commits.Find()`. The dictionary is automatically kept in sync with the commit list.

### Avalonia Fluent theme overrides ToggleButton template parts by name
When creating custom `ControlTemplate` for `ToggleButton`, **do not** name `ContentPresenter` as `PART_ContentPresenter`. The Fluent theme has a built-in style `ToggleButton:checked /template/ ContentPresenter#PART_ContentPresenter` that forces the accent color onto any `ContentPresenter` with that name. Use a custom name (e.g., `x:Name="ExpanderContent"`) to avoid the Fluent theme overriding your Background. The same applies to `Border#PART_LayoutRoot` and other PART-named elements.

### Avalonia style specificity: TemplateBinding vs DynamicResource in templates
Inside a `ControlTemplate`, `{TemplateBinding Background}` reads the control's `Background` property — which external styles (including Fluent theme pseudo-class styles like `:checked`, `:pointerover`) can change. If you want a template element's background to be immune to external style changes, use `{DynamicResource Brush.MyBrush}` directly instead of `{TemplateBinding Background}`.

### ListBox with SelectionMode=AlwaysSelected resets bound index during layout
When a `ListBox` with `SelectionMode="AlwaysSelected"` lays out, it forces the selection to index 0, overwriting any programmatically set bound value. If you need to set a default selection that differs from index 0 (e.g., `ShowLocalChangesByDefault`), use `Dispatcher.UIThread.Post(() => ..., DispatcherPriority.Background)` to defer the assignment until after layout completes. When doing so, guard against overwriting explicit user interaction by checking `if (currentIndex == 0)` before setting — this distinguishes the framework reset from a deliberate user selection.

### ContentControl recycles Views when the new Content has the same type
Avalonia's XAML `<DataTemplate>` implements `IRecyclingDataTemplate`. When `ContentControl.Content` switches between two instances of the same VM type (e.g., tab A's `LauncherPage` → tab B's `LauncherPage`, or `Histories` VM A → `Histories` VM B), `ContentPresenter.UpdateChild()` reuses the existing View instance and just swaps `DataContext`. Consequence: the View's `OnLoaded`/`OnUnloaded` only fire on the *first* attach — they do **not** fire on every switch. State that needs to follow VM lifecycle (scroll position, focus, deferred restoration, event subscriptions to the old VM) must be implemented by listening to `DataContextChanged` or by living on the VM, not in code-behind fields. To force recreation, you can implement a custom `IDataTemplate` that does **not** also implement `IRecyclingDataTemplate`, but be aware this caused visible screen flickering on tab switches in this project and was reverted.

### Debug logging in WinExe builds needs Debug.WriteLine
`Komorebi.csproj` is `<OutputType>WinExe</OutputType>` so there is no attached console — `Console.WriteLine` output goes nowhere. Use `System.Diagnostics.Debug.WriteLine(...)` instead; it routes to the debugger trace listener (Visual Studio "Output" pane, `dotnet trace`, etc.). Always strip temporary `Debug.WriteLine` instrumentation before committing.

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

- **Avalonia 12.0.1** — cross-platform XAML UI
- **CommunityToolkit.Mvvm** — MVVM source generators
- **SuperLightLogger** — logging (NLog-compatible File Target, async writer)
- **Velopack 0.0.1298** — auto-update framework
- **depends/AvaloniaEdit** — git submodule, text editor for diff/blame
- **OpenAI / Azure.AI.OpenAI** — AI commit message generation
- **LiveChartsCore 2.0.0** — contribution statistics charts

Fonts are **not bundled** — the app uses system fonts with per-locale fallback chains defined in `InstalledFont.GetLocaleDefaults()`. The `Avalonia.Fonts.Inter` NuGet package provides the Inter font for non-CJK locales.
