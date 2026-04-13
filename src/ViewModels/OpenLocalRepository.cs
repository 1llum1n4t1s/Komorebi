using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// ローカルリポジトリを開く／初期化するダイアログの ViewModel。
/// フォルダ選択後にgitリポジトリか判定し、該当すればノード登録してタブで開く。
/// gitリポジトリでなければInitダイアログにフォールバックする。
/// </summary>
public class OpenLocalRepository : Popup
{
    /// <summary>
    /// 対象リポジトリのフォルダパス。バリデーション付き。
    /// </summary>
    [Required(ErrorMessage = "Repository folder is required")]
    [CustomValidation(typeof(OpenLocalRepository), nameof(ValidateRepoPath))]
    public string RepoPath
    {
        get => _repoPath;
        set => SetProperty(ref _repoPath, value, true);
    }

    /// <summary>
    /// 追加先として選択可能なグループ一覧（非リポジトリノードをフラット化したもの）。
    /// </summary>
    public List<RepositoryNode> Groups
    {
        get;
    }

    /// <summary>
    /// 追加先として選択されているグループ。null の場合はルートに追加される。
    /// </summary>
    public RepositoryNode Group
    {
        get => _group;
        set => SetProperty(ref _group, value);
    }

    /// <summary>
    /// 選択可能なブックマーク（色）のインデックス一覧。
    /// </summary>
    public List<int> Bookmarks
    {
        get;
    }

    /// <summary>
    /// 選択されたブックマーク（色）のインデックス。
    /// </summary>
    public int Bookmark
    {
        get => _bookmark;
        set => SetProperty(ref _bookmark, value);
    }

    /// <summary>
    /// コンストラクタ。ページIDと追加先グループを受け取り、グループ／ブックマーク選択肢を構築する。
    /// </summary>
    /// <param name="pageId">操作を行うページのID</param>
    /// <param name="group">追加先グループ（null の場合は先頭グループを既定選択）</param>
    public OpenLocalRepository(string pageId, RepositoryNode group)
    {
        _pageId = pageId;
        _group = group;

        Groups = [];
        CollectGroups(Groups, Preferences.Instance.RepositoryNodes);
        if (Groups.Count > 0 && _group is null)
            Group = Groups[0];

        Bookmarks = [];
        for (var i = 0; i < Models.Bookmarks.Brushes.Length; i++)
            Bookmarks.Add(i);
    }

    /// <summary>
    /// フォルダパスが存在するかどうかのバリデーション。
    /// </summary>
    public static ValidationResult ValidateRepoPath(string folder, ValidationContext _)
    {
        if (!Directory.Exists(folder))
            return new ValidationResult("Given path can NOT be found");
        return ValidationResult.Success;
    }

    /// <summary>
    /// 確定処理。gitリポジトリ（ベア／通常）であればノード登録してタブで開く。
    /// リポジトリでない通常フォルダの場合は Init ダイアログにフォールバックする。
    /// </summary>
    public override async Task<bool> Sure()
    {
        // ベアリポジトリかどうかを判定する
        var isBare = await new Commands.IsBareRepository(_repoPath).GetResultAsync();
        var repoRoot = _repoPath;
        if (!isBare)
        {
            // 通常リポジトリならルートパスを取得する
            var test = await new Commands.QueryRepositoryRootPath(_repoPath).GetResultAsync();
            if (test.IsSuccess && !string.IsNullOrWhiteSpace(test.StdOut))
            {
                repoRoot = test.StdOut.Trim();
            }
            else
            {
                // gitリポジトリでない場合は Init ダイアログにフォールバックする
                var launcher = App.GetLauncher();
                foreach (var page in launcher.Pages)
                {
                    if (page.Node.Id.Equals(_pageId, StringComparison.Ordinal))
                    {
                        page.Popup = new Init(page.Node.Id, _repoPath, _group, test.StdErr);
                        break;
                    }
                }

                return false;
            }
        }

        // ノード登録してブックマークを反映し、ツリーをリフレッシュしてタブで開く
        var node = Preferences.Instance.FindOrAddNodeByRepositoryPath(repoRoot, _group, true);
        node.Bookmark = _bookmark;
        await node.UpdateStatusAsync(false, null);
        Welcome.Instance.Refresh();
        node.Open();
        return true;
    }

    /// <summary>
    /// リポジトリノードツリーを再帰的に探索し、グループノード（非リポジトリ）のみをフラットリストに収集する。
    /// </summary>
    private static void CollectGroups(List<RepositoryNode> outs, List<RepositoryNode> collections)
    {
        foreach (var node in collections)
        {
            if (!node.IsRepository)
            {
                outs.Add(node);
                CollectGroups(outs, node.SubNodes);
            }
        }
    }

    /// <summary>操作を行うページのID</summary>
    private string _pageId = string.Empty;
    /// <summary>対象リポジトリのフォルダパス</summary>
    private string _repoPath = string.Empty;
    /// <summary>追加先グループ</summary>
    private RepositoryNode _group = null;
    /// <summary>選択されたブックマーク色のインデックス</summary>
    private int _bookmark = 0;
}
