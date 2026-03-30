using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// HEADコミットのメッセージを修正するポップアップダイアログのViewModel。
/// git commit --amend を使用してコミットメッセージのみを変更する。
/// </summary>
public class Reword : Popup
{
    /// <summary>
    /// 修正対象のHEADコミット。
    /// </summary>
    public Models.Commit Head
    {
        get;
    }

    /// <summary>
    /// 新しいコミットメッセージ。空文字は許可されない。
    /// </summary>
    [Required(ErrorMessage = "Commit message is required!!!")]
    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value, true);
    }

    /// <summary>
    /// コンストラクタ。リポジトリ、対象コミット、元のメッセージを受け取る。
    /// </summary>
    public Reword(Repository repo, Models.Commit head, string oldMessage)
    {
        _repo = repo;
        _oldMessage = oldMessage;
        _message = _oldMessage;
        Head = head;
    }

    /// <summary>
    /// コミットメッセージの修正を実行する。
    /// メッセージが変更されていない場合は何もしない。
    /// ステージされた変更がある場合は自動スタッシュを行い、amend後にポップする。
    /// </summary>
    public override async Task<bool> Sure()
    {
        // メッセージが変更されていなければスキップ
        if (string.Compare(_message, _oldMessage, StringComparison.Ordinal) == 0)
            return true;

        using var lockWatcher = _repo.LockWatcher();
        ProgressDescription = App.Text("Progress.EditingHeadMessage");

        var log = _repo.CreateLog("Reword HEAD");
        Use(log);

        var changes = await new Commands.QueryLocalChanges(_repo.FullPath, false).GetResultAsync();
        var signOff = _repo.UIStates.EnableSignOffForCommit;
        var noVerify = _repo.UIStates.NoVerifyOnCommit;
        var needAutoStash = false;
        var succ = false;

        // ステージされた変更があるか確認（amend時に巻き込まれないようスタッシュが必要）
        foreach (var c in changes)
        {
            if (c.Index != Models.ChangeState.None)
            {
                needAutoStash = true;
                break;
            }
        }

        // 自動スタッシュでステージされた変更を退避
        if (needAutoStash)
        {
            succ = await new Commands.Stash(_repo.FullPath)
                .Use(log)
                .PushAsync("REWORD_AUTO_STASH", false);
            if (!succ)
            {
                log.Complete();
                return false;
            }
        }

        succ = await new Commands.Commit(_repo.FullPath, _message, signOff, noVerify, true, false)
            .Use(log)
            .RunAsync();

        if (succ && needAutoStash)
            await new Commands.Stash(_repo.FullPath)
                .Use(log)
                .PopAsync("stash@{0}");

        log.Complete();
        return succ;
    }

    private readonly Repository _repo;
    private readonly string _oldMessage;
    private string _message;
}
