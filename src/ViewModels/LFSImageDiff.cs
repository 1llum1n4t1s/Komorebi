using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     LFSで管理された画像ファイルの差分表示ViewModel。
    ///     新旧のLFSオブジェクトから画像を非同期で読み込む。
    /// </summary>
    public class LFSImageDiff : ObservableObject
    {
        /// <summary>LFS差分情報（新旧のOID・サイズ）。</summary>
        public Models.LFSDiff LFS
        {
            get;
        }

        /// <summary>デコードされた新旧画像の差分データ。</summary>
        public Models.ImageDiff Image
        {
            get => _image;
            private set => SetProperty(ref _image, value);
        }

        /// <summary>
        ///     コンストラクタ。バックグラウンドで新旧のLFSオブジェクトから画像を読み込む。
        /// </summary>
        public LFSImageDiff(string repo, Models.LFSDiff lfs, Models.ImageDecoder decoder)
        {
            LFS = lfs;

            Task.Run(async () =>
            {
                var oldImage = await ImageSource.FromLFSObjectAsync(repo, lfs.Old, decoder).ConfigureAwait(false);
                var newImage = await ImageSource.FromLFSObjectAsync(repo, lfs.New, decoder).ConfigureAwait(false);

                var img = new Models.ImageDiff()
                {
                    Old = oldImage.Bitmap,
                    OldFileSize = oldImage.Size,
                    New = newImage.Bitmap,
                    NewFileSize = newImage.Size
                };

                Dispatcher.UIThread.Post(() => Image = img);
            });
        }

        private Models.ImageDiff _image;
    }
}
