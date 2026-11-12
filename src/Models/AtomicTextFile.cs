using System;
using System.IO;
using System.Threading.Tasks;

namespace Komorebi.Models;

/// <summary>
/// テキストを「一時ファイルへ全量書込み → File.Move で置換」する保存ヘルパー（RepositorySettings / RepositoryUIStates 用）。
///
/// 保証範囲: 書込み途中の例外・プロセスのクラッシュで、途中までの内容が本体ファイルを置き換えることはない
/// （同一ボリューム上の rename による置換）。fsync（FileStream.Flush(true)）は行わないため、
/// 電源断・OS クラッシュ時に書込み内容がディスクへ届いていることまでは保証しない。
///
/// 一時ファイル名は同一リポジトリを複数インスタンス・worktree から同時に保存しても衝突しないよう
/// Guid 付きにしている。そのため失敗時に残ると自動では再利用・上書きされず溜まり続けるので、
/// 書込み・置換のどちらで失敗しても一時ファイルを削除してから例外を呼び出し元へ戻す。
/// Preferences の .bak 退避など、保存処理ごとの固有の意味はここへ統合しない。
/// </summary>
internal static class AtomicTextFile
{
    /// <summary>同期版。失敗時は一時ファイルを削除して例外を再送出する。</summary>
    public static void WriteAllText(string file, string content)
    {
        var tmpfile = CreateTempPath(file);
        try
        {
            File.WriteAllText(tmpfile, content);
            File.Move(tmpfile, file, true);
        }
        catch
        {
            TryDelete(tmpfile);
            throw;
        }
    }

    /// <summary>非同期版。失敗時は一時ファイルを削除して例外を再送出する。</summary>
    public static async Task WriteAllTextAsync(string file, string content)
    {
        var tmpfile = CreateTempPath(file);
        try
        {
            await File.WriteAllTextAsync(tmpfile, content);
            File.Move(tmpfile, file, true);
        }
        catch
        {
            TryDelete(tmpfile);
            throw;
        }
    }

    /// <summary>本体と同じディレクトリ（＝同一ボリューム）にランダム拡張子付きの一時パスを作る。</summary>
    private static string CreateTempPath(string file) => $"{file}.tmp.{Guid.NewGuid():N}";

    /// <summary>後始末の削除失敗は元の例外を隠さないよう握りつぶす（呼び出し元は保存失敗として扱う）。</summary>
    private static void TryDelete(string tmpfile)
    {
        try
        {
            File.Delete(tmpfile);
        }
        catch
        {
            // 削除できない一時ファイルは残るが、元の保存失敗の通知を優先する
        }
    }
}
