# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Komorebi** is a fork of [SourceGit](https://github.com/sourcegit-scm/sourcegit), an open-source, cross-platform Git GUI client built with **C#/.NET 10** and **Avalonia UI 11.3.x**. It wraps the git CLI to provide a visual interface for git operations. The fork's GitHub repository is `https://github.com/1llum1n4t1s/Komorebi`.

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
- **ViewModels** (`src/ViewModels/`, ~128 files) — inherit `ObservableObject` (CommunityToolkit.Mvvm). Dialog VMs inherit `Popup` base class.
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

### Key ViewModels
- `Launcher.cs` / `LauncherPage.cs` — top-level window with tab management. `LauncherPage.IsActive` controls visibility for view caching.
- `Repository.cs` — central VM for an open repo (branches, tags, history, working copy). Exposes `HistoriesVM`/`WorkingCopyVM`/`StashesPageVM` for cached sub-view binding.
- `Histories.cs` — commit graph and log
- `WorkingCopy.cs` — staging/unstaging, diff, committing
- `Popup.cs` — base class for all dialog VMs (`Sure()` = confirm action, `[Required]` validation)
- `Preferences.cs` — singleton app settings (serialized to `preference.json`)
- `InitSetup.cs` — first-launch popup for language + default clone directory selection
- `SelfUpdate.cs` — handles Velopack download progress and apply

### View Caching Pattern
Tab switching and sub-view switching use `ItemsControl + Panel + IsVisible` instead of `ContentControl + DataTemplate` to avoid destroying and recreating heavy Views on every switch. This keeps all Views alive in memory and toggles `IsVisible` for instant switching.

- **Tab level** (`Launcher.axaml`): `ItemsControl` bound to `Pages`, each `LauncherPage` view's `IsVisible` binds to `LauncherPage.IsActive`. The `Launcher.ActivePage` setter toggles `IsActive` on old/new page.
- **Sub-view level** (`Repository.axaml`): `Panel` contains `v:Histories`, `v:WorkingCopy`, `v:StashesPage` directly (not via DataTemplate). Each view's `IsVisible` binds to `SelectedViewIndex` via `IntConverters.IsZero`/`IsOne`/`IsTwo`.
- **`SelectedView` property**: No longer used for XAML binding (no `PropertyChanged`). Kept only for `Fetch.cs` type checking (`_repo.SelectedView is Histories { ... }`).

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
`src/AI/` contains OpenAI/Azure OpenAI integration for generating commit messages:
- `Service.cs` — API client configuration and request handling
- `Agent.cs` — orchestrates 2-stage generation (summary → message)
- `ChatTools.cs` — tool definitions for the AI agent
- Configured in Preferences under AI settings; requires an API key

### Application Entry Point
`App.axaml.cs` contains `Main()`. The app can also launch as a rebase editor (invoked by git during interactive rebase). `App.axaml.cs` is split across partial classes: `App.Commands.cs`, `App.Extensions.cs`, `App.JsonCodeGen.cs`.

### Toolbar Architecture
The app uses a unified toolbar design (RepositoryToolbar は廃止済み):
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
- **ci.yml** — builds all platforms + packages on push/PR to `main`
- **release.yml** — triggered by push to `release/**` branches: builds → packages (zip/deb/rpm/AppImage) → Velopack → GitHub Release
- **velopack.yml** — reusable workflow creating Velopack packages for 5 RIDs (win-x64, win-arm64, osx-arm64, linux-x64, linux-arm64)

Version format: `Directory.Build.props` stores the version in `<Version>` tag (e.g., `1.0.52`). CI reads it directly for both packaging and Velopack.

## Key Dependencies

- **Avalonia 11.3.x** — cross-platform XAML UI
- **CommunityToolkit.Mvvm** — MVVM source generators
- **Velopack 0.0.1298** — auto-update framework
- **depends/AvaloniaEdit** — git submodule, text editor for diff/blame
- **OpenAI / Azure.AI.OpenAI** — AI commit message generation
- **LiveChartsCore 2.0.0** — contribution statistics charts
