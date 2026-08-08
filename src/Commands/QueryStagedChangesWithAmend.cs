using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Komorebi.Commands;

/// <summary>
/// amend時にステージング済み変更のファイルモード・オブジェクトハッシュを含む詳細情報を取得するクラス。
/// git diff-index --cached -M を使用する。取得した情報はUpdateIndexInfoで復元に使用される。
/// </summary>
public partial class QueryStagedChangesWithAmend : Command
{
    // git diff-index の raw 出力は `:src-mode dst-mode src-sha dst-sha status\tpath` の順。
    // src 側が「親コミットの状態」＝ amend 中のアンステージで復元すべき状態なので、
    // mode / sha ともに src 側（1 番目と 3 番目）を捕捉する。
    // upstream からの意図的な逸脱: upstream は dst-mode（2 番目）を捕捉しており、mode を変更した
    // ファイルを amend 中にアンステージすると「親の内容 + 新しい mode」という壊れた index になる。

    /// <summary>追加・削除・変更・タイプ変更（A/D/M/T）のdiff-index出力行を解析する正規表現</summary>
    [GeneratedRegex(@"^:([\d]{6}) [\d]{6} ([0-9a-f]{40}) [0-9a-f]{40} ([ADMT])\d{0,6}\t(.*)$")]
    private static partial Regex REG_FORMAT1();
    /// <summary>リネーム・コピー（R/C）のdiff-index出力行を解析する正規表現（タブ区切りで旧パスと新パスを含む）</summary>
    [GeneratedRegex(@"^:([\d]{6}) [\d]{6} ([0-9a-f]{40}) [0-9a-f]{40} ([RC])\d{0,6}\t(.*\t.*)$")]
    private static partial Regex REG_FORMAT2();

    /// <summary>
    /// コンストラクタ。親コミットとの差分からステージング済み変更の詳細を取得するコマンドを設定する。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    /// <param name="parent">比較対象の親コミットSHA</param>
    public QueryStagedChangesWithAmend(string repo, string parent)
    {
        WorkingDirectory = repo;
        Context = repo;
        Args = $"diff-index --cached -M {parent}";
        _parent = parent;
    }

    /// <summary>
    /// コマンドを同期的に実行し、amend用データ付きの変更リストを返す。
    /// </summary>
    /// <returns>ファイルモード・オブジェクトハッシュ付きの変更モデルリスト</returns>
    public List<Models.Change> GetResult()
    {
        var rs = ReadToEnd();
        if (!rs.IsSuccess)
            return [];

        List<Models.Change> changes = [];
        var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var change = ParseLine(line, _parent);
            if (change is not null)
                changes.Add(change);
        }

        return changes;
    }

    /// <summary>
    /// diff-index の raw 出力 1 行を解析して変更モデルを生成する。
    /// <see cref="QueryLocalChanges.ParseLine"/> と同じく、パース部分を単体テスト可能にするため
    /// コマンド実行から切り離した static メソッドにしている。
    /// </summary>
    /// <param name="line">diff-index の raw 出力 1 行</param>
    /// <param name="parent">比較対象の親コミットSHA</param>
    /// <returns>変更モデル。解析できない行の場合はnull</returns>
    internal static Models.Change? ParseLine(string line, string parent)
    {
        // まずリネーム/コピー用の正規表現で試行（タブ区切りパスを含む）
        var match = REG_FORMAT2().Match(line);
        if (match.Success)
        {
            var renamed = NewChange(match, parent);
            renamed.Set(match.Groups[3].Value == "R" ? Models.ChangeState.Renamed : Models.ChangeState.Copied);
            return renamed;
        }

        // 通常の変更（A/D/M/T）用の正規表現で試行
        match = REG_FORMAT1().Match(line);
        if (!match.Success)
            return null;

        var change = NewChange(match, parent);

        // 変更種別に応じてChangeStateを設定
        switch (match.Groups[3].Value)
        {
            case "A":
                change.Set(Models.ChangeState.Added);
                break;
            case "D":
                change.Set(Models.ChangeState.Deleted);
                break;
            case "M":
                change.Set(Models.ChangeState.Modified);
                break;
            case "T":
                change.Set(Models.ChangeState.TypeChanged);
                break;
        }

        return change;
    }

    /// <summary>正規表現のマッチ結果から、amend 用メタデータ付きの変更モデルを組み立てる。</summary>
    private static Models.Change NewChange(Match match, string parent)
    {
        return new Models.Change()
        {
            Path = match.Groups[4].Value,
            DataForAmend = new Models.ChangeDataForAmend()
            {
                FileMode = match.Groups[1].Value,
                ObjectHash = match.Groups[2].Value,
                ParentSHA = parent,
            },
        };
    }

    /// <summary>比較対象の親コミットSHA</summary>
    private readonly string _parent;
}
