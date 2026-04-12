using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace Komorebi.Views;

/// <summary>
/// ユーザーアバター画像（Gravatar/GitHub風/フォールバック）を描画するカスタムコントロール。
/// </summary>
public class Avatar : Control, Models.IAvatarHost
{
    public static readonly StyledProperty<Models.User> UserProperty =
        AvaloniaProperty.Register<Avatar, Models.User>(nameof(User));

    public Models.User User
    {
        get => GetValue(UserProperty);
        set => SetValue(UserProperty, value);
    }

    public static readonly StyledProperty<bool> UseGitHubStyleAvatarProperty =
        AvaloniaProperty.Register<Avatar, bool>(nameof(UseGitHubStyleAvatar));

    public bool UseGitHubStyleAvatar
    {
        get => GetValue(UseGitHubStyleAvatarProperty);
        set => SetValue(UseGitHubStyleAvatarProperty, value);
    }

    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public Avatar()
    {
        // 高品質なビットマップ補間モードを設定する
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.HighQuality);

        // 設定のGitHubスタイルアバター使用フラグにバインドする
        this.Bind(UseGitHubStyleAvatarProperty, new Binding()
        {
            Mode = BindingMode.OneWay,
            Source = ViewModels.Preferences.Instance,
            Path = "UseGitHubStyleAvatar"
        });
    }

    /// <summary>
    /// コントロールの描画処理を行う。
    /// </summary>
    public override void Render(DrawingContext context)
    {
        if (User is null)
            return;

        // 角丸の半径を計算し、クリップ領域を設定する
        var corner = (float)Math.Max(2, Bounds.Width / 16);
        var rect = new Rect(0, 0, Bounds.Width, Bounds.Height);
        var clip = context.PushClip(new RoundedRect(rect, corner));

        if (_img is not null)
        {
            // アバター画像が取得済みの場合はそのまま描画する
            context.DrawImage(_img, rect);
        }
        else if (!UseGitHubStyleAvatar)
        {
            // フォールバック: イニシャル文字をグラデーション背景に描画する
            var fallback = GetFallbackString(User.Name);
            var typeface = new Typeface("fonts:Komorebi#JetBrains Mono");
            var label = new FormattedText(
                fallback,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                Math.Max(Bounds.Width * 0.65, 10),
                Brushes.White);

            // 文字コードの合計値からグラデーション色を決定する
            var chars = fallback.ToCharArray();
            var sum = 0;
            foreach (var c in chars)
                sum += Math.Abs(c);

            var bg = new LinearGradientBrush()
            {
                GradientStops = FALLBACK_GRADIENTS[sum % FALLBACK_GRADIENTS.Length],
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            };

            // テキストを中央に配置して描画する
            Point textOrigin = new Point((Bounds.Width - label.Width) * 0.5, (Bounds.Height - label.Height) * 0.5);
            context.DrawRectangle(bg, null, new Rect(0, 0, Bounds.Width, Bounds.Height), corner, corner);
            context.DrawText(label, textOrigin);
        }
        else
        {
            // GitHub風アバター: メールアドレスのMD5ハッシュから5x5のピクセルパターンを生成する
            context.DrawRectangle(Brushes.White, new Pen(new SolidColorBrush(Colors.Black, 0.3f), 0.65f), rect, corner, corner);

            // グリッドのオフセットとステップサイズを計算する
            var offsetX = Bounds.Width / 10.0;
            var offsetY = Bounds.Height / 10.0;

            var stepX = (Bounds.Width - offsetX * 2) / 5.0;
            var stepY = (Bounds.Height - offsetY * 2) / 5.0;

            // メールアドレスのMD5ハッシュから色とパターンを決定する
            var user = User;
            var lowered = user.Email.ToLower(CultureInfo.CurrentCulture).Trim();
            var hash = MD5.HashData(Encoding.Default.GetBytes(lowered));

            var brush = new SolidColorBrush(new Color(255, hash[0], hash[1], hash[2]));
            var switches = new bool[15];
            for (int i = 0; i < switches.Length; i++)
                switches[i] = hash[i + 1] % 2 == 1;

            // 右半分（中央列含む）のパターンを描画する
            for (int row = 0; row < 5; row++)
            {
                var x = offsetX + stepX * 2;
                var y = offsetY + stepY * row;
                var idx = row * 3;

                if (switches[idx])
                    context.FillRectangle(brush, new Rect(x, y, stepX, stepY));

                if (switches[idx + 1])
                    context.FillRectangle(brush, new Rect(x + stepX, y, stepX, stepY));

                if (switches[idx + 2])
                    context.FillRectangle(brush, new Rect(x + stepX * 2, y, stepX, stepY));
            }

            // 左半分のパターンを描画する（右側と対称）
            for (int row = 0; row < 5; row++)
            {
                var x = offsetX;
                var y = offsetY + stepY * row;
                var idx = row * 3 + 2;

                if (switches[idx])
                    context.FillRectangle(brush, new Rect(x, y, stepX, stepY));

                if (switches[idx - 1])
                    context.FillRectangle(brush, new Rect(x + stepX, y, stepX, stepY));
            }
        }

        clip.Dispose();
    }

    /// <summary>
    /// アバターリソース変更時のコールバック。対象ユーザーの場合は画像を更新する。
    /// </summary>
    public void OnAvatarResourceChanged(string email, Bitmap image)
    {
        // 現在のユーザーのメールアドレスと一致する場合のみ画像を更新する
        if (email.Equals(User?.Email, StringComparison.Ordinal))
        {
            _img = image;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// コントロールが読み込まれた際の処理。
    /// </summary>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        // アバターマネージャーに購読登録し、コンテキストメニューのハンドラを設定する
        Models.AvatarManager.Instance.Subscribe(this);
        ContextRequested += OnContextRequested;
    }

    /// <summary>
    /// コントロールがアンロードされた際の処理。
    /// </summary>
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        // コンテキストメニューのハンドラを解除し、アバターマネージャーから購読解除する
        ContextRequested -= OnContextRequested;
        Models.AvatarManager.Instance.Unsubscribe(this);
    }

    /// <summary>
    /// プロパティが変更された際の処理。
    /// </summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == UserProperty)
        {
            // ユーザーが変更された場合、新しいアバター画像をリクエストして再描画する
            var user = User;
            if (user is null)
                return;

            _img = Models.AvatarManager.Instance.Request(User.Email, false);
            InvalidateVisual();
        }
        else if (change.Property == UseGitHubStyleAvatarProperty)
        {
            // アバタースタイル設定が変更され、画像がない場合はフォールバック描画を更新する
            if (_img is null)
                InvalidateVisual();
        }
    }

    /// <summary>
    /// 右クリック時のコンテキストメニューを構築して表示する。
    /// 再取得・ローカルファイル読み込み・名前を付けて保存のメニュー項目を含む。
    /// </summary>
    private void OnContextRequested(object sender, ContextRequestedEventArgs e)
    {
        var toplevel = TopLevel.GetTopLevel(this);
        if (toplevel is null)
        {
            e.Handled = true;
            return;
        }

        // 「再取得」メニュー項目: Gravatarからアバターを再取得する
        var refetch = new MenuItem();
        refetch.Icon = App.CreateMenuIcon("Icons.Loading");
        refetch.Header = App.Text("Avatar.Refetch");
        refetch.Click += (_, ev) =>
        {
            if (User is not null)
                Models.AvatarManager.Instance.Request(User.Email, true);

            ev.Handled = true;
        };

        // 「読み込み」メニュー項目: ローカルのPNGファイルをアバターとして設定する
        var load = new MenuItem();
        load.Icon = App.CreateMenuIcon("Icons.Folder.Open");
        load.Header = App.Text("Avatar.Load");
        load.Click += async (_, ev) =>
        {
            var options = new FilePickerOpenOptions()
            {
                FileTypeFilter = [new FilePickerFileType("PNG") { Patterns = ["*.png"] }],
                AllowMultiple = false,
            };

            var selected = await toplevel.StorageProvider.OpenFilePickerAsync(options);
            if (selected.Count == 1)
            {
                var localFile = selected[0].Path.LocalPath;
                Models.AvatarManager.Instance.SetFromLocal(User.Email, localFile);
            }

            ev.Handled = true;
        };

        // 「名前を付けて保存」メニュー項目: アバター画像をPNGファイルとして保存する
        var saveAs = new MenuItem();
        saveAs.Icon = App.CreateMenuIcon("Icons.Save");
        saveAs.Header = App.Text("SaveAs");
        saveAs.Click += async (_, ev) =>
        {
            var options = new FilePickerSaveOptions();
            options.Title = App.Text("SaveAs");
            options.DefaultExtension = ".png";
            options.FileTypeChoices = [new FilePickerFileType("PNG") { Patterns = ["*.png"] }];

            var storageFile = await toplevel.StorageProvider.SaveFilePickerAsync(options);
            if (storageFile is not null)
            {
                var saveTo = storageFile.Path.LocalPath;
                await using (var writer = File.Create(saveTo))
                {
                    if (_img is not null)
                    {
                        // 取得済み画像がある場合はそのまま保存する
                        _img.Save(writer);
                    }
                    else
                    {
                        // 画像がない場合はフォールバック描画をレンダリングして保存する
                        var pixelSize = new PixelSize((int)Bounds.Width, (int)Bounds.Height);
                        var dpi = new Vector(96, 96);

                        using (var rt = new RenderTargetBitmap(pixelSize, dpi))
                        using (var ctx = rt.CreateDrawingContext())
                        {
                            Render(ctx);
                            rt.Save(writer);
                        }
                    }
                }
            }

            ev.Handled = true;
        };

        // コンテキストメニューを組み立てて表示する
        var menu = new ContextMenu();
        menu.Items.Add(refetch);
        menu.Items.Add(load);
        menu.Items.Add(new MenuItem() { Header = "-" });
        menu.Items.Add(saveAs);

        menu.Open(this);
    }

    /// <summary>
    /// ユーザー名からフォールバック表示用のイニシャル文字列を生成する。
    /// </summary>
    private static string GetFallbackString(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "?";

        // 名前をスペースで分割して各パートの先頭文字を取得する
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        List<char> chars = [];
        foreach (var part in parts)
            chars.Add(part[0]);

        // 2文字以上かつASCII文字の場合は先頭と末尾のイニシャルを返す
        if (chars.Count >= 2 && char.IsAsciiLetterOrDigit(chars[0]) && char.IsAsciiLetterOrDigit(chars[^1]))
            return $"{chars[0]}{chars[^1]}";

        // それ以外は名前の最初の1文字を返す
        return name[..1];
    }

    /// <summary>
    /// フォールバックアバターで使用するグラデーション色の配列。
    /// </summary>
    private static readonly GradientStops[] FALLBACK_GRADIENTS = [
        new GradientStops() { new GradientStop(Colors.Orange, 0), new GradientStop(Color.FromRgb(255, 213, 134), 1) },
        new GradientStops() { new GradientStop(Colors.DodgerBlue, 0), new GradientStop(Colors.LightSkyBlue, 1) },
        new GradientStops() { new GradientStop(Colors.LimeGreen, 0), new GradientStop(Color.FromRgb(124, 241, 124), 1) },
        new GradientStops() { new GradientStop(Colors.Orchid, 0), new GradientStop(Color.FromRgb(248, 161, 245), 1) },
        new GradientStops() { new GradientStop(Colors.Tomato, 0), new GradientStop(Color.FromRgb(252, 165, 150), 1) },
    ];

    private Bitmap _img = null;
}
