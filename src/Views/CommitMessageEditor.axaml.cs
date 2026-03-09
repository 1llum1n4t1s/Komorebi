using System;
using System.IO;
using System.Text.Json;

using Avalonia.Interactivity;

namespace Komorebi.Views
{
    /// <summary>
    ///     コミットメッセージエディタのコードビハインド。
    /// </summary>
    public partial class CommitMessageEditor : ChromelessWindow
    {
        public string ConventionalTypesOverride
        {
            get;
            private set;
        } = string.Empty;

        /// <summary>
        ///     コンストラクタ。コンポーネントを初期化する。
        /// </summary>
        public CommitMessageEditor()
        {
            InitializeComponent();
        }

        /// <summary>
        ///     AsStandaloneの処理を行う。
        /// </summary>
        public void AsStandalone(string file)
        {
            var gitDir = new Commands.QueryGitDir(Path.GetDirectoryName(file)).GetResult();
            if (!string.IsNullOrEmpty(gitDir))
            {
                var settingsFile = Path.Combine(gitDir, "komorebi.settings");
                if (File.Exists(settingsFile))
                {
                    try
                    {
                        using var stream = File.OpenRead(settingsFile);
                        var settings = JsonSerializer.Deserialize(stream, JsonCodeGen.Default.RepositorySettings);
                        ConventionalTypesOverride = settings.ConventionalTypesOverride;
                    }
                    catch
                    {
                        // Ignore errors
                    }
                }
            }

            _onSave = msg => File.WriteAllText(file, msg);
            _shouldExitApp = true;

            Editor.CommitMessage = File.ReadAllText(file).ReplaceLineEndings("\n").Trim();
        }

        /// <summary>
        ///     AsBuiltinの処理を行う。
        /// </summary>
        public void AsBuiltin(string conventionalTypesOverride, string msg, Action<string> onSave)
        {
            ConventionalTypesOverride = conventionalTypesOverride;

            _onSave = onSave;
            _shouldExitApp = false;

            Editor.CommitMessage = msg;
        }

        /// <summary>
        ///     ウィンドウが閉じられた後の処理。
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (_shouldExitApp)
                App.Quit(_exitCode);
        }

        /// <summary>
        ///     SaveAndCloseの処理を行う。
        /// </summary>
        private void SaveAndClose(object _1, RoutedEventArgs _2)
        {
            _onSave?.Invoke(Editor.CommitMessage);
            Close();
        }

        /// <summary>
        ///     CancelAndCloseの処理を行う。
        /// </summary>
        private void CancelAndClose(object _1, RoutedEventArgs _2)
        {
            _exitCode = -1;
            Close();
        }

        private Action<string> _onSave = null;
        private bool _shouldExitApp = true;
        private int _exitCode = 0;
    }
}
