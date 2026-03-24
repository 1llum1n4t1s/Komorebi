using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     アーカイブ作成ダイアログのViewModel。
    ///     git archiveコマンドで指定リビジョンのソースをZIPファイルにアーカイブする。
    /// </summary>
    public class Archive : Popup
    {
        /// <summary>
        ///     出力ファイルのパス。必須入力。
        /// </summary>
        [Required(ErrorMessage = "Output file name is required")]
        public string SaveFile
        {
            get => _saveFile;
            set => SetProperty(ref _saveFile, value, true);
        }

        /// <summary>
        ///     アーカイブの基準となるオブジェクト（ブランチ、コミット、またはタグ）。
        /// </summary>
        public object BasedOn
        {
            get;
            private set;
        }

        /// <summary>
        ///     ブランチを基準にアーカイブを作成するコンストラクタ。
        /// </summary>
        /// <param name="repo">対象のリポジトリViewModel</param>
        /// <param name="branch">アーカイブ対象のブランチ</param>
        public Archive(Repository repo, Models.Branch branch)
        {
            _repo = repo;
            _revision = branch.Head;
            _saveFile = $"archive-{Path.GetFileName(branch.Name)}.zip";
            BasedOn = branch;
        }

        /// <summary>
        ///     コミットを基準にアーカイブを作成するコンストラクタ。
        /// </summary>
        /// <param name="repo">対象のリポジトリViewModel</param>
        /// <param name="commit">アーカイブ対象のコミット</param>
        public Archive(Repository repo, Models.Commit commit)
        {
            _repo = repo;
            _revision = commit.SHA;
            _saveFile = $"archive-{commit.SHA.AsSpan(0, 10)}.zip";
            BasedOn = commit;
        }

        /// <summary>
        ///     タグを基準にアーカイブを作成するコンストラクタ。
        /// </summary>
        /// <param name="repo">対象のリポジトリViewModel</param>
        /// <param name="tag">アーカイブ対象のタグ</param>
        public Archive(Repository repo, Models.Tag tag)
        {
            _repo = repo;
            _revision = tag.SHA;
            _saveFile = $"archive-{Path.GetFileName(tag.Name)}.zip";
            BasedOn = tag;
        }

        /// <summary>
        ///     確定処理。git archiveコマンドを実行してZIPアーカイブを作成する。
        /// </summary>
        /// <returns>成功した場合はtrue</returns>
        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = App.Text("Progress.Archiving");

            // コマンドログを作成する
            var log = _repo.CreateLog("Archive");
            Use(log);

            // git archiveコマンドを実行する
            var succ = await new Commands.Archive(_repo.FullPath, _revision, _saveFile)
                .Use(log)
                .ExecAsync();

            log.Complete();

            // 成功時にデスクトップ通知を送信する
            if (succ)
                App.SendNotification(_repo.FullPath, $"Save archive to : {_saveFile}");
            return succ;
        }

        /// <summary>対象リポジトリへの参照</summary>
        private readonly Repository _repo = null;
        /// <summary>出力ファイルパス</summary>
        private string _saveFile;
        /// <summary>アーカイブ対象のリビジョン（SHA等）</summary>
        private readonly string _revision;
    }
}
