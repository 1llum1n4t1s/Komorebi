namespace Komorebi.Views
{
    /// <summary>
    ///     リポジトリ統計情報（コミット数・貢献者グラフ等）ビューのコードビハインド。
    /// </summary>
    public partial class Statistics : ChromelessWindow
    {
        /// <summary>
        ///     コンストラクタ。コンポーネントを初期化する。
        /// </summary>
        public Statistics()
        {
            CloseOnESC = true;
            InitializeComponent();
        }
    }
}
