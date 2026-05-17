using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using BitMiracle.LibTiff.Classic;
using Pfim;
using StbImageSharp;

namespace Komorebi.ViewModels;

/// <summary>
/// 画像ソースを管理するクラス。
/// ファイル、Gitリビジョン、LFSオブジェクトから各種画像フォーマットを読み込む。
/// 対応形式: ICO, BMP, GIF, JPG, PNG, WebP, TGA, DDS, TIFF, PSD (upstream 203c51f3 で StbImageSharp 経由追加)。
/// </summary>
public class ImageSource
{
    /// <summary>デコードされたビットマップ画像。</summary>
    public Bitmap Bitmap { get; }

    /// <summary>画像ファイルのバイトサイズ。</summary>
    public long Size { get; }

    /// <summary>
    /// コンストラクタ。ビットマップとファイルサイズを指定して初期化する。
    /// </summary>
    public ImageSource(Bitmap bitmap, long size)
    {
        Bitmap = bitmap;
        Size = size;
    }

    /// <summary>
    /// ファイル拡張子に基づいて適切な画像デコーダを判定する。
    /// </summary>
    public static Models.ImageDecoder GetDecoder(string file)
    {
        // パフォーマンス: ToLower()の文字列割り当てを排除し、OrdinalIgnoreCaseで直接比較
        var ext = Path.GetExtension(file) ?? ".invalid_img";

        return ext.ToLowerInvariant() switch
        {
            ".ico" or ".bmp" or ".gif" or ".jpg" or ".jpeg" or ".png" or ".webp" => Models.ImageDecoder.Builtin,
            ".tga" or ".dds" => Models.ImageDecoder.Pfim,
            ".tif" or ".tiff" => Models.ImageDecoder.Tiff,
            // upstream 203c51f3: PSD ファイルを StbImageSharp 経由でデコード
            ".psd" => Models.ImageDecoder.StbImage,
            _ => Models.ImageDecoder.None,
        };
    }

    /// <summary>
    /// ローカルファイルから画像を非同期で読み込む。
    /// </summary>
    public static async Task<ImageSource> FromFileAsync(string fullpath, Models.ImageDecoder decoder)
    {
        await using var stream = File.OpenRead(fullpath);
        return await Task.Run(() => LoadFromStream(stream, decoder)).ConfigureAwait(false);
    }

    /// <summary>
    /// Gitリビジョンから画像を非同期で読み込む。
    /// </summary>
    public static async Task<ImageSource> FromRevisionAsync(string repo, string revision, string file, Models.ImageDecoder decoder)
    {
        await using var stream = await Commands.QueryFileContent.RunAsync(repo, revision, file).ConfigureAwait(false);
        return await Task.Run(() => LoadFromStream(stream, decoder)).ConfigureAwait(false);
    }

    /// <summary>
    /// Git LFSオブジェクトから画像を非同期で読み込む。
    /// ローカルキャッシュがあればそれを使用し、なければリモートから取得する。
    /// </summary>
    public static async Task<ImageSource> FromLFSObjectAsync(string repo, Models.LFSObject lfs, Models.ImageDecoder decoder)
    {
        if (string.IsNullOrEmpty(lfs.Oid) || lfs.Size == 0)
            return new ImageSource(null, 0);

        var commonDir = await new Commands.QueryGitCommonDir(repo).GetResultAsync().ConfigureAwait(false);
        var localFile = Path.Combine(commonDir, "lfs", "objects", lfs.Oid[..2], lfs.Oid[2..4], lfs.Oid);
        if (File.Exists(localFile))
            return await FromFileAsync(localFile, decoder).ConfigureAwait(false);

        await using var stream = await Commands.QueryFileContent.FromLFSAsync(repo, lfs.Oid, lfs.Size).ConfigureAwait(false);
        return await Task.Run(() => LoadFromStream(stream, decoder)).ConfigureAwait(false);
    }

    /// <summary>
    /// ストリームからデコーダを使って画像を読み込む内部メソッド。
    /// デコーダ種別に応じて適切なデコード処理に振り分ける。
    /// </summary>
    private static ImageSource LoadFromStream(Stream stream, Models.ImageDecoder decoder)
    {
        var size = stream.Length;
        if (size > 0)
        {
            try
            {
                switch (decoder)
                {
                    case Models.ImageDecoder.Builtin:
                        return DecodeWithAvalonia(stream, size);
                    case Models.ImageDecoder.Pfim:
                        return DecodeWithPfim(stream, size);
                    case Models.ImageDecoder.Tiff:
                        return DecodeWithTiff(stream, size);
                    case Models.ImageDecoder.StbImage:
                        // upstream 203c51f3: PSD ファイルを StbImageSharp 経由でデコード
                        return DecodeWithStbImage(stream, size);
                }
            }
            catch (Exception e)
            {
                Models.Logger.Log($"画像デコード失敗: {e.Message}", Models.LogLevel.Warning);
            }
        }

        return new ImageSource(null, 0);
    }

