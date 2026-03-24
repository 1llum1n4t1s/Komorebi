using System.Threading.Tasks;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     Gitリポジトリの初期化（git init）を行うダイアログのViewModel。
    ///     指定パスに新規リポジトリを作成する。
    /// </summary>
    public class Init : Popup
    {
        /// <summary>初期化対象のディレクトリパス。</summary>
        public string TargetPath
        {
            get => _targetPath;
            set => SetProperty(ref _targetPath, value);
        }

        /// <summary>初期化が必要な理由の説明テキスト。</summary>
        public string Reason
        {
            get;
            private set;
        }

        /// <summary>
        ///     コンストラクタ。ページID、パス、親ノード、理由を指定して初期化する。
        /// </summary>
        public Init(string pageId, string path, RepositoryNode parent, string reason)
        {
            _pageId = pageId;
            _targetPath = path;
            _parentNode = parent;

            Reason = string.IsNullOrEmpty(reason) ? "unknown error" : reason;
            Reason = Reason.Trim();
        }

        /// <summary>
        ///     確認ボタン押下時の処理。git initを実行し、
        ///     成功時はリポジトリノードをPreferencesに登録してWelcome画面を更新する。
        /// </summary>
        public override async Task<bool> Sure()
        {
            ProgressDescription = App.Text("Progress.InitRepo", _targetPath);

            var log = new CommandLog("Initialize");
            Use(log);

            var succ = await new Commands.Init(_pageId, _targetPath)
                .Use(log)
                .ExecAsync();

            log.Complete();

            // 成功時: リポジトリノードを登録し、ステータスを更新
            if (succ)
            {
                var node = Preferences.Instance.FindOrAddNodeByRepositoryPath(_targetPath, _parentNode, true);
                await node.UpdateStatusAsync(false, null);

                Welcome.Instance.Refresh();
            }
            return succ;
        }

        private readonly string _pageId = null;
        private string _targetPath = null;
        private readonly RepositoryNode _parentNode = null;
    }
}
