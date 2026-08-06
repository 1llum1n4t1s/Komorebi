// nullable 移行未実施。1 ファイルずつ null 注釈を入れてこの 2 行を削除していく。
#nullable disable warnings
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Komorebi.Views;

/// <summary>
/// コマンドパレット内の各種データテンプレートを管理するクラス。
/// </summary>
public class CommandPaletteDataTemplates : IDataTemplate
{
    /// <summary>
    /// Buildの処理を行う。
    /// </summary>
    public Control Build(object param) => App.CreateViewForViewModel(param);
    /// <summary>
    /// Matchの処理を行う。
    /// </summary>
    public bool Match(object data) => data is ViewModels.ICommandPalette;
}
