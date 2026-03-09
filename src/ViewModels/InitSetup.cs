using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     初回起動時のセットアップダイアログのViewModel。
    ///     表示言語とデフォルトクローンディレクトリを設定する。
    /// </summary>
    public class InitSetup : Popup
    {
        /// <summary>
        ///     コンストラクタ。OS設定から検出されたロケールを初期値に設定する。
        /// </summary>
        public InitSetup()
        {
            _selectedLocale = Preferences.DetectedLocale;
        }

        /// <summary>選択された表示言語（ロケールコード）。変更時にアプリのロケールを即座に反映する。</summary>
        public string SelectedLocale
        {
            get => _selectedLocale;
            set
            {
                if (SetProperty(ref _selectedLocale, value))
                    App.SetLocale(value);
            }
        }

        /// <summary>デフォルトのクローン先ディレクトリ。必須入力。</summary>
        [Required]
        public string DefaultCloneDir
        {
            get => _defaultCloneDir;
            set => SetProperty(ref _defaultCloneDir, value, true);
        }

        /// <summary>
        ///     確認ボタン押下時の処理。ロケールとクローンディレクトリをPreferencesに保存し、
        ///     指定ディレクトリ内のリポジトリを自動スキャンする。
        /// </summary>
        public override async Task<bool> Sure()
        {
            Preferences.Instance.Locale = _selectedLocale;
            Preferences.Instance.GitDefaultCloneDir = _defaultCloneDir;

            if (!string.IsNullOrEmpty(_defaultCloneDir))
                await ScanRepositories.ScanDirectoryAsync(_defaultCloneDir);

            return true;
        }

        private string _selectedLocale;
        private string _defaultCloneDir = string.Empty;
    }
}
