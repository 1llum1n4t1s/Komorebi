using System;
using System.Threading;
using System.Threading.Tasks;

using Velopack;

namespace Komorebi.Models
{
    /// <summary>
    ///     Velopackによるアプリケーション自動更新の情報を保持するクラス。
    ///     ダウンロードと適用・再起動を管理する。
    /// </summary>
    public class VelopackUpdate
    {
        /// <summary>リリースのタグ名（例: v1.0.5）</summary>
        public string TagName => $"v{_updateInfo.TargetFullRelease.Version}";
        /// <summary>バージョン文字列</summary>
        public string VersionString => _updateInfo.TargetFullRelease.Version.ToString();

        public VelopackUpdate(UpdateManager manager, UpdateInfo updateInfo)
        {
            _manager = manager;
            _updateInfo = updateInfo;
        }

        /// <summary>
        ///     更新パッケージを非同期でダウンロードする
        /// </summary>
        /// <param name="onProgress">進捗率（0-100）のコールバック</param>
        /// <param name="token">キャンセルトークン</param>
        public async Task DownloadAsync(Action<int> onProgress, CancellationToken token)
        {
            await _manager.DownloadUpdatesAsync(_updateInfo, onProgress, cancelToken: token);
        }

        /// <summary>
        ///     ダウンロード済みの更新を適用してアプリケーションを再起動する
        /// </summary>
        public void ApplyAndRestart()
        {
            _manager.ApplyUpdatesAndRestart(_updateInfo);
        }

        private readonly UpdateManager _manager;
        private readonly UpdateInfo _updateInfo;
    }

    /// <summary>
    ///     既に最新バージョンであることを示すマーカークラス
    /// </summary>
    public class AlreadyUpToDate;

    /// <summary>
    ///     自動更新の失敗情報を保持するクラス
    /// </summary>
    public class SelfUpdateFailed
    {
        /// <summary>失敗理由のメッセージ</summary>
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
