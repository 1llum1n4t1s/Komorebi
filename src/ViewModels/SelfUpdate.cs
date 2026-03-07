using System;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
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

            IsDownloading = true;
            DownloadProgress = 0;

            Task.Run(async () =>
            {
                try
                {
                    await update.Manager.DownloadUpdatesAsync(
                        update.UpdateInfo,
                        p => Avalonia.Threading.Dispatcher.UIThread.Post(() => DownloadProgress = p));

                    update.Manager.ApplyUpdatesAndRestart(update.UpdateInfo);
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

        private object _data = null;
        private bool _isDownloading = false;
        private int _downloadProgress = 0;
    }
}
