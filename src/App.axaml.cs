using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Input.Platform;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using Avalonia.Styling;
using Avalonia.Threading;

using Velopack;
using Velopack.Sources;

namespace Komorebi
{
    public partial class App : Application
    {
        #region App Entry Point
        [STAThread]
        public static void Main(string[] args)
        {
            VelopackApp.Build().Run();

            Native.OS.SetupDataDir();

            Models.Logger.Initialize(new Models.LoggerConfig
            {
                LogDirectory = Path.Combine(Native.OS.DataDir, "logs"),
                FilePrefix = "Komorebi",
            });
            Models.Logger.LogStartup();

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                Models.Logger.LogCrash(e.ExceptionObject as Exception, "AppDomain.UnhandledException");
            };

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                Models.Logger.LogCrash(e.Exception, "UnobservedTaskException");
                e.SetObserved();
            };

            try
            {
                if (TryLaunchAsRebaseTodoEditor(args, out int exitTodo))
                    Environment.Exit(exitTodo);
                else if (TryLaunchAsRebaseMessageEditor(args, out int exitMessage))
                    Environment.Exit(exitMessage);
                else
                    BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                Models.Logger.LogCrash(ex, "Main");
            }
            finally
            {
                Models.Logger.Dispose();
            }
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            var builder = AppBuilder.Configure<App>();
            builder.UsePlatformDetect();
            builder.LogToTrace();
            builder.WithInterFont();
            builder.With(new FontManagerOptions()
            {
                DefaultFamilyName = "fonts:Inter#Inter"
            });
            builder.ConfigureFonts(manager =>
            {
                var monospace = new EmbeddedFontCollection(
                    new Uri("fonts:Komorebi", UriKind.Absolute),
                    new Uri("avares://Komorebi/Resources/Fonts", UriKind.Absolute));
                manager.AddFontCollection(monospace);
            });

