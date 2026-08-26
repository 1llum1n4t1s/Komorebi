// nullable 移行未実施。1 ファイルずつ null 注釈を入れてこの 2 行を削除していく。
#nullable disable warnings
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// git statusコマンドを実行して、ローカルの変更一覧（ステージング済み・未ステージング・未追跡）を取得するクラス。
/// porcelain形式で出力を解析する。
/// </summary>
public partial class QueryLocalChanges : Command
{
    /// <summary>
    /// porcelain形式のstatus出力行を解析する正規表現。ステータスコードとファイルパスを抽出する。
    /// </summary>
    [GeneratedRegex(@"^(\s?[\w\?]{1,4})\s+(.+)$")]
    internal static partial Regex REG_FORMAT();

    /// <summary>
    /// コンストラクタ。ローカル変更を取得するstatusコマンドを設定する。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    /// <param name="includeUntracked">未追跡ファイルを含めるかどうか</param>
    /// <param name="noOptionalLocks">オプショナルロックを無効にするかどうか</param>
    public QueryLocalChanges(string repo, bool includeUntracked = true, bool noOptionalLocks = true)
    {
        WorkingDirectory = repo;
        Context = repo;

        var builder = new StringBuilder();
        if (noOptionalLocks)
            builder.Append("--no-optional-locks ");
        // Komorebi 独自修正: C 形式の quoted path を再解釈せず、Git 推奨の NUL 形式で取得する。
        if (includeUntracked)
            builder.Append("-c core.untrackedCache=true -c status.showUntrackedFiles=all status -uall --ignore-submodules=dirty --porcelain -z");
        else
            builder.Append("status -uno --ignore-submodules=dirty --porcelain -z");

        Args = builder.ToString();
    }

    /// <summary>
    /// コマンドを非同期で実行し、ローカル変更のリストを返す。
    /// </summary>
    /// <returns>変更モデルのリスト</returns>
    public async Task<List<Models.Change>> GetResultAsync()
    {
        try
        {
            using var proc = new Process();
            proc.StartInfo = CreateGitStartInfo(true);
            proc.Start();
            var stderrDrain = DrainReaderAsync(proc.StandardError);
            var stdout = proc.StandardOutput.ReadToEndAsync();

            await proc.WaitForExitAsync().ConfigureAwait(false);
            await Task.WhenAll(stdout, stderrDrain).ConfigureAwait(false);
            return ParseOutput(stdout.Result);
        }
        catch
        {
            // Ignore exceptions.
        }

        return [];
    }

    /// <summary>NUL 区切りの porcelain v1 出力を解析する。</summary>
    internal static List<Models.Change> ParseOutput(string output)
    {
        List<Models.Change> changes = [];
        if (string.IsNullOrEmpty(output))
            return changes;

        var records = output.Split('\0');
        for (var i = 0; i < records.Length; i++)
        {
            var record = records[i];
            if (string.IsNullOrEmpty(record))
                continue;

            var change = ParseRecord(record);
            if (change is null)
                continue;

            // porcelain v1 -z の rename/copy は「新パス NUL 元パス NUL」の順になる。
            var status = record.Length >= 2 ? record[..2] : string.Empty;
            if ((status.Contains('R') || status.Contains('C')) && i + 1 < records.Length)
                change.OriginalPath = records[++i];

            changes.Add(change);
        }

        return changes;
    }

    /// <summary>
    /// porcelain形式の1行を解析して、変更モデルを生成する。
    /// ステータスコード（インデックス状態+ワークツリー状態）からChangeStateを設定する。
    /// </summary>
    /// <param name="line">porcelain形式のstatus出力行</param>
    /// <returns>変更モデル。無効な行の場合はnull</returns>
    internal static Models.Change ParseLine(string line)
    {
        var match = REG_FORMAT().Match(line);
        if (!match.Success)
            return null;

        return ParseStatus(match.Groups[1].Value, match.Groups[2].Value);
    }

    private static Models.Change ParseRecord(string record)
    {
        if (record.Length < 4 || record[2] != ' ')
            return null;

        return ParseStatus(record[..2], record[3..]);
    }

