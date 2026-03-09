using Avalonia.Interactivity;

namespace Komorebi.Views
{
    /// <summary>
    ///     新規リモートブランチへのプッシュダイアログのコードビハインド。
    /// </summary>
    public partial class PushToNewBranch : ChromelessWindow
    {
        /// <summary>
        ///     コンストラクタ。コンポーネントを初期化する。
        /// </summary>
        public PushToNewBranch()
        {
            CloseOnESC = true;
            InitializeComponent();
        }

        /// <summary>
        ///     SetRemoteの処理を行う。
        /// </summary>
        public void SetRemote(string remote)
        {
            TxtPrefix.Text = remote;
        }

        /// <summary>
        ///     Sureイベントのハンドラ。
        /// </summary>
        private void OnSure(object _1, RoutedEventArgs _2)
        {
            Close(TxtName.Text);
        }

        /// <summary>
        ///     Cancelイベントのハンドラ。
        /// </summary>
        private void OnCancel(object _1, RoutedEventArgs _2)
        {
            Close(string.Empty);
        }
    }
}
