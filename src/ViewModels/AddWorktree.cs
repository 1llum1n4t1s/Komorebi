using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// ワークツリー追加ダイアログのViewModel。
/// git worktree addコマンドで新しいワークツリーを作成する。
/// </summary>
public class AddWorktree : Popup
{
    /// <summary>
    /// ワークツリーのパス。必須入力でパスの妥当性チェック付き。
    /// </summary>
    [Required(ErrorMessage = "Worktree path is required!")]
    [CustomValidation(typeof(AddWorktree), nameof(ValidateWorktreePath))]
    public string Path
    {
        get => _path;
        set => SetProperty(ref _path, value, true);
    }

    /// <summary>
    /// 新しいブランチを作成するかどうかのフラグ。
    /// 切り替え時に選択ブランチをリセットする。
    /// </summary>
    public bool CreateNewBranch
    {
        get => _createNewBranch;
        set
        {
            if (SetProperty(ref _createNewBranch, value, true))
            {
                // 新規ブランチ作成時は選択をクリア、既存ブランチ使用時は先頭を選択する
                if (value)
                    SelectedBranch = string.Empty;
                else
                    SelectedBranch = LocalBranches.Count > 0 ? LocalBranches[0] : string.Empty;
            }
        }
    }

    /// <summary>
    /// ローカルブランチ名のリスト。
    /// </summary>
    public List<string> LocalBranches
    {
        get;
        private set;
    }

    /// <summary>
    /// 選択されたブランチ名。
    /// </summary>
    public string SelectedBranch
    {
        get => _selectedBranch;
        set => SetProperty(ref _selectedBranch, value);
    }

    /// <summary>
    /// トラッキングブランチを設定するかどうかのフラグ。
    /// 有効にするとトラッキングブランチを自動選択する。
    /// </summary>
    public bool SetTrackingBranch
    {
        get => _setTrackingBranch;
        set
        {
            if (SetProperty(ref _setTrackingBranch, value))
                // トラッキングブランチの自動選択を実行する
                AutoSelectTrackingBranch();
        }
    }

    /// <summary>
    /// リモートブランチのリスト。
    /// </summary>
    public List<Models.Branch> RemoteBranches
    {
        get;
        private set;
    }

    /// <summary>
    /// 選択されたトラッキングブランチ。
    /// </summary>
    public Models.Branch SelectedTrackingBranch
    {
        get => _selectedTrackingBranch;
        set => SetProperty(ref _selectedTrackingBranch, value);
    }

    /// <summary>
    /// コンストラクタ。リポジトリのブランチ一覧からローカル・リモートを分類して初期化する。
    /// </summary>
    /// <param name="repo">対象のリポジトリViewModel</param>
    public AddWorktree(Repository repo)
    {
        _repo = repo;

        // ブランチ一覧をローカルとリモートに振り分ける
        LocalBranches = [];
        RemoteBranches = [];
        foreach (var branch in repo.Branches)
        {
            if (branch.IsLocal)
                LocalBranches.Add(branch.Name);
            else
                RemoteBranches.Add(branch);
        }
    }

    /// <summary>
    /// ワークツリーパスが空でなく、指定ディレクトリが空であることを検証する。
    /// </summary>
    /// <param name="path">検証するパス</param>
    /// <param name="ctx">バリデーションコンテキスト</param>
    /// <returns>バリデーション結果</returns>
    public static ValidationResult ValidateWorktreePath(string path, ValidationContext ctx)
    {
        if (ctx.ObjectInstance is not AddWorktree creator)
            return new ValidationResult("Missing runtime context to create branch!");

        if (string.IsNullOrEmpty(path))
            return new ValidationResult("Worktree path is required!");

        // 相対パスの場合はリポジトリのルートと結合してフルパスにする
        var fullPath = System.IO.Path.IsPathRooted(path) ? path : System.IO.Path.Combine(creator._repo.FullPath, path);
        var info = new DirectoryInfo(fullPath);
        if (info.Exists)
        {
            // ディレクトリが空でないか確認する
            var files = info.GetFiles();
            if (files.Length > 0)
                return new ValidationResult("Given path is not empty!!!");

            var folders = info.GetDirectories();
            if (folders.Length > 0)
                return new ValidationResult("Given path is not empty!!!");
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// 確定処理。git worktree addコマンドを実行してワークツリーを追加する。
    /// </summary>
    /// <returns>成功した場合はtrue</returns>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();
        ProgressDescription = App.Text("Progress.AddingWorktree");

        // ブランチ名とトラッキングブランチを取得する
        var branchName = _selectedBranch;
        var tracking = (_setTrackingBranch && _selectedTrackingBranch != null) ? _selectedTrackingBranch.FriendlyName : string.Empty;
        var log = _repo.CreateLog("Add Worktree");

        Use(log);

        // git worktree addコマンドを実行する
        var succ = await new Commands.Worktree(_repo.FullPath)
            .Use(log)
            .AddAsync(_path, branchName, _createNewBranch, tracking);

        log.Complete();
        return succ;
    }

    /// <summary>
    /// トラッキングブランチを自動的に選択する。
    /// 選択ブランチ名またはパス名に一致するリモートブランチを検索する。
    /// </summary>
    private void AutoSelectTrackingBranch()
    {
        if (!_setTrackingBranch || RemoteBranches.Count == 0)
            return;

        // ブランチ名またはパスのファイル名部分で一致するリモートブランチを探す
        var name = string.IsNullOrEmpty(_selectedBranch) ? System.IO.Path.GetFileName(_path.TrimEnd('/', '\\')) : _selectedBranch;
        var remoteBranch = RemoteBranches.Find(b => b.Name.EndsWith(name, StringComparison.Ordinal));
        if (remoteBranch == null)
            remoteBranch = RemoteBranches[0];

        SelectedTrackingBranch = remoteBranch;
    }

    /// <summary>対象リポジトリへの参照</summary>
    private Repository _repo = null;
    /// <summary>ワークツリーのパス</summary>
    private string _path = string.Empty;
    /// <summary>新規ブランチを作成するかどうか</summary>
    private bool _createNewBranch = true;
    /// <summary>選択されたブランチ名</summary>
    private string _selectedBranch = string.Empty;
    /// <summary>トラッキングブランチを設定するかどうか</summary>
    private bool _setTrackingBranch = false;
    /// <summary>選択されたトラッキングブランチ</summary>
    private Models.Branch _selectedTrackingBranch = null;
}