    /// <summary>
    /// Avaloniaのビルトインデコーダで画像を読み込む（ICO, BMP, GIF, JPG, PNG, WebP対応）。
    /// </summary>
    private static ImageSource DecodeWithAvalonia(Stream stream, long size)
    {
        var bitmap = new Bitmap(stream);
        return new ImageSource(bitmap, size);
    }

    /// <summary>
    /// Pfimライブラリを使用してTGA/DDS画像をデコードする。
    /// 各ピクセルフォーマットに応じてAvaloniaのBitmapに変換する。
    /// </summary>
    private static ImageSource DecodeWithPfim(Stream stream, long size)
    {
        using (var pfiImage = Pfimage.FromStream(stream))
        {
            // 悪意 TGA/DDS ヘッダで int オーバーフロー → 負値 stride で
            // AccessViolation のリスク。Width/Height/Width*4 を long で評価し、危険値は弾く。
            if (pfiImage.Width <= 0 || pfiImage.Height <= 0 ||
                (long)pfiImage.Width * 4 > int.MaxValue ||
                (long)pfiImage.Width * pfiImage.Height * 4 > 0x40000000L)
                return new ImageSource(null, 0);

            var data = pfiImage.Data;
            var stride = pfiImage.Stride;

            var pixelFormat = PixelFormats.Bgra8888;
            var alphaFormat = AlphaFormat.Opaque;
            // ピクセルフォーマットごとにAvalonia互換形式に変換
            switch (pfiImage.Format)
            {
                case ImageFormat.Rgb8:
                    pixelFormat = PixelFormats.Gray8;
                    break;
                case ImageFormat.R16f:
                    pixelFormat = PixelFormats.Gray16;
                    break;
                case ImageFormat.R32f:
                    pixelFormat = PixelFormats.Gray32Float;
                    break;
                case ImageFormat.R5g5b5:
                    pixelFormat = PixelFormats.Bgr555;
                    break;
                case ImageFormat.R5g5b5a1:
                    // 5551形式をBGRA8888に手動変換
                    var pixels1 = pfiImage.DataLen / 2;
                    data = new byte[pixels1 * 4];
                    stride = pfiImage.Width * 4;
                    for (var i = 0; i < pixels1; i++)
                    {
                        var src = BitConverter.ToUInt16(pfiImage.Data, i * 2);
                        data[i * 4 + 0] = (byte)Math.Round((src & 0x1F) / 31F * 255); // B
                        data[i * 4 + 1] = (byte)Math.Round(((src >> 5) & 0x1F) / 31F * 255); // G
                        data[i * 4 + 2] = (byte)Math.Round(((src >> 10) & 0x1F) / 31F * 255); // R
                        data[i * 4 + 3] = (byte)((src >> 15) * 255F); // A
                    }

                    alphaFormat = AlphaFormat.Unpremul;
                    break;
                case ImageFormat.R5g6b5:
                    pixelFormat = PixelFormats.Bgr565;
                    break;
                case ImageFormat.Rgb24:
                    pixelFormat = PixelFormats.Bgr24;
                    break;
                case ImageFormat.Rgba16:
                    // RGBA4444形式をBGRA8888に手動変換
                    var pixels2 = pfiImage.DataLen / 2;
                    data = new byte[pixels2 * 4];
                    stride = pfiImage.Width * 4;
                    for (var i = 0; i < pixels2; i++)
                    {
                        var src = BitConverter.ToUInt16(pfiImage.Data, i * 2);
                        data[i * 4 + 0] = (byte)Math.Round((src & 0x0F) / 15F * 255); // B
                        data[i * 4 + 1] = (byte)Math.Round(((src >> 4) & 0x0F) / 15F * 255); // G
                        data[i * 4 + 2] = (byte)Math.Round(((src >> 8) & 0x0F) / 15F * 255); // R
                        data[i * 4 + 3] = (byte)Math.Round(((src >> 12) & 0x0F) / 15F * 255); // A
                    }

                    alphaFormat = AlphaFormat.Unpremul;
                    break;
                case ImageFormat.Rgba32:
                    alphaFormat = AlphaFormat.Unpremul;
                    break;
                default:
                    return new ImageSource(null, 0);
            }

            // Marshal.UnsafeAddrOfPinnedArrayElement は名前に Pinned が
            // 入っているが実際には配列を pin しない (MSDN 明記)。GC が data を移動した直後に Bitmap ctor が
            // 古いアドレスを参照すると AV。GCHandle.Alloc(Pinned) で明示的に pin する。
            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                var ptr = handle.AddrOfPinnedObject();
                var pixelSize = new PixelSize(pfiImage.Width, pfiImage.Height);
                var dpi = new Vector(96, 96);
                var bitmap = new Bitmap(pixelFormat, alphaFormat, ptr, pixelSize, dpi, stride);
                return new ImageSource(bitmap, size);
            }
            finally
            {
                handle.Free();
            }
        }
    }

    /// <summary>
    /// LibTiffを使用してTIFF画像をデコードする。
    /// RGBA形式でピクセルデータを読み込み、WritableBitmapに変換する。
    /// </summary>
    private static ImageSource DecodeWithTiff(Stream stream, long size)
    {
        using (var tiff = Tiff.ClientOpen($"{Guid.NewGuid()}.tif", "r", stream, new TiffStream()))
        {
            if (tiff is null)
                return new ImageSource(null, 0);

            // Currently only supports image when its `BITSPERSAMPLE` is one in [1,2,4,8,16]
            var width = tiff.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
            var height = tiff.GetField(TiffTag.IMAGELENGTH)[0].ToInt();

            // 悪意 TIFF ヘッダで `width * height` の int オーバーフローを防止。
            // `new int[width * height]` が負値サイズで OverflowException、または極小サイズ確保後の
            // ReadRGBAImageOriented で範囲外書き込み → メモリ破壊の経路を遮断。
            if (width <= 0 || height <= 0 || (long)width * height > 0x10000000L)
                return new ImageSource(null, 0);

            var pixels = new int[width * height];
            tiff.ReadRGBAImageOriented(width, height, pixels, Orientation.TOPLEFT);

            var pixelSize = new PixelSize(width, height);
            var dpi = new Vector(96, 96);
            var bitmap = new WriteableBitmap(pixelSize, dpi, PixelFormats.Rgba8888, AlphaFormat.Unpremul);

            using var frameBuffer = bitmap.Lock();
            Marshal.Copy(pixels, 0, frameBuffer.Address, pixels.Length);
            return new ImageSource(bitmap, size);
        }
    }

    /// <summary>
    /// StbImageSharp (PSD 等) で画像を読み込む。
    /// upstream 203c51f3 由来。Rgba8888 / Unpremul に正規化したうえで Avalonia Bitmap を構築する。
    /// </summary>
    /// <remarks>
    /// 巨大画像 (Width &gt;= 2^29) に対する stride 計算の int オーバーフローを防止するため、
    /// (long) 演算で評価し int.MaxValue を超えるサイズは ImageSource.Invalid として扱う。
    /// 悪意ある PSD ヘッダ (Width = int.MaxValue 等) によるメモリ破損 / AccessViolationException を防ぐ。
    /// </remarks>
    private static ImageSource DecodeWithStbImage(Stream stream, long size)
    {
        var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

        // 防御的サイズ検証: 0 以下 / int オーバーフローを起こすサイズは弾く。
        // Width * 4 (RGBA) が int.MaxValue を超えると stride が負値になり Avalonia Bitmap で未定義動作になる。
        if (image.Width <= 0 || image.Height <= 0 || (long)image.Width * 4 > int.MaxValue)
            return new ImageSource(null, 0);

        var data = image.Data;
        var stride = image.Width * (int)image.Comp;

        // Marshal.UnsafeAddrOfPinnedArrayElement は名前と異なり
        // 実際には配列を pin しない (MSDN 明記)。GC 移動で AV のリスクがあるため GCHandle.Alloc(Pinned) で
        // 明示的に pin する。Avalonia Bitmap ctor が同期コピーするので Free 後にアクセスされる経路はない。
        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            var ptr = handle.AddrOfPinnedObject();
            var pixelSize = new PixelSize(image.Width, image.Height);
            var dpi = new Vector(96, 96);
            var bitmap = new Bitmap(PixelFormat.Rgba8888, AlphaFormat.Unpremul, ptr, pixelSize, dpi, stride);
            return new ImageSource(bitmap, size);
        }
        finally
        {
            handle.Free();
        }
    }
}
