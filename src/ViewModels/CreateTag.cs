using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     新しいタグを作成するためのダイアログViewModel。
    ///     注釈付きタグ・軽量タグの選択、GPG署名、リモートへのプッシュをサポートする。
    /// </summary>
    public class CreateTag : Popup
    {
        /// <summary>
        ///     タグ作成の基点となるオブジェクト（ブランチまたはコミット）。
        /// </summary>
        public object BasedOn
        {
            get;
            private set;
        }

        /// <summary>
        ///     作成するタグの名前。必須入力で書式チェックと重複チェックを行う。
        /// </summary>
        [Required(ErrorMessage = "Tag name is required!")]
        [RegularExpression(@"^(?!\.)(?!/)(?!.*\.$)(?!.*/$)(?!.*\.\.)[\w\-\+\./]+$", ErrorMessage = "Bad tag name format!")]
        [CustomValidation(typeof(CreateTag), nameof(ValidateTagName))]
        public string TagName
        {
            get => _tagName;
            set => SetProperty(ref _tagName, value, true);
        }

        /// <summary>
        ///     注釈付きタグのメッセージ。
        /// </summary>
        public string Message
        {
            get;
            set;
        }

        /// <summary>
        ///     注釈付きタグとして作成するかどうか。UI状態に永続化される。
        /// </summary>
        public bool Annotated
        {
            get => _repo.UIStates.CreateAnnotatedTag;
            set
            {
                if (_repo.UIStates.CreateAnnotatedTag != value)
                {
                    _repo.UIStates.CreateAnnotatedTag = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        ///     GPG署名でタグに署名するかどうか。
        /// </summary>
        public bool SignTag
        {
            get;
            set;
        } = false;

        /// <summary>
        ///     タグ作成後にすべてのリモートへプッシュするかどうか。
        /// </summary>
        public bool PushToRemotes
        {
            get => _repo.UIStates.PushToRemoteWhenCreateTag;
            set => _repo.UIStates.PushToRemoteWhenCreateTag = value;
        }

        /// <summary>
        ///     ブランチを基点としてタグを作成するコンストラクタ。
        /// </summary>
        public CreateTag(Repository repo, Models.Branch branch)
        {
            _repo = repo;
            _basedOn = branch.Head;

            BasedOn = branch;
            SignTag = new Commands.Config(repo.FullPath).Get("tag.gpgsign").Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        ///     コミットを基点としてタグを作成するコンストラクタ。
        /// </summary>
        public CreateTag(Repository repo, Models.Commit commit)
        {
            _repo = repo;
            _basedOn = commit.SHA;

            BasedOn = commit;
            SignTag = new Commands.Config(repo.FullPath).Get("tag.gpgsign").Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        ///     タグ名の重複を検証するカスタムバリデーション。
        /// </summary>
        public static ValidationResult ValidateTagName(string name, ValidationContext ctx)
        {
            if (ctx.ObjectInstance is CreateTag creator)
            {
                var found = creator._repo.Tags.Find(x => x.Name == name);
                if (found != null)
                    return new ValidationResult("A tag with same name already exists!");
            }
            return ValidationResult.Success;
        }

        /// <summary>
        ///     タグ作成を実行する確認アクション。
        ///     注釈付き/軽量タグの作成後、必要に応じてリモートへプッシュする。
        /// </summary>
        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = App.Text("Progress.CreateTag");

            // プッシュ先のリモート一覧を取得（プッシュしない場合はnull）
            var remotes = PushToRemotes ? _repo.Remotes : null;
            var log = _repo.CreateLog("Create Tag");
            Use(log);

            var cmd = new Commands.Tag(_repo.FullPath, _tagName)
                .Use(log);

            bool succ;
            if (_repo.UIStates.CreateAnnotatedTag)
                succ = await cmd.AddAsync(_basedOn, Message, SignTag);
            else
                succ = await cmd.AddAsync(_basedOn);

            if (succ && remotes != null)
            {
                foreach (var remote in remotes)
                    await new Commands.Push(_repo.FullPath, remote.Name, $"refs/tags/{_tagName}", false)
                        .Use(log)
                        .RunAsync();
            }

            log.Complete();
            return succ;
        }

        private readonly Repository _repo = null;      // 対象リポジトリ
        private string _tagName = string.Empty;         // タグ名
        private readonly string _basedOn;               // 基点となるリビジョンSHA
    }
}
