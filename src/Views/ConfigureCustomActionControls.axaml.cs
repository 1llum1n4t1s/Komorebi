namespace Komorebi.Views;

/// <summary>
/// カスタムアクション設定コントロールのコードビハインド。
/// </summary>
public partial class ConfigureCustomActionControls : ChromelessWindow
{
    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public ConfigureCustomActionControls()
    {
        CloseOnESC = true;
        InitializeComponent();
    }
}
