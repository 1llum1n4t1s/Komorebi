using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>
/// Git LFS管理下の画像ファイルを非同期で読み込み表示するViewModel。
/// LFSオブジェクトからビットマップ画像を取得してUIに提供する。
/// </summary>
public class RevisionLFSImage : ObservableObject
{
    /// <summary>
    /// LFSオブジェクト情報。ファイルサイズやOIDなどの情報を保持する。
    /// </summary>
    public Models.RevisionLFSObject LFS
    {
        get;
    }

    /// <summary>
    /// 読み込まれた画像データ。非同期読み込み完了後に設定される。
    /// </summary>
    public Models.RevisionImageFile Image
    {
        get => _image;
        private set => SetProperty(ref _image, value);
    }

    /// <summary>
    /// コンストラクタ。LFSオブジェクトからバックグラウンドで画像を読み込む。
    /// </summary>
    /// <param name="repo">リポジトリパス</param>
    /// <param name="file">ファイルパス</param>
    /// <param name="lfs">LFSオブジェクト情報</param>
    /// <param name="decoder">画像デコーダー</param>
    public RevisionLFSImage(string repo, string file, Models.LFSObject lfs, Models.ImageDecoder decoder)
    {
        LFS = new Models.RevisionLFSObject() { Object = lfs };

        Task.Run(async () =>
        {
            var source = await ImageSource.FromLFSObjectAsync(repo, lfs, decoder).ConfigureAwait(false);
            var img = new Models.RevisionImageFile(file, source.Bitmap, source.Size);
            Dispatcher.UIThread.Post(() => Image = img);
        });
    }

    private Models.RevisionImageFile _image = null;
}
