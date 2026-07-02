using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// 2つのリビジョンの共通祖先（マージベース）を取得するgitコマンド。
/// git merge-base を実行する。マージ/リベース事前チェック機能で使用する。
/// </summary>
public partial class MergeBase : Command
{
    /// <summary>
    /// SHA形式（8〜64桁の16進文字列）を検証する正規表現。
    /// </summary>
    [GeneratedRegex(@"^[0-9a-f]{8,64}$")]
    private static partial Regex REG_HEX();

    /// <summary>
    /// MergeBaseコマンドを初期化する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    /// <param name="rev1">比較対象リビジョン1。</param>
    /// <param name="rev2">比較対象リビジョン2。</param>
    public MergeBase(string repo, string rev1, string rev2)
    {
        WorkingDirectory = repo;
        Context = repo;
        RaiseError = false;
        Args = $"merge-base {rev1} {rev2}";
    }

    /// <summary>
    /// マージベースのSHAを取得する。
    /// コマンド失敗時、またはSHA形式として不正な場合は空文字列を返す。
    /// </summary>
    public async Task<string> GetResultAsync()
    {
        var rs = await ReadToEndAsync().ConfigureAwait(false);
        if (!rs.IsSuccess)
            return string.Empty;

        var trimmed = rs.StdOut.Trim();
        if (REG_HEX().IsMatch(trimmed))
            return trimmed;

        return string.Empty;
    }
}
