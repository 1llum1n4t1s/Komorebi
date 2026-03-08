using System;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels
{
    public class SelfUpdate : ObservableObject
    {
        public object Data
        {
            get => _data;
            set => SetProperty(ref _data, value);
        }

        public bool IsDownloading
        {
            get => _isDownloading;
            set => SetProperty(ref _isDownloading, value);
        }

        public int DownloadProgress
        {
            get => _downloadProgress;
            set => SetProperty(ref _downloadProgress, value);
        }

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
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        IsDownloading = false;
                        Data = new Models.SelfUpdateFailed(e);
                    });
                }
            });
        }

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
