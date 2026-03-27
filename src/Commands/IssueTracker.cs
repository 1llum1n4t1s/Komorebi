using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
///     課題トラッカーの設定をgit configまたは専用ファイルで管理するコマンド群。
///     コミットメッセージ中の課題番号パターンとURLテンプレートの読み書きを行う。
/// </summary>
public class IssueTracker : Command
{
    /// <summary>
    ///     IssueTrackerコマンドを初期化する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    /// <param name="isShared">trueの場合は共有ファイル(.issuetracker)に保存、falseの場合はローカルgit configに保存。</param>
    public IssueTracker(string repo, bool isShared)
    {
        WorkingDirectory = repo;
        Context = repo;

        if (isShared)
        {
            // 共有設定: リポジトリ直下の.issuetrackerファイルを使用する
            var storage = $"{repo}/.issuetracker";
            _isStorageFileExists = File.Exists(storage);
            _baseArg = $"config -f {storage.Quoted()}";
        }
        else
        {
            // ローカル設定: git configの--localスコープを使用する
            _isStorageFileExists = true;
            _baseArg = "config --local";
        }
    }

    /// <summary>
    ///     全ての課題トラッカールールを非同期で読み取る。
    ///     git config -l の出力からissuetracker.*キーをパースする。
    /// </summary>
    /// <param name="outs">読み取ったルールを追加する出力リスト。</param>
    /// <param name="isShared">共有設定かどうかを示すフラグ。</param>
    public async Task ReadAllAsync(List<Models.IssueTracker> outs, bool isShared)
    {
        // ストレージファイルが存在しない場合は何もしない
        if (!_isStorageFileExists)
            return;

        // git config -l: 全ての設定値を一覧取得する
        Args = $"{_baseArg} -l";

        var rs = await ReadToEndAsync().ConfigureAwait(false);
        if (rs.IsSuccess)
        {
            var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split('=', 2);
                if (parts.Length < 2)
                    continue;

                var key = parts[0];
                var value = parts[1];

                // issuetracker.で始まるキーのみ処理する
                if (!key.StartsWith("issuetracker.", StringComparison.Ordinal))
                    continue;

                // .regexで終わるキーは正規表現パターンを設定する
                if (key.EndsWith(".regex", StringComparison.Ordinal))
                {
                    var prefixLen = "issuetracker.".Length;
                    var suffixLen = ".regex".Length;
                    var ruleName = key.Substring(prefixLen, key.Length - prefixLen - suffixLen);
                    FindOrAdd(outs, ruleName, isShared).RegexString = value;
                }
                // .urlで終わるキーはURLテンプレートを設定する
                else if (key.EndsWith(".url", StringComparison.Ordinal))
                {
                    var prefixLen = "issuetracker.".Length;
                    var suffixLen = ".url".Length;
                    var ruleName = key.Substring(prefixLen, key.Length - prefixLen - suffixLen);
                    FindOrAdd(outs, ruleName, isShared).URLTemplate = value;
                }
            }
        }
    }

    /// <summary>
    ///     新しい課題トラッカールールを追加する。
    ///     正規表現パターンとURLテンプレートの2つの設定を書き込む。
    /// </summary>
    /// <param name="rule">追加するルール情報。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> AddAsync(Models.IssueTracker rule)
    {
        // 正規表現パターンを設定する
        Args = $"{_baseArg} issuetracker.{rule.Name.Quoted()}.regex {rule.RegexString.Quoted()}";

        var succ = await ExecAsync().ConfigureAwait(false);
        if (succ)
        {
            // URLテンプレートを設定する
            Args = $"{_baseArg} issuetracker.{rule.Name.Quoted()}.url {rule.URLTemplate.Quoted()}";
            return await ExecAsync().ConfigureAwait(false);
        }

        return false;
    }

    /// <summary>
    ///     課題トラッカールールの正規表現パターンを更新する。
    /// </summary>
    /// <param name="rule">更新するルール情報。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> UpdateRegexAsync(Models.IssueTracker rule)
    {
        Args = $"{_baseArg} issuetracker.{rule.Name.Quoted()}.regex {rule.RegexString.Quoted()}";
        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     課題トラッカールールのURLテンプレートを更新する。
    /// </summary>
    /// <param name="rule">更新するルール情報。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> UpdateURLTemplateAsync(Models.IssueTracker rule)
    {
        Args = $"{_baseArg} issuetracker.{rule.Name.Quoted()}.url {rule.URLTemplate.Quoted()}";
        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     課題トラッカールールを削除する。
    ///     git config --remove-section で該当セクション全体を削除する。
    /// </summary>
    /// <param name="name">削除するルール名。</param>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> RemoveAsync(string name)
    {
        // ストレージファイルが存在しない場合は何もせず成功とする
        if (!_isStorageFileExists)
            return true;

        // git config --remove-section: 指定セクションを丸ごと削除する
        Args = $"{_baseArg} --remove-section issuetracker.{name.Quoted()}";
        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     ルールリストから指定名のルールを検索し、見つからなければ新規作成して追加する。
    /// </summary>
    /// <param name="rules">検索対象のルールリスト。</param>
    /// <param name="ruleName">検索するルール名。</param>
    /// <param name="isShared">共有設定かどうか。</param>
    /// <returns>見つかったまたは新規作成したルール。</returns>
    private Models.IssueTracker FindOrAdd(List<Models.IssueTracker> rules, string ruleName, bool isShared)
    {
        // 既存のルールから名前で検索する
        var rule = rules.Find(x => x.Name.Equals(ruleName, StringComparison.Ordinal));
        if (rule is not null)
            return rule;

        // 見つからなければ新しいルールを作成して追加する
        rule = new Models.IssueTracker() { IsShared = isShared, Name = ruleName };
        rules.Add(rule);
        return rule;
    }

    /// <summary>
    ///     設定ストレージファイルが存在するかどうか。
    /// </summary>
    private readonly bool _isStorageFileExists;

    /// <summary>
    ///     git configコマンドの基本引数（スコープとファイル指定）。
    /// </summary>
    private readonly string _baseArg;
}
