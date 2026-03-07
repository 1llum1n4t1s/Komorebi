# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**SourceGit** is an open-source, cross-platform Git GUI client built with C#/.NET 10 and Avalonia UI. It wraps the git CLI to provide a visual interface for all common git operations. The project lives in `src/` as a single-project solution (no .sln file).

## Build & Run Commands

```bash
# Restore dependencies (includes git submodule for depends/AvaloniaEdit)
dotnet restore

# Build
dotnet build

# Run in development
dotnet run --project src/SourceGit.csproj

# Format check (used in CI)
dotnet format --verify-no-changes src/SourceGit.csproj

# Release build with AOT (platform-specific)
dotnet publish src/SourceGit.csproj -c Release -o publish -r win-x64
```

There is no test project in this codebase.

## Architecture

### MVVM Pattern
The app follows MVVM using **CommunityToolkit.Mvvm**:
- **ViewModels** (`src/ViewModels/`) - inherit `ObservableObject` or `ObservableValidator` (via `Popup` base class for dialog ViewModels)
- **Views** (`src/Views/`) - Avalonia XAML (`.axaml`) files with compiled bindings (`x:DataType`)
- **Models** (`src/Models/`) - plain data classes for git objects (Commit, Branch, Change, Tag, etc.)

### Git Command Layer
`src/Commands/` contains ~85 command classes that wrap git CLI invocations:
- **`Command.cs`** is the base class - configures `Process.StartInfo` to invoke git, handles stdout/stderr capture
- Each command class (e.g., `Fetch.cs`, `Push.cs`, `Blame.cs`) sets `Args` and calls `Exec()` or `ExecAsync()`
- Commands are stateless wrappers: create instance, set properties, call exec

### Key ViewModels
- **`Launcher.cs`** / **`LauncherPage.cs`** - the top-level window managing tabs
- **`Repository.cs`** - central ViewModel for an open repo; manages branches, tags, history, working copy
- **`Histories.cs`** - commit graph and log display
- **`WorkingCopy.cs`** - staging/unstaging, diff viewing, committing
- **`Popup.cs`** - base class for all dialog ViewModels (merge, rebase, push, etc.)

### Platform Abstraction
`src/Native/` provides OS-specific implementations:
- `OS.cs` - static facade that delegates to platform backends
- `Windows.cs`, `MacOS.cs`, `Linux.cs` - platform-specific implementations

### Localization
- XAML resource dictionaries in `src/Resources/Locales/` (20+ languages)
- `en_US.axaml` is the reference locale
- `build/scripts/localization-check.js` validates translations against the English keys

### Application Entry Point
`App.axaml.cs` contains `Main()`. The app can also launch as a rebase editor (invoked by git during interactive rebase).

## Code Style

Enforced via `.editorconfig`:
- 4-space indent for C#, 2-space for XAML/XML/JSON
- `var` preferred everywhere
- Braces on new line (Allman style)
- Private fields: `_camelCase`; private static fields: `s_camelCase`; constants: `PascalCase`
- No `this.` qualifier
- `dotnet format` enforced in CI on PRs

## Key Dependencies

- **Avalonia 11.3.x** - cross-platform XAML UI framework
- **CommunityToolkit.Mvvm** - MVVM source generators and base classes
- **depends/AvaloniaEdit** - git submodule for text editor (used for diff/blame views)
- **OpenAI / Azure.AI.OpenAI** - AI commit message generation

## Version

Stored in `VERSION` file at repo root (currently `2026.05`). Read at build time by the .csproj.
