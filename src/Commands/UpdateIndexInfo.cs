using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// git update-index --index-info を使用してインデックスを更新するクラス。
/// amend時にステージング情報を復元するために使用する。
/// </summary>
public class UpdateIndexInfo
{
    /// <summary>
    /// コンストラクタ。変更リストからインデックス更新用のパッチデータを構築する。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    /// <param name="changes">インデックスに反映する変更リスト</param>
    public UpdateIndexInfo(string repo, List<Models.Change> changes)
    {
        _repo = repo;

        foreach (var c in changes)
        {
            if (c.Index == Models.ChangeState.Added)
            {
                // 追加: ゼロハッシュでインデックスから削除（追加を取り消す）。親側のデータは不要。
                _patchBuilder.Append("0 0000000000000000000000000000000000000000\t");
                _patchBuilder.Append(c.Path);
                _patchBuilder.Append('\n');
                continue;
            }

            // 追加の取り消し以外は親コミットの mode / hash が必須。amend 切替直後などで
            // ステージド一覧がまだ非 amend のもの（DataForAmend が null）だと復元できないため、
            // index を半端に書き換えず操作全体を中止する。
            var amend = c.DataForAmend;
            if (amend is null)
            {
                _hasUnrestorableEntry = true;
                _patchBuilder.Clear();
                return;
            }

            // リネームのときだけ、先に新パスのエントリを削除してから元パスを復元する。
            if (c.Index == Models.ChangeState.Renamed)
            {
                _patchBuilder.Append("0 0000000000000000000000000000000000000000\t");
                _patchBuilder.Append(c.Path);
                _patchBuilder.Append('\n');
            }

            // 親コミット時点の mode と hash で index エントリを書き戻す。
            // upstream からの意図的な逸脱: upstream は削除・リネームの復元で mode を "100644" に
            // 決め打ちしており、実行可能ファイルを amend 中にアンステージすると実行ビットが落ちる。
            _patchBuilder.Append(amend.FileMode);
            _patchBuilder.Append(' ');
            _patchBuilder.Append(amend.ObjectHash);
            _patchBuilder.Append('\t');
            _patchBuilder.Append(c.Index == Models.ChangeState.Renamed ? c.OriginalPath : c.Path);
            _patchBuilder.Append('\n');
        }
    }

    /// <summary>
    /// git update-index へ渡すパッチ本文。復元される mode / hash を単体テストで検証するために公開する。
    /// </summary>
    internal string PatchContent => _patchBuilder.ToString();

    /// <summary>
    /// 復元元データを欠く変更が含まれていたかどうか。単体テスト用。
    /// </summary>
    internal bool HasUnrestorableEntry => _hasUnrestorableEntry;

    /// <summary>
    /// パッチデータを標準入力経由で git update-index に渡して実行する。
    /// </summary>
    /// <returns>成功時true</returns>
    public async Task<bool> ExecAsync()
    {
        // 復元元データを欠く変更が含まれていた場合は git を起動せずに失敗させる
        // （部分的に書き換えて index を壊すより、何もせず利用者に再試行させる方が安全）。
        if (_hasUnrestorableEntry)
        {
            App.RaiseException(_repo, App.Text("Error.FailedToUpdateIndex", "amend target metadata is not ready yet"));
            return false;
        }

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
    /// <summary>親コミットの mode / hash を持たない変更が含まれていたかどうか</summary>
    private readonly bool _hasUnrestorableEntry;
}
