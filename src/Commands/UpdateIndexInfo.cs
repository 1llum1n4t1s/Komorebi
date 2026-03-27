using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
///     git update-index --index-info を使用してインデックスを更新するクラス。
///     amend時にステージング情報を復元するために使用する。
/// </summary>
public class UpdateIndexInfo
{
    /// <summary>
    ///     コンストラクタ。変更リストからインデックス更新用のパッチデータを構築する。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    /// <param name="changes">インデックスに反映する変更リスト</param>
    public UpdateIndexInfo(string repo, List<Models.Change> changes)
    {
        _repo = repo;

        foreach (var c in changes)
        {
            if (c.Index == Models.ChangeState.Renamed)
            {
                // リネーム: 新パスを削除（ゼロハッシュ）し、元パスを復元
                _patchBuilder.Append("0 0000000000000000000000000000000000000000\t");
                _patchBuilder.Append(c.Path);
                _patchBuilder.Append("\n100644 ");
                _patchBuilder.Append(c.DataForAmend.ObjectHash);
                _patchBuilder.Append("\t");
                _patchBuilder.Append(c.OriginalPath);
            }
            else if (c.Index == Models.ChangeState.Added)
            {
                // 追加: ゼロハッシュでインデックスから削除（追加を取り消す）
                _patchBuilder.Append("0 0000000000000000000000000000000000000000\t");
                _patchBuilder.Append(c.Path);
            }
            else if (c.Index == Models.ChangeState.Deleted)
            {
                // 削除: 元のオブジェクトハッシュでインデックスに復元
                _patchBuilder.Append("100644 ");
                _patchBuilder.Append(c.DataForAmend.ObjectHash);
                _patchBuilder.Append("\t");
                _patchBuilder.Append(c.Path);
            }
            else
            {
                // 変更・タイプ変更等: 元のファイルモードとオブジェクトハッシュで復元
                _patchBuilder.Append(c.DataForAmend.FileMode);
                _patchBuilder.Append(" ");
                _patchBuilder.Append(c.DataForAmend.ObjectHash);
                _patchBuilder.Append("\t");
                _patchBuilder.Append(c.Path);
            }

            _patchBuilder.Append('\n');
        }
    }

    /// <summary>
    ///     パッチデータを標準入力経由で git update-index に渡して実行する。
    /// </summary>
    /// <returns>成功時true</returns>
    public async Task<bool> ExecAsync()
    {
        var starter = new ProcessStartInfo();
        starter.WorkingDirectory = _repo;
        starter.FileName = Native.OS.GitExecutable;
        starter.Arguments = "-c core.editor=true update-index --index-info";
        starter.UseShellExecute = false;
        starter.CreateNoWindow = true;
        starter.WindowStyle = ProcessWindowStyle.Hidden;
        starter.RedirectStandardInput = true;
        starter.RedirectStandardOutput = false;
        starter.RedirectStandardError = true;
        starter.StandardInputEncoding = new UTF8Encoding(false);
        starter.StandardErrorEncoding = Encoding.UTF8;

        try
        {
            using var proc = Process.Start(starter)!;
            await proc.StandardInput.WriteAsync(_patchBuilder.ToString());
            proc.StandardInput.Close();

            var err = await proc.StandardError.ReadToEndAsync().ConfigureAwait(false);
            await proc.WaitForExitAsync().ConfigureAwait(false);
            var rs = proc.ExitCode == 0;

            if (!rs)
                App.RaiseException(_repo, err);

            return rs;
        }
        catch (Exception e)
        {
            App.RaiseException(_repo, App.Text("Error.FailedToUpdateIndex", e.Message));
            return false;
        }
    }

    /// <summary>リポジトリのパス</summary>
    private readonly string _repo;
    /// <summary>update-index に渡すパッチデータのビルダー</summary>
    private readonly StringBuilder _patchBuilder = new();
}