            Native.OS.SetupApp(builder);
            return builder;
        }

        /// <summary>
        /// 後方互換性のための例外ログ出力（内部でNLogロガーに委譲する）
        /// </summary>
        public static void LogException(Exception ex, string context = null)
        {
            Models.Logger.LogCrash(ex, context);
        }
        #endregion

        #region Utility Functions
        public static Control CreateViewForViewModel(object data)
        {
            var dataTypeName = data.GetType().FullName;
            if (string.IsNullOrEmpty(dataTypeName) || !dataTypeName.Contains(".ViewModels.", StringComparison.Ordinal))
                return null;

            var viewTypeName = dataTypeName.Replace(".ViewModels.", ".Views.");
            var viewType = Type.GetType(viewTypeName);
            if (viewType != null)
                return Activator.CreateInstance(viewType) as Control;

            return null;
        }

        public static Task ShowDialog(object data, Window owner = null)
        {
            if (owner == null)
            {
                if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
                    owner = mainWindow;
                else
                    return null;
            }

            if (data is Views.ChromelessWindow window)
                return window.ShowDialog(owner);

            window = CreateViewForViewModel(data) as Views.ChromelessWindow;
            if (window != null)
            {
                window.DataContext = data;
                return window.ShowDialog(owner);
            }

            return null;
        }

        public static void ShowWindow(object data)
        {
            if (data is not Views.ChromelessWindow window)
            {
                window = CreateViewForViewModel(data) as Views.ChromelessWindow;
                if (window == null)
                    return;

                window.DataContext = data;
            }

            do
            {
                if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { Windows: { Count: > 0 } windows })
                {
                    // Try to find the actived window (fall back to `MainWindow`)
                    Window actived = windows[0];
                    if (!actived.IsActive)
                    {
                        for (var i = 1; i < windows.Count; i++)
                        {
                            var test = windows[i];
                            if (test.IsActive)
                            {
                                actived = test;
                                break;
                            }
                        }
                    }

                    // Get the screen where current window locates.
                    var screen = actived.Screens.ScreenFromWindow(actived) ?? actived.Screens.Primary;
                    if (screen == null)
                        break;

                    // Calculate the startup position (Center Screen Mode) of target window
                    var rect = new PixelRect(PixelSize.FromSize(window.ClientSize, actived.DesktopScaling));
                    var centeredRect = screen.WorkingArea.CenterRect(rect);
                    if (actived.Screens.ScreenFromPoint(centeredRect.Position) == null)
                        break;

                    // Use the startup position
                    window.WindowStartupLocation = WindowStartupLocation.Manual;
                    window.Position = centeredRect.Position;
                }
            } while (false);

            window.Show();
        }

        public static async Task<bool> AskConfirmAsync(string message, Models.ConfirmButtonType buttonType = Models.ConfirmButtonType.OkCancel)
        {
            if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner })
            {
                var confirm = new Views.Confirm();
                confirm.SetData(message, buttonType);
                return await confirm.ShowDialog<bool>(owner);
            }

            return false;
        }

        public static async Task<Models.ConfirmEmptyCommitResult> AskConfirmEmptyCommitAsync(bool hasLocalChanges, bool hasSelectedUnstaged)
        {
            if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner })
            {
                var confirm = new Views.ConfirmEmptyCommit();
                confirm.TxtMessage.Text = Text(hasLocalChanges ? "ConfirmEmptyCommit.WithLocalChanges" : "ConfirmEmptyCommit.NoLocalChanges");
                confirm.BtnStageAllAndCommit.IsVisible = hasLocalChanges;
                confirm.BtnStageSelectedAndCommit.IsVisible = hasSelectedUnstaged;
                return await confirm.ShowDialog<Models.ConfirmEmptyCommitResult>(owner);
            }

            return Models.ConfirmEmptyCommitResult.Cancel;
        }

        public static void RaiseException(string context, string message)
        {
            if (Current is App { _launcher: not null } app)
            {
                var hintKey = Models.GitErrorHelper.GetHintKey(message);
                var hint = string.IsNullOrEmpty(hintKey) ? string.Empty : app.FindLocaleString(hintKey);
                app._launcher.DispatchNotification(context, message, true, hint);
            }
        }

        public static void SendNotification(string context, string message)
        {
            if (Current is App { _launcher: not null } app)
                app._launcher.DispatchNotification(context, message, false);
        }

        /// <summary>
        ///     現在のロケールリソースから指定キーの文字列を取得する。
        ///     キーが見つからない場合は空文字列を返す。
        /// </summary>
        private string FindLocaleString(string key)
        {
            if (Resources.TryGetResource(key, null, out var value) && value is string str)
                return str;
            return string.Empty;
        }

        public static void SetLocale(string localeKey)
        {
            if (Current is not App app ||
                app.Resources[localeKey] is not ResourceDictionary targetLocale ||
                targetLocale == app._activeLocale)
                return;

            if (app._activeLocale != null)
                app.Resources.MergedDictionaries.Remove(app._activeLocale);

            app.Resources.MergedDictionaries.Add(targetLocale);
            app._activeLocale = targetLocale;
        }

        /// <summary>
        ///     アプリケーションのテーマを設定する。
        ///     ベーステーマ（Light/Dark）を適用した上で、White/OneDark等のテーマ固有の色を上書きする。
        ///     さらにユーザー定義のテーマオーバーライド（JSONファイル）があれば最後に適用する。
        /// </summary>
        /// <param name="theme">テーマ名（Default, Dark, OneDark, Light, White）</param>
        /// <param name="themeOverridesFile">ユーザー定義テーマオーバーライドのJSONファイルパス（省略可）</param>
        public static void SetTheme(string theme, string themeOverridesFile)
        {
            if (Current is not App app)
                return;

            // ベーステーマ（Light/Dark）を適用する
            app.RequestedThemeVariant = ParseThemeVariant(theme);

            // 前回適用したテーマ固有の色オーバーライドを除去する
            if (app._themeColorOverrides != null)
            {
                app.Resources.MergedDictionaries.Remove(app._themeColorOverrides);
                app._themeColorOverrides = null;
            }

            // Whiteテーマ: Lightベースに白基調の色を上書きする
            if (theme.Equals("White", StringComparison.OrdinalIgnoreCase))
            {
                var resDic = new ResourceDictionary();
                resDic["Color.Window"] = Color.Parse("#FFFFFFFF");
                resDic["Color.WindowBorder"] = Color.Parse("#FFB0B0B0");
                resDic["Color.TitleBar"] = Color.Parse("#FFF0F0F0");
                resDic["Color.ToolBar"] = Color.Parse("#FFF8F8F8");
                resDic["Color.Popup"] = Color.Parse("#FFFFFFFF");
                resDic["Color.Contents"] = Color.Parse("#FFFFFFFF");
                resDic["Color.Badge"] = Color.Parse("#FFE0E0E0");
                resDic["Color.Border0"] = Color.Parse("#FFE0E0E0");
                resDic["Color.Border1"] = Color.Parse("#FFA0A0A0");
                resDic["Color.Border2"] = Color.Parse("#FFE0E0E0");
                resDic["Color.FlatButton.Background"] = Color.Parse("#FFFFFFFF");
                resDic["Color.FlatButton.BackgroundHovered"] = Color.Parse("#FFF0F0F0");
                resDic["Color.FlatButton.FloatingBorder"] = Color.Parse("#FFA0A0A0");
                resDic["Color.InlineCode"] = Color.Parse("#FFF0F0F0");
                resDic["Color.DataGridHeaderBG"] = Color.Parse("#FFF5F5F5");
                app.Resources.MergedDictionaries.Add(resDic);
                app._themeColorOverrides = resDic;
            }
            // OneDarkテーマ: Darkベースに Atom One Dark 風の配色を上書きする
            else if (theme.Equals("OneDark", StringComparison.OrdinalIgnoreCase))
            {
                var resDic = new ResourceDictionary();

                // ウィンドウ・レイアウト系
                resDic["Color.Window"] = Color.Parse("#FF282C34");
                resDic["Color.WindowBorder"] = Color.Parse("#FF4B5263");
                resDic["Color.TitleBar"] = Color.Parse("#FF21252B");
                resDic["Color.ToolBar"] = Color.Parse("#FF2C313A");
                resDic["Color.Popup"] = Color.Parse("#FF2C313A");
                resDic["Color.Contents"] = Color.Parse("#FF21252B");

                // バッジ・コンフリクト系
                resDic["Color.Badge"] = Color.Parse("#FF4B5263");
                resDic["Color.BadgeFG"] = Color.Parse("#FFABB2BF");
                resDic["Color.Conflict"] = Color.Parse("#FFE5C07B");
                resDic["Color.Conflict.Foreground"] = Color.Parse("#FF282C34");

                // ボーダー系
                resDic["Color.Border0"] = Color.Parse("#FF1E2127");
                resDic["Color.Border1"] = Color.Parse("#FF5C6370");
                resDic["Color.Border2"] = Color.Parse("#FF3E4451");

                // フラットボタン系
                resDic["Color.FlatButton.Background"] = Color.Parse("#FF2C313A");
                resDic["Color.FlatButton.BackgroundHovered"] = Color.Parse("#FF3E4451");
                resDic["Color.FlatButton.FloatingBorder"] = Color.Parse("#FF4B5263");

                // テキスト・前景色
                resDic["Color.FG1"] = Color.Parse("#FFABB2BF");
                resDic["Color.FG2"] = Color.Parse("#FF828997");

                // Diff表示色（緑=#98C379ベース、赤=#E06C75ベース、シアン=#56B6C2）
                resDic["Color.Diff.EmptyBG"] = Color.Parse("#3C000000");
                resDic["Color.Diff.AddedBG"] = Color.Parse("#C02D4A33");
                resDic["Color.Diff.DeletedBG"] = Color.Parse("#C04D2C30");
                resDic["Color.Diff.AddedHighlight"] = Color.Parse("#A0365C3B");
                resDic["Color.Diff.DeletedHighlight"] = Color.Parse("#A06D3035");
                resDic["Color.Diff.BlockBorderHighlight"] = Color.Parse("#FF56B6C2");

                // リンク・インラインコード・ヘッダー
                resDic["Color.Link"] = Color.Parse("#FF61AFEF");
                resDic["Color.InlineCode"] = Color.Parse("#FF3E4451");
                resDic["Color.InlineCodeFG"] = Color.Parse("#FFABB2BF");
                resDic["Color.DataGridHeaderBG"] = Color.Parse("#FF2C313A");

                app.Resources.MergedDictionaries.Add(resDic);
                app._themeColorOverrides = resDic;
            }

            // 前回適用したユーザー定義オーバーライドを除去する
            if (app._themeOverrides != null)
            {
                app.Resources.MergedDictionaries.Remove(app._themeOverrides);
                app._themeOverrides = null;
            }

            // ユーザー定義テーマオーバーライド（JSONファイル）が指定されていれば適用する
            if (!string.IsNullOrEmpty(themeOverridesFile))
            {
                try
                {
                    var resDic = new ResourceDictionary();
                    using var stream = File.OpenRead(themeOverridesFile);
                    var overrides = JsonSerializer.Deserialize(stream, JsonCodeGen.Default.ThemeOverrides);
                    if (overrides == null)
                        throw new JsonException("Failed to deserialize theme overrides");

                    // 各色定義をリソースに登録する
                    foreach (var kv in overrides.BasicColors)
                    {
                        if (kv.Key.Equals("SystemAccentColor", StringComparison.Ordinal))
                            resDic["SystemAccentColor"] = kv.Value;
                        else
                            resDic[$"Color.{kv.Key}"] = kv.Value;
                    }

                    // コミットグラフのペン色を設定する
                    if (overrides.GraphColors.Count > 0)
                        Models.CommitGraph.SetPens(overrides.GraphColors, overrides.GraphPenThickness);
                    else
                        Models.CommitGraph.SetDefaultPens(overrides.GraphPenThickness);

                    app.Resources.MergedDictionaries.Add(resDic);
                    app._themeOverrides = resDic;
                }
                catch (Exception ex)
                {
                    Models.Logger.Log($"テーマオーバーライドの読み込み失敗: {ex.Message}", Models.LogLevel.Warning);
                }
            }
            else
            {
                Models.CommitGraph.SetDefaultPens();
            }
        }

        /// <summary>
        ///     テーマ名からAvaloniaのThemeVariant（Light/Dark/Default）に変換する。
        ///     Light系テーマ（Light, White）はThemeVariant.Lightに、
        ///     Dark系テーマ（Dark, OneDark）はThemeVariant.Darkにマッピングされる。
        /// </summary>
        public static ThemeVariant ParseThemeVariant(string theme)
        {
            if (string.IsNullOrEmpty(theme))
                return ThemeVariant.Default;
            if (theme.Equals("Light", StringComparison.OrdinalIgnoreCase))
                return ThemeVariant.Light;
            if (theme.Equals("Dark", StringComparison.OrdinalIgnoreCase) ||
                theme.Equals("OneDark", StringComparison.OrdinalIgnoreCase))
                return ThemeVariant.Dark;
            if (theme.Equals("White", StringComparison.OrdinalIgnoreCase))
                return ThemeVariant.Light;
            return ThemeVariant.Default;
        }

        public static void SetFonts(string defaultFont, string monospaceFont)
        {
            if (Current is not App app)
                return;

            if (app._fontsOverrides != null)
            {
                app.Resources.MergedDictionaries.Remove(app._fontsOverrides);
                app._fontsOverrides = null;
            }

            defaultFont = app.FixFontFamilyName(defaultFont);
            monospaceFont = app.FixFontFamilyName(monospaceFont);

            var resDic = new ResourceDictionary();
            if (!string.IsNullOrEmpty(defaultFont))
                resDic.Add("Fonts.Default", new FontFamily(defaultFont));

            if (string.IsNullOrEmpty(monospaceFont))
            {
                if (!string.IsNullOrEmpty(defaultFont))
                {
                    monospaceFont = $"fonts:Komorebi#JetBrains Mono,{defaultFont}";
                    resDic.Add("Fonts.Monospace", FontFamily.Parse(monospaceFont));
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(defaultFont) && !monospaceFont.Contains(defaultFont, StringComparison.Ordinal))
                    monospaceFont = $"{monospaceFont},{defaultFont}";

                resDic.Add("Fonts.Monospace", FontFamily.Parse(monospaceFont));
            }

            if (resDic.Count > 0)
            {
                app.Resources.MergedDictionaries.Add(resDic);
                app._fontsOverrides = resDic;
            }
        }

        public static async Task CopyTextAsync(string data)
        {
            if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow.Clipboard: { } clipboard })
                await clipboard.SetTextAsync(data ?? "");
        }

        public static async Task<string> GetClipboardTextAsync()
        {
            if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow.Clipboard: { } clipboard })
                return await clipboard.TryGetTextAsync();
            return null;
        }

        public static string Text(string key, params object[] args)
        {
            var fmt = Current?.FindResource($"Text.{key}") as string;
            if (string.IsNullOrWhiteSpace(fmt))
                return $"Text.{key}";

            if (args == null || args.Length == 0)
                return fmt;

            return string.Format(fmt, args);
        }

        public static Avalonia.Controls.Shapes.Path CreateMenuIcon(string key)
        {
            var icon = new Avalonia.Controls.Shapes.Path();
            icon.Width = 12;
            icon.Height = 12;
            icon.Stretch = Stretch.Uniform;

            if (Current?.FindResource(key) is StreamGeometry geo)
                icon.Data = geo;

            return icon;
        }

        public static ViewModels.Launcher GetLauncher()
        {
            return Current is App app ? app._launcher : null;
        }

        public static void Quit(int exitCode)
        {
            if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                desktop.MainWindow?.Close();
                desktop.Shutdown(exitCode);
            }
            else
            {
                Environment.Exit(exitCode);
            }
        }
        #endregion

        #region Overrides
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);

            var pref = ViewModels.Preferences.Instance;
            pref.PropertyChanged += (_, _) => pref.Save();

            SetLocale(pref.Locale);
            SetTheme(pref.Theme, pref.ThemeOverrides);
            SetFonts(pref.DefaultFontFamily, pref.MonospaceFontFamily);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                BindingPlugins.DataValidators.RemoveAt(0);

                // UIスレッドの未処理例外をクラッシュログに記録する
                Dispatcher.UIThread.UnhandledException += (_, e) =>
                {
                    Models.Logger.LogCrash(e.Exception, "Dispatcher.UIThread.UnhandledException");
                    e.Handled = true;
                };

                // Disable tooltip if window is not active.
                ToolTip.ToolTipOpeningEvent.AddClassHandler<Control>((c, e) =>
                {
                    var topLevel = TopLevel.GetTopLevel(c);
                    if (topLevel is not Window { IsActive: true })
                        e.Cancel = true;
                });

                if (TryLaunchAsFileHistoryViewer(desktop))
                    return;

                if (TryLaunchAsBlameViewer(desktop))
                    return;

                if (TryLaunchAsCoreEditor(desktop))
                    return;

                if (TryLaunchAsAskpass(desktop))
                    return;

                _ipcChannel = new Models.IpcChannel();
                if (!_ipcChannel.IsFirstInstance)
                {
                    var arg = desktop.Args is { Length: > 0 } ? desktop.Args[0].Trim() : string.Empty;
                    if (!string.IsNullOrEmpty(arg))
                    {
                        if (arg.StartsWith('"') && arg.EndsWith('"'))
                            arg = arg.Substring(1, arg.Length - 2).Trim();

                        if (arg.Length > 0 && !Path.IsPathFullyQualified(arg))
                            arg = Path.GetFullPath(arg);
                    }

                    _ipcChannel.SendToFirstInstance(arg);
                    Environment.Exit(0);
                }
                else
                {
                    _ipcChannel.MessageReceived += TryOpenRepository;
                    desktop.Exit += (_, _) => _ipcChannel.Dispose();
                    TryLaunchAsNormal(desktop);
                }
            }
        }
        #endregion

        private static bool TryLaunchAsRebaseTodoEditor(string[] args, out int exitCode)
        {
            exitCode = -1;

            if (args.Length <= 1 || !args[0].Equals("--rebase-todo-editor", StringComparison.Ordinal))
                return false;

            var file = args[1];
            var filename = Path.GetFileName(file);
            if (!filename.Equals("git-rebase-todo", StringComparison.OrdinalIgnoreCase))
                return true;

            var dirInfo = new DirectoryInfo(Path.GetDirectoryName(file)!);
            if (!dirInfo.Exists || !dirInfo.Name.Equals("rebase-merge", StringComparison.Ordinal))
                return true;

            var jobsFile = Path.Combine(dirInfo.Parent!.FullName, "komorebi.interactive_rebase");
            if (!File.Exists(jobsFile))
                return true;

            using var stream = File.OpenRead(jobsFile);
            var collection = JsonSerializer.Deserialize(stream, JsonCodeGen.Default.InteractiveRebaseJobCollection);
            using var writer = new StreamWriter(file);
            foreach (var job in collection.Jobs)
            {
                var code = job.Action switch
                {
                    Models.InteractiveRebaseAction.Pick => 'p',
                    Models.InteractiveRebaseAction.Edit => 'e',
                    Models.InteractiveRebaseAction.Reword => 'r',
                    Models.InteractiveRebaseAction.Squash => 's',
                    Models.InteractiveRebaseAction.Fixup => 'f',
                    _ => 'd'
                };
                writer.WriteLine($"{code} {job.SHA}");
            }

            writer.Flush();

            exitCode = 0;
            return true;
        }

        private static bool TryLaunchAsRebaseMessageEditor(string[] args, out int exitCode)
        {
            exitCode = -1;

            if (args.Length <= 1 || !args[0].Equals("--rebase-message-editor", StringComparison.Ordinal))
                return false;

            exitCode = 0;

            var file = args[1];
            var filename = Path.GetFileName(file);
            if (!filename.Equals("COMMIT_EDITMSG", StringComparison.OrdinalIgnoreCase))
                return true;

            var gitDir = Path.GetDirectoryName(file)!;
            var origHeadFile = Path.Combine(gitDir, "rebase-merge", "orig-head");
            var ontoFile = Path.Combine(gitDir, "rebase-merge", "onto");
            var doneFile = Path.Combine(gitDir, "rebase-merge", "done");
            var jobsFile = Path.Combine(gitDir, "komorebi.interactive_rebase");
            if (!File.Exists(ontoFile) || !File.Exists(origHeadFile) || !File.Exists(doneFile) || !File.Exists(jobsFile))
                return true;

            var origHead = File.ReadAllText(origHeadFile).Trim();
            var onto = File.ReadAllText(ontoFile).Trim();
            using var stream = File.OpenRead(jobsFile);
            var collection = JsonSerializer.Deserialize(stream, JsonCodeGen.Default.InteractiveRebaseJobCollection);
            if (!collection.Onto.Equals(onto) || !collection.OrigHead.Equals(origHead))
                return true;

            var done = File.ReadAllText(doneFile).Trim().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            if (done.Length == 0)
                return true;

            var current = done[^1].Trim();
            var match = REG_REBASE_TODO().Match(current);
            if (!match.Success)
                return true;

            var sha = match.Groups[1].Value;
            foreach (var job in collection.Jobs)
            {
                if (job.SHA.StartsWith(sha))
                {
                    File.WriteAllText(file, job.Message);
                    break;
                }
            }

            return true;
        }

        private bool TryLaunchAsFileHistoryViewer(IClassicDesktopStyleApplicationLifetime desktop)
        {
            var args = desktop.Args;
            if (args is not { Length: > 1 } || !args[0].Equals("--file-history", StringComparison.Ordinal))
                return false;

            var file = Path.GetFullPath(args[1]);
            var dir = Path.GetDirectoryName(file);

            var test = new Commands.QueryRepositoryRootPath(dir).GetResult();
            if (!test.IsSuccess || string.IsNullOrEmpty(test.StdOut))
            {
                Console.Out.WriteLine($"'{args[1]}' is not in a valid git repository");
                desktop.Shutdown(-1);
                return true;
            }

            var repo = test.StdOut.Trim();
            var relFile = Path.GetRelativePath(repo, file);
            var viewer = new Views.FileHistories()
            {
                DataContext = new ViewModels.FileHistories(repo, relFile)
            };
            desktop.MainWindow = viewer;
            return true;
        }

        private bool TryLaunchAsBlameViewer(IClassicDesktopStyleApplicationLifetime desktop)
        {
            var args = desktop.Args;
            if (args is not { Length: > 1 } || !args[0].Equals("--blame", StringComparison.Ordinal))
                return false;

            var file = Path.GetFullPath(args[1]);
            var dir = Path.GetDirectoryName(file);

            var test = new Commands.QueryRepositoryRootPath(dir).GetResult();
            if (!test.IsSuccess || string.IsNullOrEmpty(test.StdOut))
            {
                Console.Out.WriteLine($"'{args[1]}' is not in a valid git repository");
                desktop.Shutdown(-1);
                return true;
            }

            var repo = test.StdOut.Trim();
            var head = new Commands.QuerySingleCommit(repo, "HEAD").GetResult();
            if (head == null)
            {
                Console.Out.WriteLine($"{repo} has no commits!");
                desktop.Shutdown(-1);
                return true;
            }

            var relFile = Path.GetRelativePath(repo, file);
            var viewer = new Views.Blame()
            {
                DataContext = new ViewModels.Blame(repo, relFile, head)
            };
            desktop.MainWindow = viewer;
            return true;
        }

        /// <summary>
        ///     gitのcore.editorとして起動された場合の処理。
        ///     対話的リベースなどでgitがコミットメッセージ編集を要求する際に呼ばれる。
        /// </summary>
        /// <returns>core.editorモードとして起動された場合はtrue</returns>
        private bool TryLaunchAsCoreEditor(IClassicDesktopStyleApplicationLifetime desktop)
        {
            var args = desktop.Args;
            if (args is not { Length: > 1 } || !args[0].Equals("--core-editor", StringComparison.Ordinal))
                return false;

            // 編集対象ファイルが存在しない場合はエラー終了する
            var file = args[1];
            if (!File.Exists(file))
            {
                desktop.Shutdown(-1);
                return true;
            }

            // コミットメッセージエディタをスタンドアロンモードで起動する
            var editor = new Views.CommitMessageEditor();
            editor.AsStandalone(file);
            desktop.MainWindow = editor;
            return true;
        }

        /// <summary>
        ///     gitのaskpass（認証情報入力）として起動された場合の処理。
        ///     環境変数 SOURCEGIT_LAUNCH_AS_ASKPASS=TRUE が設定されている場合に有効。
        /// </summary>
        /// <returns>askpassモードとして起動された場合はtrue</returns>
        private bool TryLaunchAsAskpass(IClassicDesktopStyleApplicationLifetime desktop)
        {
            var launchAsAskpass = Environment.GetEnvironmentVariable("SOURCEGIT_LAUNCH_AS_ASKPASS");
            if (launchAsAskpass is not "TRUE")
                return false;

            var args = desktop.Args;
            if (args?.Length > 0)
            {
                var askpass = new Views.Askpass();
                askpass.TxtDescription.Text = args[0];
                desktop.MainWindow = askpass;
                return true;
            }

            return false;
        }

        /// <summary>
        ///     通常モード（メインGUI）としてアプリケーションを起動する。
        ///     外部ツールの初期化、ランチャーウィンドウの生成、初回起動セットアップ、
        ///     および起動時の更新チェックを行う。
        /// </summary>
        private void TryLaunchAsNormal(IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 外部ツール（diffツール等）の検出とアバター取得を開始する
            Native.OS.SetupExternalTools();
            Models.AvatarManager.Instance.Start();

            // 起動引数にリポジトリパスが指定されていれば初期表示に使用する
            string startupRepo = null;
            if (desktop.Args is { Length: 1 } && Directory.Exists(desktop.Args[0]))
                startupRepo = desktop.Args[0];

            // 設定を読み込み、ランチャーウィンドウを生成・表示する
            var pref = ViewModels.Preferences.Instance;
            pref.SetCanModify();

            _launcher = new ViewModels.Launcher(startupRepo);
            desktop.MainWindow = new Views.Launcher() { DataContext = _launcher };
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

            // 初回起動時（デフォルトクローンディレクトリ未設定）はセットアップ画面を表示する
            if (string.IsNullOrEmpty(pref.GitDefaultCloneDir))
            {
                SetLocale(ViewModels.Preferences.DetectedLocale);
                _launcher.ActivePage.Popup = new ViewModels.InitSetup();
            }

            // 起動時の自動更新チェック（1日1回、更新がある場合のみダイアログ表示）
#if !DISABLE_UPDATE_DETECTION
            if (pref.ShouldCheck4UpdateOnStartup())
                Check4Update(false);
#endif
        }

        /// <summary>
        ///     指定されたパスのリポジトリをタブで開く。
        ///     既にアプリが起動中のとき、二重起動防止の仕組みから呼ばれる。
        /// </summary>
        private void TryOpenRepository(string repo)
        {
            if (!string.IsNullOrEmpty(repo) && Directory.Exists(repo))
            {
                // gitリポジトリのルートパスを取得する
                var test = new Commands.QueryRepositoryRootPath(repo).GetResult();
                if (test.IsSuccess && !string.IsNullOrEmpty(test.StdOut))
                {
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        // リポジトリノードを検索または追加し、タブで開く
                        var node = ViewModels.Preferences.Instance.FindOrAddNodeByRepositoryPath(test.StdOut.Trim(), null, false);
                        ViewModels.Welcome.Instance.Refresh();
                        _launcher?.OpenRepositoryInTab(node, null);

                        // ウィンドウを最前面に移動する
                        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: Views.Launcher wnd })
                            wnd.BringToTop();
                    });

                    return;
                }
            }

            // リポジトリとして開けなかった場合でもウィンドウを最前面に移動する
            Dispatcher.UIThread.Invoke(() =>
            {
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: Views.Launcher launcher })
                    launcher.BringToTop();
            });
        }

        /// <summary>
        ///     GitHubリリースから最新バージョンを確認し、更新がある場合はダイアログを表示する。
        ///     バックグラウンドスレッドで非同期実行される。
        /// </summary>
        /// <param name="manually">
        ///     true: 手動チェック（結果を常に表示、無視タグをスキップ）
        ///     false: 自動チェック（更新がある場合のみ表示、無視タグを確認）
        /// </param>
        private void Check4Update(bool manually = false)
        {
            Task.Run(async () =>
            {
                try
                {
                    // VelopackのUpdateManagerを初期化する
                    var source = new GithubSource("https://github.com/1llum1n4t1s/Komorebi", string.Empty, false);
                    var mgr = new UpdateManager(source);

                    // Velopackでインストールされていない場合（開発環境等）はチェックをスキップする
                    if (!mgr.IsInstalled)
                    {
                        if (manually)
                            ShowSelfUpdateResult(new Models.AlreadyUpToDate());
                        return;
                    }

                    // 最新バージョンの有無を確認する
                    var newVersion = await mgr.CheckForUpdatesAsync();
                    if (newVersion == null)
                    {
                        if (manually)
                            ShowSelfUpdateResult(new Models.AlreadyUpToDate());
                        return;
                    }

                    // 自動チェック時はユーザーが無視指定したバージョンをスキップする
                    if (!manually)
                    {
                        var pref = ViewModels.Preferences.Instance;
                        var newTag = $"v{newVersion.TargetFullRelease.Version}";
                        if (newTag == pref.IgnoreUpdateTag)
                            return;
                    }

                    // 更新ダイアログを表示する
                    ShowSelfUpdateResult(new Models.VelopackUpdate(mgr, newVersion));
                }
                catch (Exception e)
                {
                    Models.Logger.LogException("更新チェック失敗", e);

                    // 手動チェック時のみエラーを表示する
                    if (manually)
                        ShowSelfUpdateResult(new Models.SelfUpdateFailed(e));
                }
            });
        }

        /// <summary>
        ///     更新チェック結果をUIスレッドでダイアログ表示する。
        /// </summary>
        /// <param name="data">表示データ（VelopackUpdate / AlreadyUpToDate / SelfUpdateFailed）</param>
        private void ShowSelfUpdateResult(object data)
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                try
                {
                    await ShowDialog(new ViewModels.SelfUpdate { Data = data });
                }
                catch (Exception ex)
                {
                    Models.Logger.Log($"更新結果ダイアログ表示失敗: {ex.Message}", Models.LogLevel.Warning);
                }
            });
        }

        private string FixFontFamilyName(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            var parts = input.Split(',');
            var trimmed = new List<string>();

            foreach (var part in parts)
            {
                var t = part.Trim();
                if (string.IsNullOrEmpty(t))
                    continue;

                var sb = new StringBuilder();
                var prevChar = '\0';

                foreach (var c in t)
                {
                    if (c == ' ' && prevChar == ' ')
                        continue;
                    sb.Append(c);
                    prevChar = c;
                }

                var name = sb.ToString();
                var added = false;

                try
                {
                    var fontFamily = FontFamily.Parse(name);
                    if (fontFamily.FamilyTypefaces.Count > 0)
                    {
                        trimmed.Add(name);
                        added = true;
                    }
                }
                catch
                {
                    // Ignore exceptions.
                }

                // Fallback: try as bundled font with fonts:Komorebi# prefix
                if (!added && !name.StartsWith("fonts:", StringComparison.Ordinal))
                {
                    try
                    {
                        var bundledName = $"fonts:Komorebi#{name}";
                        var fontFamily = FontFamily.Parse(bundledName);
                        if (fontFamily.FamilyTypefaces.Count > 0)
                            trimmed.Add(bundledName);
                    }
                    catch
                    {
                        // Ignore exceptions.
                    }
                }
            }

            return trimmed.Count > 0 ? string.Join(',', trimmed) : string.Empty;
        }

        [GeneratedRegex(@"^[a-z]+\s+([a-fA-F0-9]{4,40})(\s+.*)?$")]
        private static partial Regex REG_REBASE_TODO();

        private Models.IpcChannel _ipcChannel = null;
        private ViewModels.Launcher _launcher = null;
        private ResourceDictionary _activeLocale = null;
        private ResourceDictionary _themeColorOverrides = null;
        private ResourceDictionary _themeOverrides = null;
        private ResourceDictionary _fontsOverrides = null;
    }
}
