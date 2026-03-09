using Avalonia.Interactivity;

namespace Komorebi.Views
{
    /// <summary>
    ///     空コミット確認ダイアログのコードビハインド。
    /// </summary>
    public partial class ConfirmEmptyCommit : ChromelessWindow
    {
        /// <summary>
        ///     コンストラクタ。コンポーネントを初期化する。
        /// </summary>
        public ConfirmEmptyCommit()
        {
            InitializeComponent();
        }

        /// <summary>
        ///     StageAllThenCommitの処理を行う。
        /// </summary>
        private void StageAllThenCommit(object _1, RoutedEventArgs _2)
        {
            Close(Models.ConfirmEmptyCommitResult.StageAllAndCommit);
        }

        /// <summary>
        ///     Continueの処理を行う。
        /// </summary>
        private void Continue(object _1, RoutedEventArgs _2)
        {
            Close(Models.ConfirmEmptyCommitResult.CreateEmptyCommit);
        }

        /// <summary>
        ///     CloseWindowの処理を行う。
        /// </summary>
        private void CloseWindow(object _1, RoutedEventArgs _2)
        {
            Close(Models.ConfirmEmptyCommitResult.Cancel);
        }
    }
}
