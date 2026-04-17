using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

using Velopack;
using Velopack.Sources;

namespace Komorebi;

public partial class App : Application
{
    #region App Entry Point
    /// <summary>
    /// アプリケーションのエントリーポイント。
    /// Velopack初期化 → ログ初期化 → 起動モード判定（リベースエディタ or 通常GUI）の順に処理する。
    /// 未処理例外のクラッシュログ記録も設定する。
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {
        // Velopackの自動更新フックを最初に実行する（更新適用後の再起動処理等）
        VelopackApp.Build().Run();

        // アプリケーションデータディレクトリ（設定・ログ保存先）を初期化する
        Native.OS.SetupDataDir();

        // ログシステムを初期化し、起動ログを記録する
        Models.Logger.Initialize(new Models.LoggerConfig
        {
            LogDirectory = Path.Combine(Native.OS.DataDir, "logs"),
            FilePrefix = "Komorebi",
        });
        Models.Logger.LogStartup();

        // AppDomain全体の未処理例外をクラッシュログに記録するハンドラを登録する。
        // CLRはこのハンドラ復帰直後にプロセスを強制終了するため、非同期ログバッファが
        // ディスクにフラッシュされる保証がない。Logger.Dispose() で同期フラッシュを強制する。
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Models.Logger.LogCrash(e.ExceptionObject as Exception, "AppDomain.UnhandledException");
            Models.Logger.Dispose();
        };

