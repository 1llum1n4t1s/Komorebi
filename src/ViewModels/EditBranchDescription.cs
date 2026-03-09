using System;
using System.Threading.Tasks;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     ブランチの説明文を編集するダイアログViewModel。
    ///     git config の branch.{name}.description を更新する。
    /// </summary>
    public class EditBranchDescription : Popup
    {
        /// <summary>
        ///     編集対象のブランチ。
        /// </summary>
        public Models.Branch Target
        {
            get;
        }

        /// <summary>
        ///     ブランチの説明文。UIでの編集対象。
        /// </summary>
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        /// <summary>
        ///     コンストラクタ。対象リポジトリ、ブランチ、現在の説明文を指定する。
        /// </summary>
        public EditBranchDescription(Repository repo, Models.Branch target, string desc)
        {
            Target = target;

            _repo = repo;
            _originalDescription = desc;
            _description = desc;
        }

        /// <summary>
        ///     説明文の保存を実行する確認アクション。
        ///     変更がない場合は何もせずに成功を返す。
        /// </summary>
        public override async Task<bool> Sure()
        {
            var trimmed = _description.Trim();
            // 変更がない場合はスキップ
            if (string.IsNullOrEmpty(trimmed))
            {
                if (string.IsNullOrEmpty(_originalDescription))
                    return true;
            }
            else if (trimmed.Equals(_originalDescription, StringComparison.Ordinal))
            {
                return true;
            }

            var log = _repo.CreateLog("Edit Branch Description");
            Use(log);

            await new Commands.Config(_repo.FullPath)
                .Use(log)
                .SetAsync($"branch.{Target.Name}.description", trimmed);

            log.Complete();
            return true;
        }

        private readonly Repository _repo; // 対象リポジトリ
        private string _originalDescription = string.Empty; // 元の説明文（変更検出用）
        private string _description = string.Empty; // 編集中の説明文
    }
}
