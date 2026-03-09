namespace Komorebi.Views
{
    /// <summary>
    ///     ホットキー一覧表示ダイアログのコードビハインド。
    /// </summary>
    public partial class Hotkeys : ChromelessWindow
    {
        /// <summary>
        ///     コンストラクタ。コンポーネントを初期化する。
        /// </summary>
        public Hotkeys()
        {
            CloseOnESC = true;
            InitializeComponent();
        }
    }
}
