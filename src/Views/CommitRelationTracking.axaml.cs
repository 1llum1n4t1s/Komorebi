using System.Threading.Tasks;
using Avalonia.Controls;

namespace Komorebi.Views;

/// <summary>
/// コミット間の関連追跡ビューのコードビハインド。
/// </summary>
public partial class CommitRelationTracking : UserControl
{
    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public CommitRelationTracking()
    {
        InitializeComponent();
    }

    /// <summary>
    /// SetDataAsyncの処理を行う。
    /// </summary>
    public async Task SetDataAsync(ViewModels.CommitDetail detail)
    {
        LoadingIcon.IsVisible = true;
        var containsIn = await detail.GetRefsContainsThisCommitAsync();
        Container.ItemsSource = containsIn;
        LoadingIcon.IsVisible = false;
    }
}
