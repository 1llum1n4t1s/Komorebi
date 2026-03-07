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
dotnet run --project src/SourceGit.csproj

# Format check (CI enforced)
dotnet format --verify-no-changes src/SourceGit.csproj

# Release publish (platform-specific, AOT by default)
dotnet publish src/SourceGit.csproj -c Release -o publish -r win-x64

# Build without AOT (faster for local testing)
dotnet publish src/SourceGit.csproj -c Release -o publish -r win-x64 -p:DisableAOT=true

# Build without update detection
dotnet build -p:DisableUpdateDetection=true
```

No test project exists in this codebase.

## Solution Structure

`SourceGit.slnx` (XML-based solution file):
- `src/SourceGit.csproj` — main application
- `depends/AvaloniaEdit/` — git submodule (text editor for diff/blame views)
- `.github/workflows/` — CI/CD workflows
- `build/` — packaging scripts and resources

## Architecture

### MVVM Pattern
- **ViewModels** (`src/ViewModels/`, ~85 files) — inherit `ObservableObject` (CommunityToolkit.Mvvm). Dialog VMs inherit `Popup` base class.
- **Views** (`src/Views/`, ~295 files) — Avalonia XAML (`.axaml`) with compiled bindings (`x:DataType`)
- **Models** (`src/Models/`, ~73 files) — plain data classes for git objects and app state
- **Converters** (`src/Converters/`) — IValueConverters for XAML bindings

### Git Command Layer
`src/Commands/` (~82 files) wraps git CLI invocations:
- `Command.cs` is the base — configures `Process.StartInfo`, handles stdout/stderr capture
- Each subclass sets `Args` and calls `Exec()` or `ExecAsync()`
- Commands are stateless: create, configure, execute

### Key ViewModels
- `Launcher.cs` / `LauncherPage.cs` — top-level window with tab management
- `Repository.cs` — central VM for an open repo (branches, tags, history, working copy)
- `Histories.cs` — commit graph and log
- `WorkingCopy.cs` — staging/unstaging, diff, committing
- `Popup.cs` — base class for all dialog VMs
- `SelfUpdate.cs` — handles Velopack download progress and apply

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
- XAML resource dictionaries in `src/Resources/Locales/` (14 languages)
- `en_US.axaml` is the reference locale
- `build/scripts/localization-check.js` validates translations
- Keys follow `Text.Category.Name` convention

### Application Entry Point
`App.axaml.cs` contains `Main()`. The app can also launch as a rebase editor (invoked by git during interactive rebase). `App.axaml.cs` is split across partial classes: `App.Commands.cs`, `App.Extensions.cs`, `App.JsonCodeGen.cs`.

## Code Style

Enforced via `.editorconfig` and `dotnet format` in CI:
- 4-space indent for C#, 2-space for XAML/XML/JSON
- `var` preferred everywhere
- Braces on new line (Allman style)
- Private fields: `_camelCase`; private static: `s_camelCase`; constants: `PascalCase`
- No `this.` qualifier

## CI/CD

- **format-check.yml** — `dotnet format --verify-no-changes` on PRs to `develop`
- **localization-check.yml** — validates locale files against `en_US`
- **ci.yml** — builds all platforms + packages on push/PR to `develop`
- **release.yml** — triggered by `v*` tags: builds → packages (zip/deb/rpm/AppImage) → Velopack → GitHub Release
- **velopack.yml** — reusable workflow creating Velopack packages for 6 RIDs (win-x64, win-arm64, osx-x64, osx-arm64, linux-x64, linux-arm64)

Version format: `VERSION` file stores `YYYY.MM` (e.g., `2026.05`). CI converts to semver `YYYY.M.0` for Velopack.

## Key Dependencies

- **Avalonia 11.3.x** — cross-platform XAML UI
- **CommunityToolkit.Mvvm** — MVVM source generators
- **Velopack 0.0.1298** — auto-update framework
- **depends/AvaloniaEdit** — git submodule, text editor for diff/blame
- **OpenAI / Azure.AI.OpenAI** — AI commit message generation
- **LiveChartsCore** — contribution statistics charts
