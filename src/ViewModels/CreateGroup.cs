using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
///     リポジトリグループ作成ダイアログのViewModel。
///     サイドバーのリポジトリツリーに新しいグループノードを追加する。
/// </summary>
public class CreateGroup : Popup
{
    /// <summary>
    ///     グループ名。必須項目。
    /// </summary>
    [Required(ErrorMessage = "Group name is required!")]
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value, true);
    }

    /// <summary>
    ///     コンストラクタ。親ノードを受け取って初期化する。
    /// </summary>
    /// <param name="parent">グループを追加する親ノード（nullでルート直下）</param>
    public CreateGroup(RepositoryNode parent)
    {
        _parent = parent;
    }

    /// <summary>
    ///     確定処理。新しいグループノードを設定に追加する。
    /// </summary>
    /// <returns>常にtrue</returns>
    public override Task<bool> Sure()
    {
        // 新しいグループノードを作成して設定に追加する
        Preferences.Instance.AddNode(new RepositoryNode()
        {
            Id = Guid.NewGuid().ToString(),
            Name = _name,
            IsRepository = false,
            IsExpanded = false,
        }, _parent, true);

        // ウェルカムページのリポジトリ一覧を更新する
        Welcome.Instance.Refresh();
        return Task.FromResult(true);
    }

    /// <summary>グループを追加する親ノード</summary>
    private readonly RepositoryNode _parent = null;
    /// <summary>グループ名</summary>
    private string _name = string.Empty;
}
