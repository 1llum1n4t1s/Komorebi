using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// gitの設定値を読み書きするコマンド群。
/// git config を実行し、ローカルまたはグローバル設定を操作する。
/// </summary>
public class Config : Command
{
    /// <summary>
    /// Configコマンドを初期化する。
    /// </summary>
    /// <param name="repository">リポジトリパス。空の場合はグローバル設定として動作する。</param>
    public Config(string repository)
    {
        if (string.IsNullOrEmpty(repository))
        {
            // リポジトリが指定されていない場合、グローバル設定を操作する
            WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        else
        {
            // リポジトリが指定されている場合、ローカル設定を操作する
            WorkingDirectory = repository;
            Context = repository;
            _isLocal = true;
        }
    }

    /// <summary>
    /// 全ての設定値を同期的に読み取る。
    /// git config -l を実行し、キーと値のペアを辞書として返す。
    /// </summary>
    /// <returns>設定キーと値の辞書。</returns>
    public Dictionary<string, string> ReadAll()
    {
        Args = "config -l";
        var output = ReadToEnd();
        return output.IsSuccess ? ParseConfigOutput(output.StdOut) : [];
    }

    /// <summary>
    /// 全ての設定値を非同期で読み取る。
    /// git config -l を実行し、キーと値のペアを辞書として返す。
    /// キーは大文字小文字を区別しない辞書で返される。
    /// </summary>
    /// <returns>設定キーと値の辞書（キーは大文字小文字を区別しない）。</returns>
    public async Task<Dictionary<string, string>> ReadAllAsync()
    {
        Args = "config -l";
        var output = await ReadToEndAsync().ConfigureAwait(false);
        // パフォーマンス: ToLower()による文字列割り当てを排除し、OrdinalIgnoreCase辞書で大文字小文字を吸収
        return output.IsSuccess ? ParseConfigOutput(output.StdOut, StringComparer.OrdinalIgnoreCase) : [];
    }

    /// <summary>
    /// git config -l の出力をキーと値の辞書にパースする共通ヘルパー。
    /// </summary>
    /// <param name="stdout">git configの標準出力。</param>
    /// <param name="comparer">辞書のキー比較子。nullの場合はデフォルト（大文字小文字区別あり）。</param>
    /// <returns>設定キーと値の辞書。</returns>
    private static Dictionary<string, string> ParseConfigOutput(string stdout, IEqualityComparer<string> comparer = null)
    {
        var rs = comparer is not null ? new Dictionary<string, string>(comparer) : new Dictionary<string, string>();
        var lines = stdout.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var parts = line.Split('=', 2);
            if (parts.Length == 2)
                rs[parts[0]] = parts[1];
        }
        return rs;
    }

    /// <summary>
    /// 特定のキーの設定値を同期的に取得する。
    /// git config &lt;key&gt; を実行する。
    /// </summary>
    /// <param name="key">設定キー名。</param>
    /// <returns>設定値の文字列。</returns>
    public string Get(string key)
    {
        // git config <key>: 指定キーの値を取得する
        Args = $"config {key}";
        return ReadToEnd().StdOut.Trim();
    }

    /// <summary>
    /// 特定のキーの設定値を非同期で取得する。
    /// git config &lt;key&gt; を実行する。
    /// </summary>
    /// <param name="key">設定キー名。</param>
    /// <returns>設定値の文字列。</returns>
    public async Task<string> GetAsync(string key)
    {
        // git config <key>: 指定キーの値を取得する
        Args = $"config {key}";

        var rs = await ReadToEndAsync().ConfigureAwait(false);
        return rs.StdOut.Trim();
    }

    /// <summary>
    /// 設定値を非同期で設定または削除する。
    /// git config [--local|--global] &lt;key&gt; &lt;value&gt; を実行する。
    /// </summary>
    /// <param name="key">設定キー名。</param>
    /// <param name="value">設定する値。空の場合はキーを削除する。</param>
    /// <param name="allowEmpty">空の値を許可するかどうか。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> SetAsync(string key, string value, bool allowEmpty = false)
    {
        // ローカルまたはグローバルのスコープを決定する
        var scope = _isLocal ? "--local" : "--global";

        if (!allowEmpty && string.IsNullOrWhiteSpace(value))
            // 値が空の場合はキーを削除する
            Args = $"config {scope} --unset {key}";
        else
            // 指定されたキーに値を設定する
            Args = $"config {scope} {key} {value.Quoted()}";

        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// ローカルリポジトリの設定かどうかを示すフラグ。
    /// </summary>
    private bool _isLocal = false;
}
