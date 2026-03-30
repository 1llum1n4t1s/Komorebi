using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// リポジトリノード（リポジトリまたはグループ）の編集ダイアログViewModel。
/// 名前とブックマーク色を変更できる。
/// </summary>
public class EditRepositoryNode : Popup
{
    /// <summary>
    /// 表示用のターゲット名（リポジトリならID、グループなら名前）。
    /// </summary>
    public string Target
    {
        get;
    }

    /// <summary>
    /// ノードのID。
    /// </summary>
    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    /// <summary>
    /// ノードの表示名。必須バリデーション付き。
    /// </summary>
    [Required(ErrorMessage = "Name is required!")]
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value, true);
    }

    /// <summary>
    /// 選択可能なブックマーク色のインデックスリスト。
    /// </summary>
    public List<int> Bookmarks
    {
        get;
    }

    /// <summary>
    /// 現在選択されているブックマーク色のインデックス。
    /// </summary>
    public int Bookmark
    {
        get => _bookmark;
        set => SetProperty(ref _bookmark, value);
    }

    /// <summary>
    /// リポジトリかグループかの区別（表示制御用）。
    /// </summary>
    public bool IsRepository
    {
        get => _isRepository;
        set => SetProperty(ref _isRepository, value);
    }

    /// <summary>
    /// コンストラクタ。編集対象のノードから初期値を設定し、ブックマーク色リストを初期化する。
    /// </summary>
    public EditRepositoryNode(RepositoryNode node)
    {
        _node = node;
        _id = node.Id;
        _name = node.Name;
        _isRepository = node.IsRepository;
        _bookmark = node.Bookmark;

        Target = node.IsRepository ? node.Id : node.Name;

        Bookmarks = [];
        for (var i = 0; i < Models.Bookmarks.Brushes.Length; i++)
            Bookmarks.Add(i);
    }

    /// <summary>
    /// ノード編集を実行する確認アクション。
    /// 名前変更時はソート順を再計算する。
    /// </summary>
    public override Task<bool> Sure()
    {
        // 名前が変更された場合はソートが必要
        bool needSort = _node.Name != _name;
        _node.Name = _name;
        _node.Bookmark = _bookmark;

        if (needSort)
        {
            Preferences.Instance.SortByRenamedNode(_node);
            Welcome.Instance.Refresh();
        }

        return Task.FromResult(true);
    }

    private RepositoryNode _node = null; // 編集対象ノード
    private string _id = null; // ノードID
    private string _name = null; // ノード名
    private bool _isRepository = false; // リポジトリかグループか
    private int _bookmark = 0; // ブックマーク色インデックス
}
