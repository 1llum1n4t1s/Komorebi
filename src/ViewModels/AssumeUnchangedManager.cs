using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Threading;

namespace Komorebi.ViewModels;

/// <summary>
/// 「変更なしとみなす」ファイルの管理ViewModel。
/// git update-index --assume-unchangedで設定されたファイルの一覧表示と解除を行う。
/// </summary>
public class AssumeUnchangedManager
{
    /// <summary>
    /// 「変更なしとみなす」設定がされているファイルパスのリスト。
    /// </summary>
    public AvaloniaList<string> Files { get; private set; }

    /// <summary>
    /// コンストラクタ。リポジトリを受け取り、バックグラウンドでファイル一覧を取得する。
    /// </summary>
    /// <param name="repo">対象のリポジトリViewModel</param>
    public AssumeUnchangedManager(Repository repo)
    {
        _repo = repo;
        Files = new AvaloniaList<string>();

        // バックグラウンドでassume-unchangedファイル一覧を取得し、UIスレッドでリストに追加する
        Task.Run(async () =>
        {
            var collect = await new Commands.QueryAssumeUnchangedFiles(_repo.FullPath)
                .GetResultAsync()
                .ConfigureAwait(false);
            Dispatcher.UIThread.Post(() => Files.AddRange(collect));
        });
    }

    /// <summary>
    /// 指定ファイルのassume-unchanged設定を解除する。
    /// </summary>
    /// <param name="file">設定を解除するファイルパス</param>
    public async Task RemoveAsync(string file)
    {
        if (!string.IsNullOrEmpty(file))
        {
            // コマンドログを作成する
            var log = _repo.CreateLog("Remove Assume Unchanged File");

            // git update-index --no-assume-unchangedコマンドで設定を解除する
            await new Commands.AssumeUnchanged(_repo.FullPath, file, false)
                .Use(log)
                .ExecAsync();

            log.Complete();
            // リストからも削除する
            Files.Remove(file);
        }
    }

    /// <summary>対象リポジトリへの参照</summary>
    private readonly Repository _repo;
}
