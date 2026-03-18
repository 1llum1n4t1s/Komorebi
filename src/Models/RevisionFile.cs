using System.Globalization;
using System.IO;
using Avalonia.Media.Imaging;

namespace Komorebi.Models
{
    /// <summary>
    ///     特定リビジョンのバイナリファイル情報
    /// </summary>
    public class RevisionBinaryFile
    {
        /// <summary>ファイルサイズ（バイト）</summary>
        public long Size { get; set; } = 0;
    }

    /// <summary>
    ///     特定リビジョンの画像ファイル情報。ビットマップ画像とメタデータを保持する。
    /// </summary>
    public class RevisionImageFile
    {
        /// <summary>デコードされたビットマップ画像</summary>
        public Bitmap Image { get; }
        /// <summary>画像ファイルのサイズ（バイト）</summary>
        public long FileSize { get; }
        /// <summary>画像の形式（拡張子を大文字に変換）</summary>
        public string ImageType { get; }
        public string ImageSize => Image != null ? $"{Image.PixelSize.Width} x {Image.PixelSize.Height}" : "0 x 0";

        public RevisionImageFile(string file, Bitmap img, long size)
        {
            Image = img;
            FileSize = size;
            var ext = Path.GetExtension(file);
            ImageType = string.IsNullOrEmpty(ext) ? string.Empty : ext.Substring(1).ToUpper(CultureInfo.CurrentCulture);
        }
    }

    /// <summary>
    ///     特定リビジョンのテキストファイル情報
    /// </summary>
    public class RevisionTextFile
    {
        /// <summary>ファイル名</summary>
        public string FileName { get; set; }
        /// <summary>テキスト内容</summary>
        public string Content { get; set; }
    }

    /// <summary>
    ///     特定リビジョンのGit LFSオブジェクト情報
    /// </summary>
    public class RevisionLFSObject
    {
        /// <summary>LFSオブジェクト</summary>
        public LFSObject Object { get; set; }
    }

    /// <summary>
    ///     特定リビジョンのサブモジュール情報
    /// </summary>
    public class RevisionSubmodule
    {
        public Commit Commit { get; set; } = null;
        public CommitFullMessage FullMessage { get; set; } = null;
    }
}
