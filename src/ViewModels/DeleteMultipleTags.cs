using System.Collections.Generic;
using System.Threading.Tasks;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     複数のタグを一括削除するためのダイアログViewModel。
    ///     リモートからの削除オプションも提供する。
    /// </summary>
    public class DeleteMultipleTags : Popup
    {
        /// <summary>
        ///     削除対象のタグリスト。
        /// </summary>
        public List<Models.Tag> Tags
        {
            get;
        }

        /// <summary>
        ///     リモートからもタグを削除するかどうか。
        /// </summary>
        public bool DeleteFromRemote
        {
            get;
            set;
        } = false;

        /// <summary>
        ///     コンストラクタ。対象リポジトリとタグリストを指定する。
        /// </summary>
        public DeleteMultipleTags(Repository repo, List<Models.Tag> tags)
        {
            _repo = repo;
            Tags = tags;
        }

        /// <summary>
        ///     複数タグの一括削除を実行する確認アクション。
        ///     各タグをローカルで削除し、オプションで全リモートからも削除する。
        /// </summary>
        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Deleting multiple tags...";

            var log = _repo.CreateLog("Delete Multiple Tags");
            Use(log);

            // 各タグを順番に削除
            foreach (var tag in Tags)
            {
                var succ = await new Commands.Tag(_repo.FullPath, tag.Name)
                .Use(log)
                .DeleteAsync();

                if (succ && DeleteFromRemote)
                {
                    foreach (var r in _repo.Remotes)
                        await new Commands.Push(_repo.FullPath, r.Name, $"refs/tags/{tag.Name}", true)
                            .Use(log)
                            .RunAsync();
                }
            }

            log.Complete();
            _repo.MarkTagsDirtyManually();
            return true;
        }

        private readonly Repository _repo;
    }
}
