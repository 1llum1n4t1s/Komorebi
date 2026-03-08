using System;
using System.Threading;
using System.Threading.Tasks;

using Velopack;

namespace Komorebi.Models
{
    public class VelopackUpdate
    {
        public string TagName => $"v{_updateInfo.TargetFullRelease.Version}";
        public string VersionString => _updateInfo.TargetFullRelease.Version.ToString();

        public VelopackUpdate(UpdateManager manager, UpdateInfo updateInfo)
        {
            _manager = manager;
            _updateInfo = updateInfo;
        }

        public async Task DownloadAsync(Action<int> onProgress, CancellationToken token)
        {
            await _manager.DownloadUpdatesAsync(_updateInfo, onProgress, cancelToken: token);
        }

        public void ApplyAndRestart()
        {
            _manager.ApplyUpdatesAndRestart(_updateInfo);
        }

        private readonly UpdateManager _manager;
        private readonly UpdateInfo _updateInfo;
    }

    public class AlreadyUpToDate;

    public class SelfUpdateFailed
    {
        public string Reason
        {
            get;
            private set;
        }

        public SelfUpdateFailed(Exception e)
        {
            if (e.InnerException is { } inner)
                Reason = inner.Message;
            else
                Reason = e.Message;
        }
    }
}
