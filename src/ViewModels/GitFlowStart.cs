using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     Git Flowブランチの開始（start）操作を行うダイアログのViewModel。
    ///     新しいfeature/release/hotfixブランチを作成する。
    /// </summary>
    public class GitFlowStart : Popup
    {
        /// <summary>
        ///     Git Flowブランチの種類（feature/release/hotfix）。
        /// </summary>
        public Models.GitFlowBranchType Type
        {
            get;
            private set;
        }

        /// <summary>
        ///     ブランチ名のプレフィックス（例: "feature/"）。
        /// </summary>
        public string Prefix
        {
            get;
            private set;
        }

        /// <summary>
        ///     新規ブランチの名前（プレフィックスを除いた部分）。バリデーション付き。
        /// </summary>
        [Required(ErrorMessage = "Name is required!!!")]
        [RegularExpression(@"^[\w\-/\.#]+$", ErrorMessage = "Bad branch name format!")]
        [CustomValidation(typeof(GitFlowStart), nameof(ValidateBranchName))]
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value, true);
        }

        /// <summary>
        ///     コンストラクタ。リポジトリとブランチ種別を指定して初期化する。
        /// </summary>
        public GitFlowStart(Repository repo, Models.GitFlowBranchType type)
        {
            _repo = repo;

            Type = type;
            Prefix = _repo.GitFlow.GetPrefix(type);
        }

        /// <summary>
        ///     ブランチ名の重複チェックを行うバリデーションメソッド。
        ///     同名のブランチが既に存在する場合はエラーを返す。
        /// </summary>
        public static ValidationResult ValidateBranchName(string name, ValidationContext ctx)
        {
            if (ctx.ObjectInstance is GitFlowStart starter)
            {
                // プレフィックス付きのフルネームで既存ブランチと比較
                var check = $"{starter.Prefix}{name}";
                foreach (var b in starter._repo.Branches)
                {
                    if (b.FriendlyName == check)
                        return new ValidationResult("A branch with same name already exists!");
                }
            }

            return ValidationResult.Success;
        }

        /// <summary>
        ///     確認ボタン押下時の処理。Git Flowのstartコマンドを実行する。
        /// </summary>
        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = App.Text("Progress.GitFlowStart", $"{Prefix}{_name}");

            var log = _repo.CreateLog("GitFlow - Start");
            Use(log);

            var succ = await Commands.GitFlow.StartAsync(_repo.FullPath, Type, _name, log);
            log.Complete();
            return succ;
        }

        private readonly Repository _repo;
        private string _name = null;
    }
}
