using System;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     アプリケーションの自動アップデートを管理するViewModel。
    ///     Velopackフレームワークを使用してアップデートのダウンロードと適用を行う。
    /// </summary>
    public class SelfUpdate : ObservableObject
    {
        /// <summary>
        ///     アップデート関連のデータ。更新情報やエラー情報を保持する。
        /// </summary>
        public object Data
        {
            get => _data;
            set => SetProperty(ref _data, value);
        }

        /// <summary>
        ///     アップデートのダウンロード中かどうか。
        /// </summary>
        public bool IsDownloading
        {
            get => _isDownloading;
            set => SetProperty(ref _isDownloading, value);
        }

        /// <summary>
        ///     ダウンロードの進捗率（0-100）。
        /// </summary>
        public int DownloadProgress
        {
            get => _downloadProgress;
            set => SetProperty(ref _downloadProgress, value);
        }

        /// <summary>
        ///     アップデートをダウンロードして適用する。ダウンロード完了後にアプリを再起動する。
        ///     既にダウンロード中の場合は何もしない。
        /// </summary>
        public void DownloadAndApplyUpdate(Models.VelopackUpdate update)
        {
            if (_isDownloading)
                return;

            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            IsDownloading = true;
            DownloadProgress = 0;

            Task.Run(async () =>
            {
                try
                {
                    await update.DownloadAsync(
                        p => Avalonia.Threading.Dispatcher.UIThread.Post(() => DownloadProgress = p),
                        token);

                    update.ApplyAndRestart();
                }
                catch (OperationCanceledException)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => IsDownloading = false);
                }
                catch (Exception e)
                {
                    Models.Logger.LogException("アップデートダウンロード失敗", e);
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        IsDownloading = false;
                        Data = new Models.SelfUpdateFailed(e);
                    });
                }
            });
        }

        /// <summary>
        ///     進行中のダウンロードをキャンセルする。
        /// </summary>
        public void CancelDownload()
        {
            _cts?.Cancel();
        }

        private object _data = null;
        private bool _isDownloading = false;
        private int _downloadProgress = 0;
        private CancellationTokenSource _cts;
    }
}
