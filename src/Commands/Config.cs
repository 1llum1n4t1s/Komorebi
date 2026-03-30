using System;
using System.Collections.Generic;
using System.Globalization;
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
        // git config -l: 全ての設定値を一覧表示する
        Args = "config -l";

        var output = ReadToEnd();
        var rs = new Dictionary<string, string>();
        if (output.IsSuccess)
        {
            // 出力を行ごとに分割し、key=value形式をパースする
            var lines = output.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split('=', 2);
                if (parts.Length == 2)
                    rs[parts[0]] = parts[1];
            }
        }

        return rs;
    }

    /// <summary>
    /// 全ての設定値を非同期で読み取る。
    /// git config -l を実行し、キーと値のペアを辞書として返す。
    /// キーは小文字に正規化される。
    /// </summary>
    /// <returns>設定キー（小文字）と値の辞書。</returns>
    public async Task<Dictionary<string, string>> ReadAllAsync()
    {
        // git config -l: 全ての設定値を一覧表示する
        Args = "config -l";

        var output = await ReadToEndAsync().ConfigureAwait(false);
        var rs = new Dictionary<string, string>();
        if (output.IsSuccess)
        {
            // 出力を行ごとに分割し、key=value形式をパースする
            var lines = output.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split('=', 2);
                if (parts.Length == 2)
                {
                    // キーは常に小文字で統一する（大文字小文字の差異を吸収するため）
                    var key = parts[0].ToLower(CultureInfo.CurrentCulture); // Always use lower case for key
                    var value = parts[1];
                    rs[key] = value;
                }
            }
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
