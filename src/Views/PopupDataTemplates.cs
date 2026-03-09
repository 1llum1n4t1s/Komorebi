using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Komorebi.Views
{
    /// <summary>
    ///     ポップアップダイアログのViewModel→Viewマッピングを管理するデータテンプレートクラス。
    /// </summary>
    public class PopupDataTemplates : IDataTemplate
    {
        /// <summary>
        ///     Buildの処理を行う。
        /// </summary>
        public Control Build(object param) => App.CreateViewForViewModel(param);
        /// <summary>
        ///     Matchの処理を行う。
        /// </summary>
        public bool Match(object data) => data is ViewModels.Popup;
    }
}
