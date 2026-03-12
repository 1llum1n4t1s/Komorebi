using System;
using System.Diagnostics;

namespace Komorebi.Commands
{
    /// <summary>
    ///     外部差分ツールを起動して差分を表示するgitコマンド。
    ///     git difftool を実行する。
    /// </summary>
    public class DiffTool : Command
    {
        /// <summary>
        ///     DiffToolコマンドを初期化する。
        /// </summary>
        /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
        /// <param name="option">diff対象を指定するオプション。</param>
        public DiffTool(string repo, Models.DiffOption option)
        {
            WorkingDirectory = repo;
            Context = repo;
            _option = option;
        }

        /// <summary>
        ///     外部差分ツールを起動する。
        ///     設定に応じてカスタムツールまたはgit configのツールを使用する。
        /// </summary>
        public void Open()
        {
            // アプリ設定からdiff/mergeツールの設定を取得する
            var tool = Native.OS.GetDiffMergeTool(true);
            if (tool == null)
            {
                App.RaiseException(Context, App.Text("DiffMergeTool.InvalidSetting"));
                return;
            }

            if (string.IsNullOrEmpty(tool.Cmd))
            {
                // カスタムコマンドが未設定の場合はgit configのdifftoolを使用する
                if (!CheckGitConfiguration())
                    return;

                // git difftool -g --no-prompt: GUIディフツールをプロンプトなしで起動する
                Args = $"difftool -g --no-prompt {_option}";
            }
            else
            {
                // カスタムツールのコマンドを構成する
                var cmd = $"{tool.Exec.Quoted()} {tool.Cmd}";

                // git -c difftool.komorebi.cmd=... difftool --tool=komorebi:
                // カスタムディフツールを一時設定で起動する
                Args = $"-c difftool.komorebi.cmd={cmd.Quoted()} difftool --tool=komorebi --no-prompt {_option}";
            }

            try
            {
                // gitプロセスを起動する（非同期で外部ツールが開く）
                Process.Start(CreateGitStartInfo(false));
            }
            catch (Exception ex)
            {
                App.RaiseException(Context, ex.Message);
            }
        }

        /// <summary>
        ///     git configに差分ツールが設定されているかを確認する。
        ///     diff.guitool → merge.guitool → diff.tool → merge.tool の順に確認する。
        /// </summary>
        /// <returns>有効な差分ツールが設定されていればtrue。</returns>
        private bool CheckGitConfiguration()
        {
            // git configから全設定を読み取る
            var config = new Config(WorkingDirectory).ReadAll();

            // 優先順位に従ってdiffツール設定を検索する
            if (config.TryGetValue("diff.guitool", out var guiTool))
                return CheckCLIBasedTool(guiTool);
            if (config.TryGetValue("merge.guitool", out var mergeGuiTool))
                return CheckCLIBasedTool(mergeGuiTool);
            if (config.TryGetValue("diff.tool", out var diffTool))
                return CheckCLIBasedTool(diffTool);
            if (config.TryGetValue("merge.tool", out var mergeTool))
                return CheckCLIBasedTool(mergeTool);

            App.RaiseException(Context, App.Text("DiffMergeTool.MissingConfig", "diff.guitool"));
            return false;
        }

        /// <summary>
        ///     CLIベースのツールかどうかを確認する。
        ///     vimdiff/nvimdiffはGUIアプリで非対応のためエラーとする。
        /// </summary>
        /// <param name="tool">確認するツール名。</param>
        /// <returns>GUIで使用可能なツールであればtrue。</returns>
        private bool CheckCLIBasedTool(string tool)
        {
            // vimdiff/nvimdiffはCLIツールなのでGUIアプリでは非対応
            if (tool.StartsWith("vimdiff", StringComparison.Ordinal) ||
                tool.StartsWith("nvimdiff", StringComparison.Ordinal))
            {
                App.RaiseException(Context, App.Text("DiffMergeTool.CLIToolNotSupported", tool));
                return false;
            }

            return true;
        }

        /// <summary>
        ///     diff対象のオプション。
        /// </summary>
        private Models.DiffOption _option;
    }
}
