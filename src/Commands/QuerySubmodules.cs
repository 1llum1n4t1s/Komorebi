using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// サブモジュールの状態を取得するクラス。
/// git submodule status でステータスを取得し、.gitmodulesからURL・ブランチ情報を補完する。
/// さらにNormalステータスのサブモジュールについてはローカル変更の有無も確認する。
/// </summary>
public partial class QuerySubmodules : Command
{
    /// <summary>サブモジュールステータス行を解析する正規表現（先頭記号、SHA、パス）</summary>
    [GeneratedRegex(@"^([U\-\+ ])([0-9a-f]+)\s(.*?)(\s\(.*\))?$")]
    private static partial Regex REG_FORMAT_STATUS();
    /// <summary>ローカル変更の有無を確認するための正規表現（porcelain出力）</summary>
    [GeneratedRegex(@"^\s?[\w\?]{1,4}\s+(.+)$")]
    private static partial Regex REG_FORMAT_DIRTY();
    /// <summary>.gitmodulesの設定行を解析する正規表現（モジュール名、キー、値）</summary>
    [GeneratedRegex(@"^submodule\.(\S*)\.(\w+)=(.*)$")]
    private static partial Regex REG_FORMAT_MODULE_INFO();

    /// <summary>
    /// コンストラクタ。サブモジュールステータスを取得するコマンドを設定する。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    public QuerySubmodules(string repo)
    {
        WorkingDirectory = repo;
        Context = repo;
        Args = "submodule status";
    }

    /// <summary>
    /// コマンドを非同期で実行し、サブモジュールのリストを返す。
    /// ステータス取得→.gitmodules解析→ローカル変更チェックの3段階で情報を収集する。
    /// </summary>
    /// <returns>サブモジュールモデルのリスト</returns>
    public async Task<List<Models.Submodule>> GetResultAsync()
    {
        var submodules = new List<Models.Submodule>();
        var rs = await ReadToEndAsync().ConfigureAwait(false);

        var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var map = new Dictionary<string, Models.Submodule>();
        var needCheckLocalChanges = false;
        foreach (var line in lines)
        {
            var match = REG_FORMAT_STATUS().Match(line);
            if (match.Success)
            {
                var stat = match.Groups[1].Value;
                var sha = match.Groups[2].Value;
                var path = match.Groups[3].Value;

                // 先頭文字でサブモジュールのステータスを判定
                var module = new Models.Submodule() { Path = path, SHA = sha };
                switch (stat[0])
                {
                    case '-': // 未初期化
                        module.Status = Models.SubmoduleStatus.NotInited;
                        break;
                    case '+': // リビジョン変更あり
                        module.Status = Models.SubmoduleStatus.RevisionChanged;
                        break;
                    case 'U': // マージ未解決
                        module.Status = Models.SubmoduleStatus.Unmerged;
                        break;
                    default: // 通常（ローカル変更チェックが必要）
                        module.Status = Models.SubmoduleStatus.Normal;
                        needCheckLocalChanges = true;
                        break;
                }

                map.Add(path, module);
                submodules.Add(module);
            }
        }

        // .gitmodulesからURL・ブランチ情報を取得してサブモジュールに設定
        if (submodules.Count > 0)
        {
            Args = "config --file .gitmodules --list";
            rs = await ReadToEndAsync().ConfigureAwait(false);
            if (rs.IsSuccess)
            {
                var modules = new Dictionary<string, ModuleInfo>();
                lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    var match = REG_FORMAT_MODULE_INFO().Match(line);
                    if (match.Success)
                    {
                        var name = match.Groups[1].Value;
                        var key = match.Groups[2].Value;
                        var val = match.Groups[3].Value;

                        if (!modules.TryGetValue(name, out var m))
                        {
                            // 名前のエイリアスを検索（パスが名前と一致するモジュールを探す）
                            foreach (var kv in modules)
                            {
                                if (kv.Value.Path.Equals(name, StringComparison.Ordinal))
                                {
                                    m = kv.Value;
                                    break;
                                }
                            }

                            if (m is null)
                            {
                                m = new ModuleInfo();
                                modules.Add(name, m);
                            }
                        }

                        if (key.Equals("path", StringComparison.Ordinal))
                            m.Path = val;
                        else if (key.Equals("url", StringComparison.Ordinal))
                            m.URL = val;
                        else if (key.Equals("branch", StringComparison.Ordinal))
                            m.Branch = val;
                    }
                }

                foreach (var kv in modules)
                {
                    if (map.TryGetValue(kv.Value.Path, out var m))
                    {
                        m.URL = kv.Value.URL;
                        m.Branch = kv.Value.Branch;
                    }
                }
            }
        }

        // Normalステータスのサブモジュールについてローカル変更の有無を確認
        if (needCheckLocalChanges)
        {
            var builder = new StringBuilder();
            foreach (var kv in map)
            {
                if (kv.Value.Status == Models.SubmoduleStatus.Normal)
                    builder.Append(kv.Key.Quoted()).Append(' ');
            }

            Args = $"--no-optional-locks status --porcelain -- {builder}";
            rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
                return submodules;

            lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var match = REG_FORMAT_DIRTY().Match(line);
                if (match.Success)
                {
                    var path = match.Groups[1].Value;
                    if (map.TryGetValue(path, out var m))
                        m.Status = Models.SubmoduleStatus.Modified;
                }
            }
        }

        return submodules;
    }

    /// <summary>
    /// .gitmodulesから解析されたモジュール情報を保持する内部クラス。
    /// </summary>
    private class ModuleInfo
    {
        /// <summary>サブモジュールのパス</summary>
        public string Path { get; set; } = string.Empty;
        /// <summary>サブモジュールのリモートURL</summary>
        public string URL { get; set; } = string.Empty;
        /// <summary>サブモジュールの追跡ブランチ</summary>
        public string Branch { get; set; } = "HEAD";
    }
}
