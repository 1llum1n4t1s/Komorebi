using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     コマンドログ一覧を表示するViewModel。
    ///     リポジトリで実行されたgitコマンドの履歴を閲覧・管理する。
    /// </summary>
    public class ViewLogs : ObservableObject
    {
        /// <summary>
        ///     コマンドログのリスト。リポジトリのログコレクションを参照する。
        /// </summary>
        public AvaloniaList<CommandLog> Logs
        {
            get => _repo.Logs;
        }

        /// <summary>
        ///     現在選択されているコマンドログ。
        /// </summary>
        public CommandLog SelectedLog
        {
            get => _selectedLog;
            set => SetProperty(ref _selectedLog, value);
        }

        /// <summary>
        ///     コンストラクタ。リポジトリのログから初期選択を設定する。
        /// </summary>
        public ViewLogs(Repository repo)
        {
            _repo = repo;
            // ログが存在する場合は最初のエントリを選択
            _selectedLog = repo.Logs?.Count > 0 ? repo.Logs[0] : null;
        }

        /// <summary>
        ///     全てのログをクリアする。
        /// </summary>
        public void ClearAll()
        {
            SelectedLog = null;
            Logs.Clear();
        }

        private Repository _repo = null;
        private CommandLog _selectedLog = null;
    }
}
