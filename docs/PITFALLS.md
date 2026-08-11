# Common Pitfalls

このファイルは [CLAUDE.md](../CLAUDE.md) から参照される、Komorebi 固有の落とし穴集です。コードに触れる前に該当項目を確認してください。

### Repository.Close — async race ガードは `_isClosed` フラグで
`Repository.Close()` は走行中の非同期タスク (Fetch/Pull/Refresh*) が VM 内部フィールドを参照中の可能性があるため、async から触られうるフィールド (`_uiStates`, `_watcher`, `_histories`, `_workingCopy`, `_stashesPage`, `_searchCommitContext`) には **null 代入しない**。代わりに Close 冒頭で `_isClosed = true` を立て、各 async タスク (`RefreshCommits`/`RefreshWorkingCopyChanges`/`RunAutoFetchAsync` 等) は冒頭または `await` 前後で `if (_isClosed) return;` ガードする。フィールドの解放は GC + 各 VM の `Dispose()` に任せる。

同期 UI 経路でしか参照されないフィールド (`_settings`, `_filterDebounceTimer`, `_visibleTags`, `_visibleSubmodules`) は従来通り null 代入する (race 対象外)。Komorebi の Close 設計は upstream `c1d08e29` (#2289) の IDisposable 全剥がしと Komorebi 旧設計 (全 null 代入) の中間で、これは独自路線として確定済み。次回 upstream sync で sourcegit のリソース解放戦略変更があっても、`_isClosed` フラグは維持する。

### Process stdout/stderr must be read concurrently
`ReadToEnd()` and `ReadToEndAsync()` in `Command.cs` read stdout and stderr in parallel to avoid deadlocks. When adding new process-spawning code, **never** read stdout fully before starting to read stderr — if the stderr buffer fills (4KB–64KB), the process blocks waiting for stderr to be consumed, while the caller blocks waiting for stdout to finish.

### Temporary files need try/finally cleanup
When using `Path.GetTempFileName()` for git `--file=` or `-F` options, always wrap cleanup so the file is deleted even if `ExecAsync()` throws. **Prefer `using var temp = new TempFileScope();`** (defined in `App.Extensions.cs`) over hand-rolled `try/finally`; it provides RAII delete semantics with idempotent multi-Dispose. Existing `try/finally` patterns in `Commit.cs`, `Tag.cs`, `Commands/Discard.cs` are equivalent — when refactoring touches them, prefer migrating to `TempFileScope`. Never call `File.Delete(path)` after `await ExecAsync()` without try/finally (or `TempFileScope`) guard; if the await throws, the temp file leaks into `%TEMP%`.

### Independent git commands should run in parallel
When a ViewModel needs results from multiple independent git commands, use `Task.WhenAll` instead of sequential awaits. Examples: `BlameCommandPalette.cs`, `Compare.cs`, `Histories.cs`.

### HttpClient must be reused
`AvatarManager.cs` uses a static `HttpClient` instance. Never create `new HttpClient()` per request — it causes socket exhaustion.

### Watcher lock scope must not include MarkWorkingCopyDirtyManually
When using `LockWatcher()` in a `Popup.Sure()` override, the lock must be released **before** calling `MarkWorkingCopyDirtyManually()` / `MarkBranchesDirtyManually()`. If the lock is held during the call, FileSystemWatcher events that were buffered during the lock get delivered after unlock, causing `_updateWC` to be re-set. This triggers a second `RefreshWorkingCopyChanges()` that cancels the first one's `CancellationToken`, resulting in the UI not updating. **Always** use a block-scoped `using (_repo.LockWatcher()) { ... }` around git command execution only, then call `MarkWorkingCopyDirtyManually()` outside the block. **Never** use the method-scoped `using var lockWatcher = ...;` form when the method also needs to call `MarkWorkingCopyDirtyManually()` — the lock won't release until the method returns.

`LockContext.Dispose()` is **not idempotent** (`Interlocked.Decrement` runs every time), so don't double-Dispose by mixing `using var` with manual `lockWatcher.Dispose()`. Use the block scope to express the intended lock range explicitly. See `Discard.cs` and `WorkingCopy.cs` (Stage/Unstage) for the correct pattern.

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

### Avalonia 11+ `Window.Screens` is null in the constructor
`TopLevel.Screens` returns `null` until the platform windowing backend finishes initialization (i.e. until `OnOpened` fires). Any logic that queries `Screens` (screen-bounds checks, multi-monitor awareness, `ScreenFromWindow`) must live in `OnOpened` or later, not the constructor. For cross-window reuse, there is a `ChromelessWindow.TryRestoreWindowPosition(x, y, width, height)` helper that already guards against `Screens == null`.

### `App.ShowWindow(...)` sets Position before Show — use it as a fallback, not a conflict
`App.ShowWindow` (used for non-modal child windows like FileHistories/Blame) computes a center point on the active screen and assigns `window.Position = centeredRect.Position` before calling `Show()`. **Do not** set `Position` in the window's constructor — it will be silently overwritten. Instead, set `Position` in `OnOpened` (which runs after `Show()`) to override the centering when you have a valid saved coordinate. When there's no saved position, the centering remains as a correct first-launch fallback — this is why Avalonia's `WindowStartupLocation.CenterOwner` is **not** suitable here (the windows are opened via owner-less `window.Show()`, so `CenterOwner` degrades to `Manual` / OS default).

### PowerShell script encoding trap (Japanese Windows)
When running batch edits on locale files via `.ps1` scripts: **use `pwsh` (PowerShell 7+), not `powershell` (Windows PowerShell 5.1)**. PS 5.1 reads UTF-8 `.ps1` files as Shift-JIS on Japanese Windows unless they have a BOM, corrupting any non-ASCII string literals in the script **before** execution. This has historically produced mojibake in ja_JP/ko_KR/ru_RU/sa/ta_IN/uk_UA locale values. Verify locale values with `Grep` after bulk edits.

### `.axaml` files must be committed as LF to prevent `&#xD;` accumulation
`.gitattributes` pins `*.axaml text eol=lf` (and `docs/TRANSLATION.md text eol=lf`). Without this, Windows git `autocrlf` converts `\n` to `\r\n` on checkout; the xml2js/AXAML round-trip re-encodes each `\r` as `&#xD;`, so every Windows contributor adds a new layer of `&#xD;` markers until the file is unreadable. If you find unexplained `&#xD;` entities in a locale diff, check that `.gitattributes` is in effect (`git check-attr text eol -- src/Resources/Locales/ja_JP.axaml`).

### Debug logging in WinExe builds needs Debug.WriteLine
`Komorebi.csproj` is `<OutputType>WinExe</OutputType>` so there is no attached console — `Console.WriteLine` output goes nowhere. Use `System.Diagnostics.Debug.WriteLine(...)` instead; it routes to the debugger trace listener (Visual Studio "Output" pane, `dotnet trace`, etc.). Always strip temporary `Debug.WriteLine` instrumentation before committing.

### SkiaSharp 3.x — DirectWrite フォント読み取りと GC ファイナライザの競合クラッシュ
libSkiaSharp (SkiaSharp 3.x) では、GlyphTypeface 生成中の DirectWrite フォントストリーム読み取り (`SkDWriteFontFileStream::read`) と GC ファイナライザが競合すると、native 側の use-after-free で **managed ハンドラを一切通らないサイレントクラッシュ** (0xc0000005、イベントログでは `libSkiaSharp.DLL+0x2a0934`) が起きる。実際に v1.0.96〜1.0.97 で「起動 1〜6 秒後にたまに落ちる」形で 7 回発生した（2026-08 に minidump 解析で特定。`ReadFileFragment` 成功直後の `ReleaseFileFragment` で `fFontFileStream` が null 化する）。SkiaSharp 4 のライフサイクル再設計で修正済みだが、Avalonia 12.x は 3.x 系依存のため差し替えできない。

対策として `Models.FontWarmup` が `TryLaunchAsNormal` 冒頭（リポジトリ復元前・可能なら `GC.TryStartNoGCRegion` 区間内）で主要フォント＋代表フォールバック先を一括 GlyphTypeface 化し、危険経路の実行回数と GC 競合の確率を抑えている。**GlyphTypeface を大量生成する新規コードは追加しない**こと（過去に `ResolveMonospaceFont` が全システムフォントを開いて数百回読み取っていた前科がある — 判定対象を候補フォントだけに絞って解消済み）。Avalonia が SkiaSharp 4 系へ移行したら NoGC 区間は不要になる（ウォームアップ自体は初回描画の高速化として残してよい）。

### `git diff-index` の raw 形式は src/dst の 2 組 — amend 復元は必ず src 側を使う
`git diff-index --cached -M <parent>` の 1 行は `:src-mode dst-mode src-sha dst-sha status\tpath` で、**src = 親コミット側 / dst = index 側**。amend 中のアンステージは「index エントリを親コミットの状態へ戻す」操作なので、`QueryStagedChangesWithAmend` が `ChangeDataForAmend` へ詰めるのは **src-mode と src-sha の両方**でなければならない。dst-mode を拾うと「親の内容 + 新しい mode」という壊れた index になり、`chmod +x` を amend 中にアンステージしても mode 変更だけ残る（upstream sourcegit は dst-mode を拾っている。意図的な逸脱としてコメント済み）。

同様に `UpdateIndexInfo` の復元行は削除・リネームでも `DataForAmend.FileMode` を使う。ここを `"100644"` 決め打ちにすると実行可能ファイルの復元で実行ビットが落ちる。`Deleted` の dst-mode は `000000` なので、**正規表現の修正と決め打ちの除去は必ずセットで行う**（片方だけ直すと mode `000000` = 削除指示を書き込んでしまう）。回帰テストは `tests/Komorebi.Tests/Commands/QueryStagedChangesWithAmendTests.cs`（入力行は実 git から採取）。

### Window テンプレートのズームは `LayoutTransformControl` ではなく `v:ZoomHost` を使う
Avalonia の `LayoutTransformControl.ArrangeOverride` は子を**常に中央寄せ**で配置し、その配置サイズを次の判定で決める。

```csharp
if (IsSizeSmaller(finalSizeTransformed, TransformRoot.DesiredSize))
    finalSizeTransformed = TransformRoot.DesiredSize;   // ← サイズ「全体」を差し替える

private static bool IsSizeSmaller(Size a, Size b)
    => (a.Width + AcceptableDelta < b.Width) || (a.Height + AcceptableDelta < b.Height);
```

判定は**幅 OR 高さ**の論理和なのに、置き換えは**幅と高さの両方**。そのため「子の DesiredSize.Width が利用可能幅をほんの少し超える」だけで、**高さまでコンテンツの自然高さに潰され、ウィンドウ内で垂直中央寄せされる**。DPI 125% の実測では利用可能幅 1332.8 に対し DesiredSize.Width が 1333.6（ちょうど 1 物理px 超過）で発火し、高さが 822.4 → 377.6 に潰れてウィンドウの上下に約 220 DIP ずつの空白が出た。`_childActualSize` による再測定リカバリ経路は Avalonia 側でコメントアウトされていて機能しない。

これは Avalonia 12.1 の回帰ではない（`LayoutTransformControl.cs` は 12.0.5 と 12.1.1 でバイト単位同一、11.3.0 との差も購読管理のみ）。`UseLayoutRounding="False"` でも回避できない（実機で確認済み）。本家 sourcegit-scm/sourcegit も同一テンプレートのため同じ潜在バグを抱えている。

対策として `src/Views/ZoomHost.cs`（等倍率スケール専用の `Decorator`。Measure/Arrange で倍率の逆数を掛け、子に `RenderTransform` を適用するだけ）へ差し替えている。**upstream 追従で `LayoutTransformControl` を戻さないこと。** 回転・せん断が必要になった場合のみ再検討する。

### `StageChangesAsync` を Watcher ロック内から呼ぶときは `markDirty: false`
`StageChangesAsync` は既定で末尾に `MarkWorkingCopyDirtyManually()` を呼ぶ。呼び出し元が外側で `LockWatcher()` を保持したまま呼ぶと、上記「Watcher lock scope」の禁止パターンに落ちる（内側の `using` を抜けてもロックカウントは 0 にならない）。`CommitAsync` の auto-stage のように外側ロック内から呼ぶ場合は `markDirty: false` を渡し、ロック解除後に呼び出し元がまとめて `MarkBranchesDirtyManually()` する。
