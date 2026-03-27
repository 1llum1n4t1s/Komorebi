using System;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
///     マージツールを起動してコンフリクト解決を行うgitコマンド。
///     git mergetool を実行する。
/// </summary>
public class MergeTool : Command
{
    /// <summary>
    ///     MergeToolコマンドを初期化する。
    /// </summary>
    /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
    /// <param name="file">マージツールで開くファイルパス（空の場合は全てのコンフリクトファイル）。</param>
    public MergeTool(string repo, string file)
    {
        WorkingDirectory = repo;
        Context = repo;

        // ファイルパスをクォートして保持する
        _file = string.IsNullOrEmpty(file) ? string.Empty : file.Quoted();
    }

    /// <summary>
    ///     マージツールを非同期で起動する。
    ///     設定に応じてカスタムツールまたはgit configのツールを使用する。
    /// </summary>
    /// <returns>コマンドが成功した場合はtrue。</returns>
    public async Task<bool> OpenAsync()
    {
        // アプリ設定からdiff/mergeツールの設定を取得する
        var tool = Native.OS.GetDiffMergeTool(false);
        if (tool is null)
        {
            App.RaiseException(Context, App.Text("DiffMergeTool.InvalidSetting"));
            return false;
        }

        if (string.IsNullOrEmpty(tool.Cmd))
        {
            // カスタムコマンドが未設定の場合はgit configのmergetoolを使用する
            var ok = await CheckGitConfigurationAsync();
            if (!ok)
                return false;

            // git mergetool -g --no-prompt: GUIマージツールをプロンプトなしで起動する
            Args = $"mergetool -g --no-prompt {_file}";
        }
        else
        {
            // カスタムツールのコマンドを構成する
            var cmd = $"{tool.Exec.Quoted()} {tool.Cmd}";

            // git -c mergetool.komorebi.cmd=... mergetool --tool=komorebi:
            // カスタムマージツールを一時設定で起動する
            // writeToTemp=true: 一時ファイルに書き出す
            // keepBackup=false: バックアップファイルを残さない
            // trustExitCode=true: ツールの終了コードを信頼する
            Args = $"-c mergetool.komorebi.cmd={cmd.Quoted()} -c mergetool.writeToTemp=true -c mergetool.keepBackup=false -c mergetool.trustExitCode=true mergetool --tool=komorebi {_file}";
        }

        return await ExecAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     git configにマージツールが設定されているかを確認する。
    ///     merge.guitool → merge.tool の順に確認する。
    /// </summary>
    /// <returns>有効なマージツールが設定されていればtrue。</returns>
    private async Task<bool> CheckGitConfigurationAsync()
    {
        // git configからmerge.guitoolを取得する
        var tool = await new Config(WorkingDirectory).GetAsync("merge.guitool");

        // merge.guitoolが未設定の場合はmerge.toolにフォールバックする
        if (string.IsNullOrEmpty(tool))
            tool = await new Config(WorkingDirectory).GetAsync("merge.tool");

        if (string.IsNullOrEmpty(tool))
        {
            App.RaiseException(Context, App.Text("DiffMergeTool.MissingConfig", "merge.guitool"));
            return false;
        }

        // CLIベースのツール（vimdiff, nvimdiff）はGUIアプリでは非対応
        if (tool.StartsWith("vimdiff", StringComparison.Ordinal) ||
            tool.StartsWith("nvimdiff", StringComparison.Ordinal))
        {
            App.RaiseException(Context, App.Text("DiffMergeTool.CLIToolNotSupported", tool));
            return false;
        }

        return true;
    }

    /// <summary>
    ///     マージツールで開くファイルパス。
    /// </summary>
    private string _file;
}