        // Task内の未観測例外をクラッシュログに記録し、プロセス終了を防ぐ
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Models.Logger.LogCrash(e.Exception, "UnobservedTaskException");
            e.SetObserved();
        };

        try
        {
            // 起動モードを判定する: リベースTodoエディタ → リベースメッセージエディタ → 通常GUI
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

        // macOS: メインループ正常終了後もexit()時にC++デストラクタ
        // (ComPtr<IAvnDispatcher>)が.NETランタイムへの逆P/Invokeで
        // abort()するため、プロセスを強制終了して回避する。
        if (OperatingSystem.IsMacOS())
            Process.GetCurrentProcess().Kill();
    }

    /// <summary>
    /// Avaloniaアプリケーションビルダーを構成して返す。
    /// プラットフォーム検出、フォント設定（Interをデフォルト、JetBrains Monoを等幅）、
    /// OS固有の設定を適用する。
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>();
        builder.UsePlatformDetect();        // 実行環境のOS・レンダラーを自動検出する
        builder.LogToTrace();               // ログ出力をSystem.Diagnostics.Traceに転送する
        builder.WithInterFont();            // Inter フォントを組み込みフォントとして登録する
        builder.With(new FontManagerOptions()
        {
            DefaultFamilyName = "fonts:Inter#Inter"  // アプリ全体のデフォルトフォントをInterに設定する
        });
        // 旧実装ではアプリ内蔵フォント（fonts:Komorebi）を EmbeddedFontCollection として
        // 登録していたが、バンドルフォントを全廃した後はリソースが空のため何も提供できない。
        // 残すと `fonts:Komorebi#X` を参照したコードが実行時に GlyphTypeface 生成で例外を
        // 投げて Render ループを破壊する原因となるため、登録自体を削除する。

        // OS固有のウィンドウ装飾やIME設定等を適用する
        Native.OS.SetupApp(builder);
        return builder;
    }

    /// <summary>
    /// 後方互換性のための例外ログ出力（内部でSuperLightLoggerロガーに委譲する）
    /// </summary>
    public static void LogException(Exception ex, string context = null)
    {
        Models.Logger.LogCrash(ex, context);
    }
    #endregion

    #region Utility Functions
    /// <summary>
    /// ViewModelオブジェクトに対応するViewコントロールを名前規約で自動解決して生成する。
    /// 名前空間の ".ViewModels." を ".Views." に置換して対応するViewの型を探す。
    /// </summary>
    /// <param name="data">対応するViewを生成したいViewModelオブジェクト</param>
    /// <returns>生成されたViewコントロール。対応するViewが見つからない場合はnull</returns>
    public static Control CreateViewForViewModel(object data)
    {
        // ViewModelの完全修飾名を取得し、".ViewModels."を含むか確認する
        var dataTypeName = data.GetType().FullName;
        if (string.IsNullOrEmpty(dataTypeName) || !dataTypeName.Contains(".ViewModels.", StringComparison.Ordinal))
            return null;

        // 名前空間の ".ViewModels." → ".Views." 置換でView型名を導出する
        var viewTypeName = dataTypeName.Replace(".ViewModels.", ".Views.");
        var viewType = Type.GetType(viewTypeName);
        if (viewType is not null)
            return Activator.CreateInstance(viewType) as Control;

        return null;
    }

    /// <summary>
    /// ViewModelまたはウィンドウをモーダルダイアログとして表示する。
    /// ViewModelが渡された場合は名前規約でViewを自動解決する。
    /// ownerが省略された場合はメインウィンドウを親ウィンドウとする。
    /// </summary>
    /// <param name="data">表示するViewModelまたはChromelessWindowインスタンス</param>
    /// <param name="owner">親ウィンドウ（省略時はメインウィンドウ）</param>
    /// <returns>ダイアログが閉じるまで待機するTask</returns>
    public static Task ShowDialog(object data, Window owner = null)
    {
        // 親ウィンドウが指定されていなければメインウィンドウを使用する
        if (owner is null)
        {
            if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
                owner = mainWindow;
            else
                return null;
        }

        // 既にChromelessWindowインスタンスならそのまま表示する
        if (data is Views.ChromelessWindow window)
            return window.ShowDialog(owner);

        // ViewModelから名前規約でViewを自動生成し、DataContextを設定して表示する
        window = CreateViewForViewModel(data) as Views.ChromelessWindow;
        if (window is not null)
        {
            window.DataContext = data;
            return window.ShowDialog(owner);
        }

        return null;
    }

    /// <summary>
    /// ViewModelまたはウィンドウを非モーダル（独立）ウィンドウとして表示する。
    /// 現在アクティブなウィンドウが存在するスクリーンの中央に配置する。
    /// </summary>
    /// <param name="data">表示するViewModelまたはChromelessWindowインスタンス</param>
    public static void ShowWindow(object data)
    {
        // ViewModelからViewを自動解決する（既にウィンドウならそのまま使用する）
        if (data is not Views.ChromelessWindow window)
        {
            window = CreateViewForViewModel(data) as Views.ChromelessWindow;
            if (window is null)
                return;

            window.DataContext = data;
        }

        // do-whileブロックでウィンドウ位置の計算を行う（breakで中断可能にするため）
        do
        {
            if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { Windows: { Count: > 0 } windows })
            {
                // 現在アクティブなウィンドウを探す（見つからなければ先頭ウィンドウを使用する）
                var actived = windows[0];
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

                // アクティブウィンドウが表示されているスクリーンを取得する
                var screen = actived.Screens.ScreenFromWindow(actived) ?? actived.Screens.Primary;
                if (screen is null)
                    break;

                // 対象ウィンドウをスクリーンの作業領域中央に配置する座標を計算する
                var rect = new PixelRect(PixelSize.FromSize(window.ClientSize, actived.DesktopScaling));
                var centeredRect = screen.WorkingArea.CenterRect(rect);
                if (actived.Screens.ScreenFromPoint(centeredRect.Position) is null)
                    break;

                // 計算した位置を手動指定モードで適用する
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Position = centeredRect.Position;
            }
        } while (false);

        window.Show();
    }

    /// <summary>
    /// 確認ダイアログをモーダル表示し、ユーザーの応答（OK/キャンセル等）を待つ。
    /// </summary>
    /// <param name="message">表示するメッセージ</param>
    /// <param name="buttonType">ボタンの種類（OkCancel等）</param>
    /// <returns>ユーザーがOKを押した場合はtrue</returns>
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

    /// <summary>
    /// 空コミット確認ダイアログを表示する。
    /// ステージに変更がない状態でコミットしようとした際に呼ばれ、
    /// 「全てステージしてコミット」「選択をステージしてコミット」「空コミット」等の選択肢を提示する。
    /// </summary>
    /// <param name="hasLocalChanges">ワーキングツリーにローカル変更があるか</param>
    /// <param name="hasSelectedUnstaged">未ステージの選択ファイルがあるか</param>
    /// <returns>ユーザーの選択結果</returns>
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

    /// <summary>
    /// gitコマンドのエラーをUIに通知する。
    /// エラーメッセージからヒント（対処法）を自動検索し、あれば併せて表示する。
    /// </summary>
    /// <param name="context">エラーが発生したコンテキスト（リポジトリID等）</param>
    /// <param name="message">gitから返されたエラーメッセージ</param>
    public static void RaiseException(string context, string message)
    {
        if (Current is App { _launcher: not null } app)
        {
            // エラーメッセージからヒントキーを検索し、ローカライズされたヒント文字列を取得する
            var hintKey = Models.GitErrorHelper.GetHintKey(message);
            var hint = string.IsNullOrEmpty(hintKey) ? string.Empty : app.FindLocaleString(hintKey);

            string actionLabel = null;
            Action actionCallback = null;

            // SSH秘密鍵パーミッションエラーの場合、自動修正アクションを提供する
            if (hintKey == "Text.GitError.KeyPermissionTooOpen" && !OperatingSystem.IsWindows())
            {
                var keyPath = Models.GitErrorHelper.ExtractKeyPathFromPermissionError(message);
                if (!string.IsNullOrEmpty(keyPath))
                {
                    actionLabel = app.FindLocaleString("Text.GitError.KeyPermissionTooOpen.Fix");
                    actionCallback = () =>
                    {
                        try
                        {
                            var psi = new System.Diagnostics.ProcessStartInfo("chmod", $"600 \"{keyPath}\"")
                            {
                                UseShellExecute = false,
                                CreateNoWindow = true,
                            };
                            System.Diagnostics.Process.Start(psi)?.WaitForExit(5000);
                            app._launcher.DispatchNotification(context, $"Fixed permissions for '{keyPath}'", false);
                        }
                        catch (Exception e)
                        {
                            app._launcher.DispatchNotification(context, e.Message, true);
                        }
                    };
                }
            }

            app._launcher.DispatchNotification(context, message, true, hint, actionLabel, actionCallback);
        }
    }

    /// <summary>
    /// 情報通知をUIに送信する（エラーではない通常のメッセージ）。
    /// </summary>
    /// <param name="context">通知のコンテキスト（リポジトリID等）</param>
    /// <param name="message">表示するメッセージ</param>
    public static void SendNotification(string context, string message)
    {
        if (Current is App { _launcher: not null } app)
            app._launcher.DispatchNotification(context, message, false);
    }

    /// <summary>
    /// 現在のロケールリソースから指定キーの文字列を取得する。
    /// キーが見つからない場合は空文字列を返す。
    /// </summary>
    private string FindLocaleString(string key)
    {
        if (Resources.TryGetResource(key, null, out var value) && value is string str)
            return str;
        return string.Empty;
    }

    /// <summary>
    /// アプリケーションのロケール（表示言語）を切り替える。
    /// App.axamlに登録されたResourceDictionaryをキーで検索し、
    /// 現在のロケールと置き換える。
    /// </summary>
    /// <param name="localeKey">ロケールキー（例: "ja_JP", "en_US"）</param>
    public static void SetLocale(string localeKey)
    {
        if (Current is not App app ||
            app.Resources[localeKey] is not ResourceDictionary targetLocale ||
            targetLocale == app._activeLocale)
            return;

        // 現在適用中のロケールリソースを除去する
        if (app._activeLocale is not null)
            app.Resources.MergedDictionaries.Remove(app._activeLocale);

        // 新しいロケールリソースを追加して保持する
        app.Resources.MergedDictionaries.Add(targetLocale);
        app._activeLocale = targetLocale;
    }

    /// <summary>
    /// アプリケーションのテーマを設定する。
    /// ベーステーマ（Light/Dark）を適用した上で、White/OneDark等のテーマ固有の色を上書きする。
    /// さらにユーザー定義のテーマオーバーライド（JSONファイル）があれば最後に適用する。
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
        if (app._themeColorOverrides is not null)
        {
            app.Resources.MergedDictionaries.Remove(app._themeColorOverrides);
            app._themeColorOverrides = null;
        }

        // Whiteテーマ: Lightベースに白基調の色を上書きする
        if (theme.Equals("White", StringComparison.OrdinalIgnoreCase))
        {
            var resDic = new ResourceDictionary
            {
                ["Color.Window"] = Color.Parse("#FFFFFFFF"),
                ["Color.WindowBorder"] = Color.Parse("#FFB0B0B0"),
                ["Color.TitleBar"] = Color.Parse("#FFF0F0F0"),
                ["Color.ToolBar"] = Color.Parse("#FFF8F8F8"),
                ["Color.Popup"] = Color.Parse("#FFFFFFFF"),
                ["Color.Contents"] = Color.Parse("#FFFFFFFF"),
                ["Color.Badge"] = Color.Parse("#FFE0E0E0"),
                ["Color.Border0"] = Color.Parse("#FFE0E0E0"),
                ["Color.Border1"] = Color.Parse("#FFA0A0A0"),
                ["Color.Border2"] = Color.Parse("#FFE0E0E0"),
                ["Color.FlatButton.Background"] = Color.Parse("#FFFFFFFF"),
                ["Color.FlatButton.BackgroundHovered"] = Color.Parse("#FFF0F0F0"),
                ["Color.FlatButton.FloatingBorder"] = Color.Parse("#FFA0A0A0"),
                ["Color.InlineCode"] = Color.Parse("#FFF0F0F0"),
                ["Color.DataGridHeaderBG"] = Color.Parse("#FFF5F5F5"),
            };
            app.Resources.MergedDictionaries.Add(resDic);
            app._themeColorOverrides = resDic;
        }
        // OneDarkテーマ: Darkベースに Atom One Dark 風の配色を上書きする
        else if (theme.Equals("OneDark", StringComparison.OrdinalIgnoreCase))
        {
            var resDic = new ResourceDictionary
            {
                // ウィンドウ・レイアウト系
                ["Color.Window"] = Color.Parse("#FF282C34"),
                ["Color.WindowBorder"] = Color.Parse("#FF4B5263"),
                ["Color.TitleBar"] = Color.Parse("#FF21252B"),
                ["Color.ToolBar"] = Color.Parse("#FF2C313A"),
                ["Color.Popup"] = Color.Parse("#FF2C313A"),
                ["Color.Contents"] = Color.Parse("#FF21252B"),

                // バッジ・コンフリクト系
                ["Color.Badge"] = Color.Parse("#FF4B5263"),
                ["Color.BadgeFG"] = Color.Parse("#FFABB2BF"),
                ["Color.Conflict"] = Color.Parse("#FFE5C07B"),
                ["Color.Conflict.Foreground"] = Color.Parse("#FF282C34"),

                // ボーダー系
                ["Color.Border0"] = Color.Parse("#FF1E2127"),
                ["Color.Border1"] = Color.Parse("#FF5C6370"),
                ["Color.Border2"] = Color.Parse("#FF3E4451"),

                // フラットボタン系
                ["Color.FlatButton.Background"] = Color.Parse("#FF2C313A"),
                ["Color.FlatButton.BackgroundHovered"] = Color.Parse("#FF3E4451"),
                ["Color.FlatButton.FloatingBorder"] = Color.Parse("#FF4B5263"),

                // テキスト・前景色
                ["Color.FG1"] = Color.Parse("#FFABB2BF"),
                ["Color.FG2"] = Color.Parse("#FF828997"),

                // Diff表示色（緑=#98C379ベース、赤=#E06C75ベース、シアン=#56B6C2）
                ["Color.Diff.EmptyBG"] = Color.Parse("#3C000000"),
                ["Color.Diff.AddedBG"] = Color.Parse("#C02D4A33"),
                ["Color.Diff.DeletedBG"] = Color.Parse("#C04D2C30"),
                ["Color.Diff.AddedHighlight"] = Color.Parse("#A0365C3B"),
                ["Color.Diff.DeletedHighlight"] = Color.Parse("#A06D3035"),
                ["Color.Diff.BlockBorderHighlight"] = Color.Parse("#FF56B6C2"),

                // リンク・インラインコード・ヘッダー
                ["Color.Link"] = Color.Parse("#FF61AFEF"),
                ["Color.InlineCode"] = Color.Parse("#FF3E4451"),
                ["Color.InlineCodeFG"] = Color.Parse("#FFABB2BF"),
                ["Color.DataGridHeaderBG"] = Color.Parse("#FF2C313A"),
            };

            app.Resources.MergedDictionaries.Add(resDic);
            app._themeColorOverrides = resDic;
        }

        // 前回適用したユーザー定義オーバーライドを除去する
        if (app._themeOverrides is not null)
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
                var overrides = JsonSerializer.Deserialize(stream, JsonCodeGen.Default.ThemeOverrides)
                    ?? throw new JsonException("Failed to deserialize theme overrides");

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
    /// テーマ名からAvaloniaのThemeVariant（Light/Dark/Default）に変換する。
    /// Light系テーマ（Light, White）はThemeVariant.Lightに、
    /// Dark系テーマ（Dark, OneDark）はThemeVariant.Darkにマッピングされる。
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

    /// <summary>
    /// アプリケーションのデフォルトフォントと等幅フォントを設定する。
    /// フォント名のバリデーション・正規化を行い、リソースディクショナリに登録する。
    /// 等幅フォントが未指定の場合はJetBrains Monoをベースにフォールバックを構成する。
    /// </summary>
    /// <param name="defaultFont">UIデフォルトフォント名（カンマ区切りで複数指定可）</param>
    /// <param name="monospaceFont">等幅フォント名（diff/blame表示用、カンマ区切りで複数指定可）</param>
    public static void SetFonts(string defaultFont, string monospaceFont)
    {
        if (Current is not App app)
            return;

        // 前回適用したフォントオーバーライドを除去する
        if (app._fontsOverrides is not null)
        {
            app.Resources.MergedDictionaries.Remove(app._fontsOverrides);
            app._fontsOverrides = null;
        }

        // フォント名を正規化する（余分な空白除去、存在確認、バンドルフォント解決）
        defaultFont = FixFontFamilyName(defaultFont);
        monospaceFont = FixFontFamilyName(monospaceFont);

        var resDic = new ResourceDictionary();

        // デフォルトフォントが指定されていればリソースに登録する
        if (!string.IsNullOrEmpty(defaultFont))
            resDic.Add("Fonts.Default", new FontFamily(defaultFont));

        if (!string.IsNullOrEmpty(monospaceFont))
        {
            // 等幅フォント指定あり: デフォルトフォントをフォールバックに追加する（未含時のみ）
            if (!string.IsNullOrEmpty(defaultFont) && !monospaceFont.Contains(defaultFont, StringComparison.Ordinal))
                monospaceFont = $"{monospaceFont},{defaultFont}";

            resDic.Add("Fonts.Monospace", FontFamily.Parse(monospaceFont));
        }

        // フォント設定が1つ以上あればリソースディクショナリとして登録する
        if (resDic.Count > 0)
        {
            app.Resources.MergedDictionaries.Add(resDic);
            app._fontsOverrides = resDic;
        }
    }

    /// <summary>
    /// テキストをシステムクリップボードにコピーする。
    /// </summary>
    /// <param name="data">コピーするテキスト（nullの場合は空文字列をセットする）</param>
    public static async Task CopyTextAsync(string data)
    {
        if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow.Clipboard: { } clipboard })
            await clipboard.SetTextAsync(data ?? "");
    }

    /// <summary>
    /// システムクリップボードからテキストを取得する。
    /// </summary>
    /// <returns>クリップボードのテキスト。取得できない場合はnull</returns>
    public static async Task<string> GetClipboardTextAsync()
    {
        if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow.Clipboard: { } clipboard })
            return await clipboard.TryGetTextAsync();
        return null;
    }

    /// <summary>
    /// ローカライズされた文字列をキーで取得する。
    /// キーには自動的に "Text." プレフィックスが付与される。
    /// argsが指定された場合はstring.Formatでプレースホルダーを置換する。
    /// </summary>
    /// <param name="key">ロケールリソースのキー（"Text."プレフィックスなし）</param>
    /// <param name="args">書式文字列の引数（{0}, {1}等の置換用）</param>
    /// <returns>ローカライズ済み文字列。キーが見つからない場合は "Text.{key}" をそのまま返す</returns>
    public static string Text(string key, params object[] args)
    {
        var fmt = Current?.FindResource($"Text.{key}") as string;
        if (string.IsNullOrWhiteSpace(fmt))
            return $"Text.{key}";

        if (args is null || args.Length == 0)
            return fmt;

        return string.Format(fmt, args);
    }

    /// <summary>
    /// リソースキーからStreamGeometryアイコンを取得し、メニュー用のPathコントロールを生成する。
    /// 12x12サイズでUniformストレッチに設定される。
    /// </summary>
    /// <param name="key">アイコンリソースのキー名</param>
    /// <returns>アイコンを表示するPathコントロール</returns>
    public static Avalonia.Controls.Shapes.Path CreateMenuIcon(string key)
    {
        var icon = new Avalonia.Controls.Shapes.Path
        {
            Width = 12,
            Height = 12,
            Stretch = Stretch.Uniform,
        };

        if (Current?.FindResource(key) is StreamGeometry geo)
            icon.Data = geo;

        return icon;
    }

    /// <summary>
    /// ワークスペース切替メニューを構築して表示する共有ヘルパー。
    /// Repository.axaml.cs と WelcomeToolbar.axaml.cs から呼び出される。
    /// </summary>
    /// <param name="anchor">メニューを開くボタン</param>
    /// <param name="placement">メニューの配置方向</param>
    /// <param name="onWorkspaceSwitched">ワークスペース切替後のコールバック（表示更新用）</param>
    public static void OpenWorkspaceMenu(Avalonia.Controls.Button anchor, Avalonia.Controls.PlacementMode placement, Action onWorkspaceSwitched)
    {
        if (GetLauncher() is not { } launcher)
            return;

        var pref = ViewModels.Preferences.Instance;
        var menu = new Avalonia.Controls.ContextMenu();
        menu.Placement = placement;

        var groupHeader = new Avalonia.Controls.TextBlock()
        {
            Text = Text("Launcher.Workspaces"),
            FontWeight = Avalonia.Media.FontWeight.Bold,
        };

        var workspaces = new Avalonia.Controls.MenuItem();
        workspaces.Header = groupHeader;
        workspaces.IsEnabled = false;
        menu.Items.Add(workspaces);

        for (var i = 0; i < pref.Workspaces.Count; i++)
        {
            var workspace = pref.Workspaces[i];
            var icon = CreateMenuIcon(workspace.IsActive ? "Icons.Check" : "Icons.Workspace");
            icon.Fill = workspace.Brush;

            var item = new Avalonia.Controls.MenuItem();
            item.Header = workspace.Name;
            item.Icon = icon;
            item.Click += (_, ev) =>
            {
                if (!workspace.IsActive)
                {
                    launcher.CommandPalette = null;
                    launcher.SwitchWorkspace(workspace);
                    onWorkspaceSwitched?.Invoke();
                }
                ev.Handled = true;
            };

            menu.Items.Add(item);
        }

        menu.Items.Add(new Avalonia.Controls.MenuItem() { Header = "-" });

        var configure = new Avalonia.Controls.MenuItem();
        configure.Header = Text("Workspace.Configure");
        configure.Click += async (_, ev) =>
        {
            await ShowDialog(new ViewModels.ConfigureWorkspace());
            onWorkspaceSwitched?.Invoke();
            ev.Handled = true;
        };
        menu.Items.Add(configure);
        menu.Open(anchor);
    }

    /// <summary>
    /// 現在のLauncherビューモデルインスタンスを取得する。
    /// アプリケーションが初期化されていない場合はnullを返す。
    /// </summary>
    public static ViewModels.Launcher GetLauncher()
    {
        return Current is App app ? app._launcher : null;
    }

    /// <summary>
    /// アプリケーションを指定の終了コードで終了する。
    /// デスクトップライフタイムが利用可能な場合はメインウィンドウを閉じてから
    /// シャットダウンし、そうでなければEnvironment.Exitで強制終了する。
    /// </summary>
    /// <param name="exitCode">プロセスの終了コード</param>
    public static void Quit(int exitCode)
    {
        // バックグラウンドのアバターダウンロードを停止して、IOException を防止
        Models.AvatarManager.Instance.Stop();

        if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 明示的シャットダウンモードに切り替えてメインウィンドウを閉じる
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
    /// <summary>
    /// Avaloniaフレームワーク初期化時に呼ばれる。
    /// AXAMLリソースのロード、設定の自動保存登録、ロケール・テーマ・フォントの初期適用を行う。
    /// </summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // 設定変更時に自動保存するハンドラを登録する
        var pref = ViewModels.Preferences.Instance;
        pref.PropertyChanged += (_, _) => pref.Save();

        // ユーザー設定に基づいてロケール・テーマ・フォントを初期適用する
        SetLocale(pref.Locale);
        SetTheme(pref.Theme, pref.ThemeOverrides);
        SetFonts(pref.DefaultFontFamily, pref.MonospaceFontFamily);
    }

    /// <summary>
    /// フレームワーク初期化完了後に呼ばれる。
    /// 起動モードの判定（ファイル履歴ビューア、blameビューア、コアエディタ、askpass、通常GUI）と
    /// IPC二重起動防止の初期化を行う。
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
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
                        arg = arg[1..^1].Trim();

                    if (arg.Length > 0 && !Path.IsPathFullyQualified(arg))
                        arg = Path.GetFullPath(arg);
                }

                _ipcChannel.SendToFirstInstance(arg);
                Environment.Exit(0);
            }
            else
            {
                _ipcChannel.MessageReceived += TryOpenRepository;
                desktop.Exit += (_, _) =>
                {
                    _ipcChannel.Dispose();

                    // macOS: [NSApplication terminate:]がexit()を呼ぶとC++グローバルデストラクタ
                    // (ComPtr<IAvnDispatcher>::~ComPtr)が実行され、シャットダウン中の.NETランタイムへ
                    // 逆P/Invokeを試みてabort()でクラッシュする。
                    // Exit イベント時点（exit()の前）でプロセスを強制終了して回避する。
                    if (OperatingSystem.IsMacOS())
                        Process.GetCurrentProcess().Kill();
                };
                TryLaunchAsNormal(desktop);
            }
        }
    }
    #endregion

    /// <summary>
    /// 対話的リベースのTodoエディタモードとして起動を試みる。
    /// gitが--rebase-todo-editorオプション付きで呼び出した場合に、
    /// komorebi.interactive_rebaseファイルのジョブリストをgit-rebase-todoに書き出す。
    /// </summary>
    /// <param name="args">コマンドライン引数</param>
    /// <param name="exitCode">エディタモードのプロセス終了コード</param>
    /// <returns>リベースTodoエディタとして起動された場合はtrue</returns>
    private static bool TryLaunchAsRebaseTodoEditor(string[] args, out int exitCode)
    {
        exitCode = -1;

        // --rebase-todo-editor引数が無ければ通常起動に移行する
        if (args.Length <= 1 || !args[0].Equals("--rebase-todo-editor", StringComparison.Ordinal))
            return false;

        // 編集対象がgit-rebase-todoファイルであることを確認する
        var file = args[1];
        var filename = Path.GetFileName(file);
        if (!filename.Equals("git-rebase-todo", StringComparison.OrdinalIgnoreCase))
            return true;

        // rebase-mergeディレクトリ内のファイルであることを確認する
        var dirInfo = new DirectoryInfo(Path.GetDirectoryName(file)!);
        if (!dirInfo.Exists || !dirInfo.Name.Equals("rebase-merge", StringComparison.Ordinal))
            return true;

        // Komorebiが生成した対話的リベースジョブファイルが存在するか確認する
        var jobsFile = Path.Combine(dirInfo.Parent!.FullName, "komorebi.interactive_rebase");
        if (!File.Exists(jobsFile))
            return true;

        // ジョブファイルをデシリアライズし、各アクションをgit rebase-todoフォーマットで書き出す
        using var stream = File.OpenRead(jobsFile);
        var collection = JsonSerializer.Deserialize(stream, JsonCodeGen.Default.InteractiveRebaseJobCollection);
        using var writer = new StreamWriter(file);
        foreach (var job in collection.Jobs)
        {
            // アクション種別をgitの1文字コード（p=pick, e=edit, r=reword, s=squash, f=fixup, d=drop）に変換する
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

    /// <summary>
    /// 対話的リベースのメッセージエディタモードとして起動を試みる。
    /// gitがrewordアクション等でコミットメッセージ編集を要求した際に呼ばれ、
    /// komorebi.interactive_rebaseファイルから該当コミットのメッセージをCOMMIT_EDITMSGに書き出す。
    /// </summary>
    /// <param name="args">コマンドライン引数</param>
    /// <param name="exitCode">エディタモードのプロセス終了コード</param>
    /// <returns>リベースメッセージエディタとして起動された場合はtrue</returns>
    private static bool TryLaunchAsRebaseMessageEditor(string[] args, out int exitCode)
    {
        exitCode = -1;

        // --rebase-message-editor引数が無ければ通常起動に移行する
        if (args.Length <= 1 || !args[0].Equals("--rebase-message-editor", StringComparison.Ordinal))
            return false;

        exitCode = 0;

        // 編集対象がCOMMIT_EDITMSGファイルであることを確認する
        var file = args[1];
        var filename = Path.GetFileName(file);
        if (!filename.Equals("COMMIT_EDITMSG", StringComparison.OrdinalIgnoreCase))
            return true;

        // リベース状態管理ファイル群の存在を確認する
        var gitDir = Path.GetDirectoryName(file)!;
        var origHeadFile = Path.Combine(gitDir, "rebase-merge", "orig-head");
        var ontoFile = Path.Combine(gitDir, "rebase-merge", "onto");
        var doneFile = Path.Combine(gitDir, "rebase-merge", "done");
        var jobsFile = Path.Combine(gitDir, "komorebi.interactive_rebase");
        if (!File.Exists(ontoFile) || !File.Exists(origHeadFile) || !File.Exists(doneFile) || !File.Exists(jobsFile))
            return true;

        // ジョブファイルのonto/origHeadが現在のリベースセッションと一致するか確認する
        var origHead = File.ReadAllText(origHeadFile).Trim();
        var onto = File.ReadAllText(ontoFile).Trim();
        using var stream = File.OpenRead(jobsFile);
        var collection = JsonSerializer.Deserialize(stream, JsonCodeGen.Default.InteractiveRebaseJobCollection);
        if (!collection.Onto.Equals(onto) || !collection.OrigHead.Equals(origHead))
            return true;

        // doneファイルから最後に処理されたコミットのSHAを取得する
        var done = File.ReadAllText(doneFile).Trim().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (done.Length == 0)
            return true;

        // 最終行からコミットSHAを正規表現で抽出する
        var current = done[^1].Trim();
        var match = REG_REBASE_TODO().Match(current);
        if (!match.Success)
            return true;

        // 該当SHAのジョブからメッセージを取得し、COMMIT_EDITMSGに書き出す
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

    /// <summary>
    /// ファイル履歴ビューアモードとして起動を試みる。
    /// --file-historyオプション付きで起動された場合に、指定ファイルのgit履歴画面を表示する。
    /// </summary>
    /// <returns>ファイル履歴ビューアとして起動された場合はtrue</returns>
    private static bool TryLaunchAsFileHistoryViewer(IClassicDesktopStyleApplicationLifetime desktop)
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

    /// <summary>
    /// blameビューアモードとして起動を試みる。
    /// --blameオプション付きで起動された場合に、指定ファイルのblame画面を表示する。
    /// </summary>
    /// <returns>blameビューアとして起動された場合はtrue</returns>
    private static bool TryLaunchAsBlameViewer(IClassicDesktopStyleApplicationLifetime desktop)
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
        if (head is null)
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
    /// gitのcore.editorとして起動された場合の処理。
    /// 対話的リベースなどでgitがコミットメッセージ編集を要求する際に呼ばれる。
    /// </summary>
    /// <returns>core.editorモードとして起動された場合はtrue</returns>
    private static bool TryLaunchAsCoreEditor(IClassicDesktopStyleApplicationLifetime desktop)
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
    /// gitのaskpass（認証情報入力）として起動された場合の処理。
    /// 環境変数 SOURCEGIT_LAUNCH_AS_ASKPASS=TRUE が設定されている場合に有効。
    /// </summary>
    /// <returns>askpassモードとして起動された場合はtrue</returns>
    private static bool TryLaunchAsAskpass(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var launchAsAskpass = Environment.GetEnvironmentVariable("SOURCEGIT_LAUNCH_AS_ASKPASS");
        if (launchAsAskpass is not "TRUE")
            return false;

        var args = desktop.Args;
        if (args?.Length > 0)
        {
            var askpass = new Views.Askpass();
            askpass.TxtDescription.Text = args[0];
            askpass.SetHint(args[0]);
            desktop.MainWindow = askpass;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 通常モード（メインGUI）としてアプリケーションを起動する。
    /// 外部ツールの初期化、ランチャーウィンドウの生成、初回起動セットアップ、
    /// および起動時の更新チェックを行う。
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

#if DEBUG
        // デバッグビルド時のみCRDebuggerを初期化する。
        // 画面の四隅（40x40px領域）をダブルクリックでデバッガー画面をトグル表示する。
        InitCRDebugger(desktop.MainWindow);
#endif
    }

#if DEBUG
    /// <summary>
    /// CRDebuggerを初期化し、メインウィンドウの四隅ダブルクリックでトグルするハンドラを登録する。
    /// </summary>
    private static void InitCRDebugger(Window mainWindow)
    {
        if (CRDebugger.Core.CRDebugger.IsInitialized)
            return;

        var options = new CRDebugger.Core.CRDebuggerOptions
        {
            Theme = CRDebugger.Core.Theming.CRTheme.Dark,
            DefaultTab = CRDebugger.Core.CRTab.Console,
            CaptureUnhandledExceptions = true,
        };
        CRDebugger.Avalonia.CRDebuggerAvaloniaExtensions.UseAvalonia(options);
        CRDebugger.Core.CRDebugger.Initialize(options);

        // Optionsタブにテスト用コンテナを登録する
        CRDebugger.Core.CRDebugger.AddOptionContainer(new DebugNotificationTester());
        CRDebugger.Core.CRDebugger.AddOptionContainer(new DebugAskpassTester());

        // 画面の四隅（40x40px）のダブルクリックでデバッガーをトグルする
        const double cornerSize = 40;
        mainWindow.DoubleTapped += (_, e) =>
        {
            if (e.Source is not Avalonia.Visual visual)
                return;

            var pos = e.GetPosition(mainWindow);
            var bounds = mainWindow.Bounds;

            var isLeft = pos.X < cornerSize;
            var isRight = pos.X > bounds.Width - cornerSize;
            var isTop = pos.Y < cornerSize;
            var isBottom = pos.Y > bounds.Height - cornerSize;

            if ((isLeft || isRight) && (isTop || isBottom))
                CRDebugger.Core.CRDebugger.Toggle();
        };
    }

    /// <summary>
    /// CRDebugger Optionsタブに表示される通知テスト用コンテナ。
    /// 各シナリオをボタンから直接発火できる。
    /// CRDebuggerはリフレクションでインスタンスメソッドを検出するためstaticにできない。
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822")]
    private sealed class DebugNotificationTester
    {
        [CRDebugger.Core.Options.Attributes.CRCategory("通知テスト")]
        [CRDebugger.Core.Options.Attributes.CRAction(Label = "📢 情報通知")]
        [CRDebugger.Core.Options.Attributes.CRSortOrder(1)]
        public void SendInfo()
        {
            Dispatch("テスト情報通知メッセージ", false);
        }

        [CRDebugger.Core.Options.Attributes.CRCategory("通知テスト")]
        [CRDebugger.Core.Options.Attributes.CRAction(Label = "❌ エラー通知")]
        [CRDebugger.Core.Options.Attributes.CRSortOrder(2)]
        public void SendError()
        {
            Dispatch("テストエラー通知メッセージ", true);
        }

        [CRDebugger.Core.Options.Attributes.CRCategory("通知テスト")]
        [CRDebugger.Core.Options.Attributes.CRAction(Label = "📢 情報 + ヒント")]
        [CRDebugger.Core.Options.Attributes.CRSortOrder(3)]
        public void SendInfoWithHint()
        {
            Dispatch("操作が完了しました", false, "ヒント: これはテスト用のヒントメッセージです");
        }

        [CRDebugger.Core.Options.Attributes.CRCategory("通知テスト")]
        [CRDebugger.Core.Options.Attributes.CRAction(Label = "❌ エラー + ヒント")]
        [CRDebugger.Core.Options.Attributes.CRSortOrder(4)]
        public void SendErrorWithHint()
        {
            Dispatch("fatal: authentication failed for 'https://github.com/user/repo.git'", true, "認証に失敗しました。パスワードまたはトークンを確認してください。");
        }

        [CRDebugger.Core.Options.Attributes.CRCategory("通知テスト")]
        [CRDebugger.Core.Options.Attributes.CRAction(Label = "📢 長文通知")]
        [CRDebugger.Core.Options.Attributes.CRSortOrder(5)]
        public void SendLongMessage()
        {
            Dispatch("これは非常に長い通知メッセージのテストです。実際の運用で発生しうる長文のgitエラーメッセージや、複数行にわたる詳細情報が正しく表示されるかを確認するためのものです。", false);
        }

        [CRDebugger.Core.Options.Attributes.CRCategory("通知テスト")]
        [CRDebugger.Core.Options.Attributes.CRAction(Label = "📢 長文ヒント（折り返し確認）")]
        [CRDebugger.Core.Options.Attributes.CRSortOrder(6)]
        public void SendLongHint()
        {
            Dispatch(
                "fatal: Could not read from remote repository.",
                true,
                "SSH秘密鍵ファイルのパーミッションが緩すぎます。鍵ファイルは所有者のみアクセス可能（chmod 600）である必要があります。この問題はmacOSやLinuxで発生し、秘密鍵ファイルのパーミッションが644や755など他のユーザーからも読み取り可能な状態になっている場合に発生します。");
        }

        [CRDebugger.Core.Options.Attributes.CRCategory("通知テスト")]
        [CRDebugger.Core.Options.Attributes.CRAction(Label = "🔑 鍵パーミッションエラー（アクションボタン付き）")]
        [CRDebugger.Core.Options.Attributes.CRSortOrder(7)]
        public void SendKeyPermissionError()
        {
            var launcher = GetLauncher();
            if (launcher is null)
                return;

            var errorMsg =
                "@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@\n" +
                "@         WARNING: UNPROTECTED PRIVATE KEY FILE!          @\n" +
                "@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@\n" +
                "Permissions 0755 for '/Users/user/.ssh/id_rsa' are too open.\n" +
                "It is required that your private key files are NOT accessible by others.\n" +
                "This private key will be ignored.\n" +
                "Load key \"/Users/user/.ssh/id_rsa\": bad permissions\n" +
                "git@github.com: Permission denied (publickey).\n" +
                "fatal: Could not read from remote repository.";

            var hintKey = Models.GitErrorHelper.GetHintKey(errorMsg);
            var hint = string.IsNullOrEmpty(hintKey) ? "" : Text(hintKey.Replace("Text.", ""));
            var keyPath = Models.GitErrorHelper.ExtractKeyPathFromPermissionError(errorMsg);
            var fixLabel = Text("GitError.KeyPermissionTooOpen.Fix");

            // デバッグ用: Windows制限を外してアクションボタンを直接構築
            launcher.DispatchNotification(
                launcher.ActivePage?.Node.Id ?? "",
                errorMsg,
                true,
                hint,
                fixLabel,
                () => launcher.DispatchNotification(
                    launcher.ActivePage?.Node.Id ?? "",
                    $"[デバッグ] chmod 600 \"{keyPath}\" を実行します（テスト環境のため実際には実行しません）",
                    false));
        }

        [CRDebugger.Core.Options.Attributes.CRCategory("通知テスト")]
        [CRDebugger.Core.Options.Attributes.CRAction(Label = "🗑️ 通知クリア")]
        [CRDebugger.Core.Options.Attributes.CRSortOrder(8)]
        public void ClearNotifications()
        {
            GetLauncher()?.ActivePage?.ClearNotifications();
        }

        private static void Dispatch(string message, bool isError, string hint = "")
        {
            var launcher = GetLauncher();
            if (launcher is null)
                return;

            launcher.DispatchNotification(
                launcher.ActivePage?.Node.Id ?? "",
                message,
                isError,
                hint);
        }
    }

    /// <summary>
    /// Askpassダイアログの表示確認用テスター。
    /// 各種SSHプロンプトパターン（ホスト鍵確認、パスフレーズ、パスワード）を
    /// Askpassダイアログとしてプレビュー表示する。
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822")]
    private sealed class DebugAskpassTester
    {
        [CRDebugger.Core.Options.Attributes.CRCategory("Askpassテスト")]
        [CRDebugger.Core.Options.Attributes.CRAction(Label = "🔑 初回ホスト鍵確認")]
        [CRDebugger.Core.Options.Attributes.CRSortOrder(1)]
        public void ShowHostKeyNew()
        {
            ShowAskpass(
                "The authenticity of host 'github.com (64:ff9b::141b:b171)' can't be established.\n" +
                "ED25519 key fingerprint is SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU\n" +
                "This key is not known by any other names.\n" +
                "Are you sure you want to continue connecting (yes/no/[fingerprint])?");
        }

        [CRDebugger.Core.Options.Attributes.CRCategory("Askpassテスト")]
        [CRDebugger.Core.Options.Attributes.CRAction(Label = "⚠️ ホスト鍵変更警告")]
        [CRDebugger.Core.Options.Attributes.CRSortOrder(2)]
        public void ShowHostKeyChanged()
        {
            ShowAskpass(
                "@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@\n" +
                "@    WARNING: REMOTE HOST IDENTIFICATION HAS CHANGED!     @\n" +
                "@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@\n" +
                "IT IS POSSIBLE THAT SOMEONE IS DOING SOMETHING NASTY!\n" +
                "Host key for github.com has changed and you have requested strict checking.\n" +
                "Host key verification failed.");
        }

        [CRDebugger.Core.Options.Attributes.CRCategory("Askpassテスト")]
        [CRDebugger.Core.Options.Attributes.CRAction(Label = "🔐 パスフレーズ入力")]
        [CRDebugger.Core.Options.Attributes.CRSortOrder(3)]
        public void ShowPassphrase()
        {
            ShowAskpass("Enter passphrase for key '/Users/user/.ssh/id_ed25519': ");
        }

        [CRDebugger.Core.Options.Attributes.CRCategory("Askpassテスト")]
        [CRDebugger.Core.Options.Attributes.CRAction(Label = "🔒 パスワード入力")]
        [CRDebugger.Core.Options.Attributes.CRSortOrder(4)]
        public void ShowPassword()
        {
            ShowAskpass("Password for 'https://user@github.com': ");
        }

        private static void ShowAskpass(string message)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var askpass = new Views.Askpass { IsPreview = true };
                askpass.TxtDescription.Text = message;
                askpass.SetHint(message);
                askpass.Show();
            });
        }
    }
#endif

    /// <summary>
    /// 指定されたパスのリポジトリをタブで開く。
    /// 既にアプリが起動中のとき、二重起動防止の仕組みから呼ばれる。
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

    /// <summary>更新チェック中かどうかのアトミックフラグ（0=未実行, 1=実行中）</summary>
    private static int _isCheckingUpdate;

    /// <summary>
    /// GitHubリリースから最新バージョンを確認し、更新がある場合はダイアログを表示する。
    /// バックグラウンドスレッドで非同期実行される。
    /// </summary>
    /// <param name="manually">
    /// true: 手動チェック（結果を常に表示、無視タグをスキップ）
    /// false: 自動チェック（更新がある場合のみ表示、無視タグを確認）
    /// </param>
    private static void Check4Update(bool manually = false)
    {
        // 先勝ち: 既に更新チェック中なら何もしない（起動時自動チェックとメニュー手動チェックの同時実行を防止）
        if (Interlocked.CompareExchange(ref _isCheckingUpdate, 1, 0) != 0)
            return;

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
                if (newVersion is null)
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
            finally
            {
                Interlocked.Exchange(ref _isCheckingUpdate, 0);
            }
        });
    }

    /// <summary>
    /// 更新チェック結果をUIスレッドでダイアログ表示する。
    /// </summary>
    /// <param name="data">表示データ（VelopackUpdate / AlreadyUpToDate / SelfUpdateFailed）</param>
    private static void ShowSelfUpdateResult(object data)
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

    /// <summary>
    /// ユーザー入力のフォントファミリー名を正規化・検証する。
    /// カンマ区切りの各フォント名について以下の処理を行う:
    /// 1. 前後の空白をトリムし、連続する空白を1つに圧縮する
    /// 2. システムフォントとしてパースを試み、タイプフェースが存在するか確認する
    /// 3. システムフォントとして見つからない場合、バンドルフォント（fonts:Komorebi#プレフィックス）として再試行する
    /// 無効なフォント名は静かに除外される。
    /// </summary>
    /// <param name="input">ユーザーが入力したフォントファミリー名（カンマ区切りで複数指定可）</param>
    /// <returns>検証済みフォント名のカンマ区切り文字列。有効なフォントがない場合は空文字列</returns>
    private static string FixFontFamilyName(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        // カンマで分割して各フォント名を個別に処理する
        var parts = input.Split(',');
        List<string> trimmed = [];

        foreach (var part in parts)
        {
            // 前後の空白をトリムし、空の要素はスキップする
            var t = part.Trim();
            if (string.IsNullOrEmpty(t))
                continue;

            // 連続する空白文字を1つに圧縮する（例: "Noto  Sans" → "Noto Sans"）
            var sb = new StringBuilder();
            var prevChar = '\0';

            foreach (var c in t)
            {
                if (c == ' ' && prevChar == ' ')
                    continue;  // 連続空白の2文字目以降をスキップする
                sb.Append(c);
                prevChar = c;
            }

            var name = sb.ToString();

            // システムフォントとしてパースを試みる
            try
            {
                var fontFamily = FontFamily.Parse(name);
                // タイプフェース（Regular, Bold等）が1つ以上あれば有効なフォントと判定する
                if (fontFamily.FamilyTypefaces.Count > 0)
                    trimmed.Add(name);
            }
            catch
            {
                // フォントパースの例外は無視する（無効なフォント名として扱う）
            }
        }

        // 有効なフォントが1つ以上あればカンマ区切りで結合して返す
        return trimmed.Count > 0 ? string.Join(',', trimmed) : string.Empty;
    }

    /// <summary>
    /// リベースTodoファイルの各行からアクションコードとコミットSHAを抽出する正規表現。
    /// 例: "pick abc1234 commit message" → グループ1="abc1234"
    /// </summary>
    [GeneratedRegex(@"^[a-z]+\s+([a-fA-F0-9]{4,40})(\s+.*)?$")]
    private static partial Regex REG_REBASE_TODO();

    /// <summary>二重起動防止用のIPCチャネル</summary>
    private Models.IpcChannel _ipcChannel = null;
    /// <summary>ランチャー（メインウィンドウ）のビューモデル</summary>
    private ViewModels.Launcher _launcher = null;
    /// <summary>現在適用中のロケールリソースディクショナリ</summary>
    private ResourceDictionary _activeLocale = null;
    /// <summary>テーマ固有の色オーバーライド（White, OneDark等のビルトインテーマ色）</summary>
    private ResourceDictionary _themeColorOverrides = null;
    /// <summary>ユーザー定義のテーマオーバーライド（JSONファイルから読み込んだカスタム色）</summary>
    private ResourceDictionary _themeOverrides = null;
    /// <summary>ユーザー指定のフォントオーバーライド（デフォルト・等幅フォント設定）</summary>
    private ResourceDictionary _fontsOverrides = null;
}