    private static Models.Change ParseStatus(string status, string path)
    {
        status = status.TrimEnd();
        var change = new Models.Change() { Path = path };

        switch (status)
        {
            case " M":
                change.Set(Models.ChangeState.None, Models.ChangeState.Modified);
                break;
            case " T":
                change.Set(Models.ChangeState.None, Models.ChangeState.TypeChanged);
                break;
            case " A":
                change.Set(Models.ChangeState.None, Models.ChangeState.Added);
                break;
            case " D":
                change.Set(Models.ChangeState.None, Models.ChangeState.Deleted);
                break;
            case " R":
                change.Set(Models.ChangeState.None, Models.ChangeState.Renamed);
                break;
            case " C":
                change.Set(Models.ChangeState.None, Models.ChangeState.Copied);
                break;
            case "M":
                change.Set(Models.ChangeState.Modified);
                break;
            case "MM":
                change.Set(Models.ChangeState.Modified, Models.ChangeState.Modified);
                break;
            case "MT":
                change.Set(Models.ChangeState.Modified, Models.ChangeState.TypeChanged);
                break;
            case "MD":
                change.Set(Models.ChangeState.Modified, Models.ChangeState.Deleted);
                break;
            case "T":
                change.Set(Models.ChangeState.TypeChanged);
                break;
            case "TM":
                change.Set(Models.ChangeState.TypeChanged, Models.ChangeState.Modified);
                break;
            case "TT":
                change.Set(Models.ChangeState.TypeChanged, Models.ChangeState.TypeChanged);
                break;
            case "TD":
                change.Set(Models.ChangeState.TypeChanged, Models.ChangeState.Deleted);
                break;
            case "A":
                change.Set(Models.ChangeState.Added);
                break;
            case "AM":
                change.Set(Models.ChangeState.Added, Models.ChangeState.Modified);
                break;
            case "AT":
                change.Set(Models.ChangeState.Added, Models.ChangeState.TypeChanged);
                break;
            case "AD":
                change.Set(Models.ChangeState.Added, Models.ChangeState.Deleted);
                break;
            case "D":
                change.Set(Models.ChangeState.Deleted);
                break;
            case "R":
                change.Set(Models.ChangeState.Renamed);
                break;
            case "RM":
                change.Set(Models.ChangeState.Renamed, Models.ChangeState.Modified);
                break;
            case "RT":
                change.Set(Models.ChangeState.Renamed, Models.ChangeState.TypeChanged);
                break;
            case "RD":
                change.Set(Models.ChangeState.Renamed, Models.ChangeState.Deleted);
                break;
            case "C":
                change.Set(Models.ChangeState.Copied);
                break;
            case "CM":
                change.Set(Models.ChangeState.Copied, Models.ChangeState.Modified);
                break;
            case "CT":
                change.Set(Models.ChangeState.Copied, Models.ChangeState.TypeChanged);
                break;
            case "CD":
                change.Set(Models.ChangeState.Copied, Models.ChangeState.Deleted);
                break;
            case "DD":
                change.ConflictReason = Models.ConflictReason.BothDeleted;
                change.Set(Models.ChangeState.None, Models.ChangeState.Conflicted);
                break;
            case "AU":
                change.ConflictReason = Models.ConflictReason.AddedByUs;
                change.Set(Models.ChangeState.None, Models.ChangeState.Conflicted);
                break;
            case "UD":
                change.ConflictReason = Models.ConflictReason.DeletedByThem;
                change.Set(Models.ChangeState.None, Models.ChangeState.Conflicted);
                break;
            case "UA":
                change.ConflictReason = Models.ConflictReason.AddedByThem;
                change.Set(Models.ChangeState.None, Models.ChangeState.Conflicted);
                break;
            case "DU":
                change.ConflictReason = Models.ConflictReason.DeletedByUs;
                change.Set(Models.ChangeState.None, Models.ChangeState.Conflicted);
                break;
            case "AA":
                change.ConflictReason = Models.ConflictReason.BothAdded;
                change.Set(Models.ChangeState.None, Models.ChangeState.Conflicted);
                break;
            case "UU":
                change.ConflictReason = Models.ConflictReason.BothModified;
                change.Set(Models.ChangeState.None, Models.ChangeState.Conflicted);
                break;
            case "??":
                change.Set(Models.ChangeState.None, Models.ChangeState.Untracked);
                break;
        }

        if (change.Index != Models.ChangeState.None || change.WorkTree != Models.ChangeState.None)
            return change;

        return null;
    }
}
