using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
///     更新可能なサブモジュールの一覧を取得するクラス。
///     git submodule status を使用して、変更または未初期化のサブモジュールを検出する。
/// </summary>
public partial class QueryUpdatableSubmodules : Command
{
    /// <summary>サブモジュールステータス行を解析する正規表現（ステータス記号、SHA、パス）</summary>
    [GeneratedRegex(@"^([\-\+])([0-9a-f]+)\s(.*?)(\s\(.*\))?$")]
    private static partial Regex REG_FORMAT_STATUS();

    /// <summary>
    ///     コンストラクタ。更新可能なサブモジュールを取得するコマンドを設定する。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    /// <param name="includeUninited">未初期化のサブモジュールも含めるかどうか</param>
    public QueryUpdatableSubmodules(string repo, bool includeUninited)
    {
        WorkingDirectory = repo;
        Context = repo;
        Args = "submodule status";

        _includeUninited = includeUninited;
    }

    /// <summary>
    ///     コマンドを非同期で実行し、更新可能なサブモジュールのパスリストを返す。
    /// </summary>
    /// <returns>サブモジュールパスのリスト</returns>
    public async Task<List<string>> GetResultAsync()
    {
        var submodules = new List<string>();
        var rs = await ReadToEndAsync().ConfigureAwait(false);

        var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var match = REG_FORMAT_STATUS().Match(line);
            if (match.Success)
            {
                var stat = match.Groups[1].Value;
                var path = match.Groups[3].Value;
                // '-' は未初期化を示す。includeUninitedがfalseの場合はスキップ
                if (!_includeUninited && stat.StartsWith('-'))
                    continue;

                submodules.Add(path);
            }
        }

        return submodules;
    }

    /// <summary>未初期化のサブモジュールも含めるかどうかのフラグ</summary>
    private bool _includeUninited = false;
}
