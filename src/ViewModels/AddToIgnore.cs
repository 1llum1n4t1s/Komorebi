using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// .gitignoreへのパターン追加ダイアログのViewModel。
/// 指定したパターンを選択した無視ファイルに書き込む。
/// </summary>
public class AddToIgnore : Popup
{
    /// <summary>
    /// 選択可能な保存先ファイルの一覧。
    /// </summary>
    public List<Models.GitIgnoreFile> StorageFiles
    {
        get;
    }

    /// <summary>
    /// 追加する無視パターン。必須入力。
    /// </summary>
    [Required(ErrorMessage = "Ignore pattern is required!")]
    public string Pattern
    {
        get => _pattern;
        set => SetProperty(ref _pattern, value, true);
    }

    /// <summary>
    /// パターンの保存先ファイル（.gitignore等）。必須選択。
    /// 選択に応じてパターンを保存先基準の相対形式に切り替える。
    /// </summary>
    [Required(ErrorMessage = "Storage file is required!!!")]
    public Models.GitIgnoreFile SelectedStorageFile
    {
        get => _selectedStorageFile;
        set
        {
            if (SetProperty(ref _selectedStorageFile, value, true))
                Pattern = value.Pattern;
        }
    }

    /// <summary>
    /// コンストラクタ。リポジトリと初期パターンを受け取って初期化する。
    /// </summary>
    /// <param name="repo">対象のリポジトリViewModel</param>
    /// <param name="pattern">初期の無視パターン</param>
    public AddToIgnore(Repository repo, string pattern)
    {
        _repo = repo;
        _pattern = pattern;

        // パターンに応じた保存先候補を構築し、先頭をデフォルト選択にする
        StorageFiles = Models.GitIgnoreFile.GetSupported(repo.FullPath, repo.GitDir, pattern);
        SelectedStorageFile = StorageFiles[0];
    }

    /// <summary>
    /// 確定処理。無視パターンを指定ファイルに追記する。
    /// </summary>
    /// <returns>常にtrue</returns>
    public override async Task<bool> Sure()
    {
        ProgressDescription = App.Text("Progress.AddingIgnoredFiles");

        // 保存先ファイルのフルパスを取得する
        var file = _selectedStorageFile.FullPath;

        // LockWatcher はファイル書き込み中だけ保持する。MarkWorkingCopyDirtyManually を
        // ロック保持中に呼ぶと FSW イベントの重複処理で UI 更新が消失する（Discard.cs パターン準拠）。
        using (_repo.LockWatcher())
        {
            if (!File.Exists(file))
            {
                // ファイルが存在しない場合は新規作成してパターンを書き込む
                await File.WriteAllLinesAsync(file!, [_pattern]);
            }
            else
            {
                // 既存ファイルの末尾に改行がない場合は空行を挿入してからパターンを追記する
                var org = await File.ReadAllTextAsync(file);
                if (!org.EndsWith('\n'))
                    await File.AppendAllLinesAsync(file, ["", _pattern]);
                else
                    await File.AppendAllLinesAsync(file, [_pattern]);
            }
        }

        // ワーキングコピーの状態を更新する（ロック解除後）
        _repo.MarkWorkingCopyDirtyManually();
        return true;
    }

    /// <summary>対象リポジトリへの参照</summary>
    private readonly Repository _repo;
    /// <summary>無視パターン文字列</summary>
    private string _pattern;
    /// <summary>選択中の保存先ファイル</summary>
    private Models.GitIgnoreFile _selectedStorageFile;
}
