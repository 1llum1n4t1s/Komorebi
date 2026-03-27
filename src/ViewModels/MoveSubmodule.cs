using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// サブモジュールを新しいパスに移動するダイアログのViewModel。
/// git mvコマンドを使用してサブモジュールのパスを変更する。
/// </summary>
public class MoveSubmodule : Popup
{
    /// <summary>
    /// 移動対象のサブモジュール。
    /// </summary>
    public Models.Submodule Submodule
    {
        get;
    }

    /// <summary>
    /// 移動先のパス（必須入力、バリデーション付き）。
    /// </summary>
    [Required(ErrorMessage = "Path is required!!!")]
    public string MoveTo
    {
        get => _moveTo;
        set => SetProperty(ref _moveTo, value, true);
    }

    /// <summary>
    /// リポジトリとサブモジュールを指定してダイアログを初期化する。
    /// 移動先パスの初期値は現在のサブモジュールパス。
    /// </summary>
    public MoveSubmodule(Repository repo, Models.Submodule submodule)
    {
        _repo = repo;
        _moveTo = submodule.Path;
        Submodule = submodule;
    }

    /// <summary>
    /// サブモジュールの移動を実行する。
    /// パスが変更されていない場合は何もせず成功を返す。
    /// </summary>
    public override async Task<bool> Sure()
    {
        ProgressDescription = App.Text("Progress.MovingSubmodule");

        // 旧パスと新パスの絶対パスを取得
        var oldPath = Native.OS.GetAbsPath(_repo.FullPath, Submodule.Path);
        var newPath = Native.OS.GetAbsPath(_repo.FullPath, _moveTo);
        // パスが変更されていない場合はスキップ
        if (oldPath.Equals(newPath, StringComparison.Ordinal))
            return true;

        using var lockWatcher = _repo.LockWatcher();
        var log = _repo.CreateLog("Move Submodule");
        Use(log);

        // git mvコマンドでサブモジュールを移動
        var succ = await new Commands.Move(_repo.FullPath, oldPath, newPath, false)
            .Use(log)
            .ExecAsync();

        log.Complete();
        return succ;
    }

    /// <summary>対象リポジトリ</summary>
    private Repository _repo;
    /// <summary>移動先パス</summary>
    private string _moveTo;
}
