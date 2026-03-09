using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Komorebi.Commands
{
    /// <summary>
    ///     全てのgitコマンドの基底クラス。
    ///     gitプロセスの起動、標準出力/エラーの取得、非同期実行などを提供する。
    /// </summary>
    public partial class Command
    {
        /// <summary>
        ///     gitコマンドの実行結果を格納するクラス。
        /// </summary>
        public class Result
        {
            /// <summary>
            ///     コマンドが成功したかどうかを示すフラグ。
            /// </summary>
            public bool IsSuccess { get; set; } = false;

            /// <summary>
            ///     標準出力の内容。
            /// </summary>
            public string StdOut { get; set; } = string.Empty;

            /// <summary>
            ///     標準エラー出力の内容。
            /// </summary>
            public string StdErr { get; set; } = string.Empty;

            /// <summary>
            ///     失敗結果を生成するファクトリメソッド。
            /// </summary>
            /// <param name="reason">失敗理由の文字列。</param>
            /// <returns>エラーメッセージが設定された失敗結果。</returns>
            public static Result Failed(string reason) => new Result() { StdErr = reason };
        }

        /// <summary>
        ///     gitエディタの種別を定義する列挙型。
        /// </summary>
        public enum EditorType
        {
            /// <summary>エディタを使用しない。</summary>
            None,
            /// <summary>通常のコアエディタ。</summary>
            CoreEditor,
            /// <summary>リベース用エディタ。</summary>
            RebaseEditor,
        }

        /// <summary>
        ///     コマンドの実行コンテキスト（通常はリポジトリパス）。エラー表示時に使用される。
        /// </summary>
        public string Context { get; set; } = string.Empty;

        /// <summary>
        ///     gitコマンドを実行する作業ディレクトリ。
        /// </summary>
        public string WorkingDirectory { get; set; } = null;

        /// <summary>
        ///     使用するエディタの種別。デフォルトはCoreEditor。
        /// </summary>
        public EditorType Editor { get; set; } = EditorType.CoreEditor;

        /// <summary>
        ///     SSH認証に使用する秘密鍵のパス。
        /// </summary>
        public string SSHKey { get; set; } = string.Empty;

        /// <summary>
        ///     gitコマンドに渡す引数文字列。
        /// </summary>
        public string Args { get; set; } = string.Empty;

        // Only used in `ExecAsync` mode.
        /// <summary>
        ///     非同期実行時のキャンセルトークン。ExecAsyncモードでのみ使用される。
        /// </summary>
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        /// <summary>
        ///     エラー発生時に例外を上げるかどうかのフラグ。
        /// </summary>
        public bool RaiseError { get; set; } = true;

        /// <summary>
        ///     コマンドログの出力先。nullの場合はログを記録しない。
        /// </summary>
        public Models.ICommandLog Log { get; set; } = null;

        /// <summary>
        ///     gitコマンドを非同期で実行し、出力をストリーミングで処理する。
        ///     主にfetch、push、pullなど長時間実行されるコマンドに使用する。
        /// </summary>
        /// <returns>コマンドが成功した場合はtrue。</returns>
        public async Task<bool> ExecAsync()
        {
            // コマンドラインをログに記録する
            Log?.AppendLine($"$ git {Args}\n");

            // エラーメッセージ収集用のリスト
            var errs = new List<string>();

            // gitプロセスを作成し、出力リダイレクトを設定する
            using var proc = new Process();
            proc.StartInfo = CreateGitStartInfo(true);
            proc.OutputDataReceived += (_, e) => HandleOutput(e.Data, errs);
            proc.ErrorDataReceived += (_, e) => HandleOutput(e.Data, errs);

            // キャンセル時にプロセスを強制終了するためのラッパー
            var captured = new CapturedProcess() { Process = proc };
            var capturedLock = new object();
            try
            {
                // プロセスを起動する
                proc.Start();

                // Not safe, please only use `CancellationToken` in readonly commands.
                // キャンセルトークンが設定されている場合、キャンセル時にプロセスを終了する
                if (CancellationToken.CanBeCanceled)
                {
                    CancellationToken.Register(() =>
                    {
                        lock (capturedLock)
                        {
                            if (captured is { Process: { HasExited: false } })
                                captured.Process.Kill();
                        }
                    });
                }
            }
            catch (Exception e)
            {
                // プロセス起動に失敗した場合、エラーを通知する
                if (RaiseError)
                    App.RaiseException(Context, e.Message);

                Log?.AppendLine(string.Empty);
                return false;
            }

            // 標準出力と標準エラーの非同期読み取りを開始する
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            try
            {
                // プロセスの終了を待機する
                await proc.WaitForExitAsync(CancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                // 待機中の例外（キャンセルなど）を処理する
                HandleOutput(e.Message, errs);
            }

            // プロセス参照をクリアしてキャンセルハンドラでの二重終了を防ぐ
            lock (capturedLock)
            {
                captured.Process = null;
            }

            Log?.AppendLine(string.Empty);

            // キャンセルされておらず、終了コードが0以外の場合はエラーとして処理する
            if (!CancellationToken.IsCancellationRequested && proc.ExitCode != 0)
            {
                if (RaiseError)
                {
                    // エラーメッセージを結合してユーザーに通知する
                    var errMsg = string.Join("\n", errs).Trim();
                    if (!string.IsNullOrEmpty(errMsg))
                        App.RaiseException(Context, errMsg);
                }

                return false;
            }

            return true;
        }

        /// <summary>
        ///     gitコマンドを同期的に実行し、標準出力と標準エラーを全て読み取る。
        ///     短時間で完了するコマンド（configの取得など）に使用する。
        /// </summary>
        /// <returns>実行結果を含むResultオブジェクト。</returns>
        protected Result ReadToEnd()
        {
            // gitプロセスを作成して起動する
            using var proc = new Process();
            proc.StartInfo = CreateGitStartInfo(true);

            try
            {
                proc.Start();
            }
            catch (Exception e)
            {
                // プロセス起動失敗時は失敗結果を返す
                return Result.Failed(e.Message);
            }

            // 標準出力と標準エラーを全て読み取る
            var rs = new Result() { IsSuccess = true };
            rs.StdOut = proc.StandardOutput.ReadToEnd();
            rs.StdErr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            // 終了コードで成功/失敗を判定する
            rs.IsSuccess = proc.ExitCode == 0;
            return rs;
        }

        /// <summary>
        ///     gitコマンドを非同期で実行し、標準出力と標準エラーを全て読み取る。
        ///     ReadToEndの非同期版。
        /// </summary>
        /// <returns>実行結果を含むResultオブジェクト。</returns>
        protected async Task<Result> ReadToEndAsync()
        {
            // gitプロセスを作成して起動する
            using var proc = new Process();
            proc.StartInfo = CreateGitStartInfo(true);

            try
            {
                proc.Start();
            }
            catch (Exception e)
            {
                return Result.Failed(e.Message);
            }

            // 標準出力と標準エラーを非同期で読み取る
            var rs = new Result() { IsSuccess = true };
            rs.StdOut = await proc.StandardOutput.ReadToEndAsync(CancellationToken).ConfigureAwait(false);
            rs.StdErr = await proc.StandardError.ReadToEndAsync(CancellationToken).ConfigureAwait(false);
            await proc.WaitForExitAsync(CancellationToken).ConfigureAwait(false);

            // 終了コードで成功/失敗を判定する
            rs.IsSuccess = proc.ExitCode == 0;
            return rs;
        }

        /// <summary>
        ///     gitプロセス起動用のProcessStartInfoを生成する。
        ///     環境変数（SSH、ロケールなど）やエディタ設定も行う。
        /// </summary>
        /// <param name="redirect">標準出力/エラーをリダイレクトするかどうか。</param>
        /// <returns>設定済みのProcessStartInfoオブジェクト。</returns>
        protected ProcessStartInfo CreateGitStartInfo(bool redirect)
        {
            var start = new ProcessStartInfo();
            // gitの実行ファイルパスを設定する
            start.FileName = Native.OS.GitExecutable;
            start.UseShellExecute = false;
            start.CreateNoWindow = true;

            // リダイレクトが必要な場合、標準出力/エラーをUTF-8で取得する
            if (redirect)
            {
                start.RedirectStandardOutput = true;
                start.RedirectStandardError = true;
                start.StandardOutputEncoding = Encoding.UTF8;
                start.StandardErrorEncoding = Encoding.UTF8;
            }

            // Force using this app as SSH askpass program
            // このアプリ自体をSSH askpassプログラムとして使用する設定
            var selfExecFile = Process.GetCurrentProcess().MainModule!.FileName;
            start.Environment.Add("SSH_ASKPASS", selfExecFile); // Can not use parameter here, because it invoked by SSH with `exec`
            start.Environment.Add("SSH_ASKPASS_REQUIRE", "prefer");
            start.Environment.Add("SOURCEGIT_LAUNCH_AS_ASKPASS", "TRUE");
            if (!OperatingSystem.IsLinux())
                start.Environment.Add("DISPLAY", "required");

            // If an SSH private key was provided, sets the environment.
            // SSH秘密鍵が指定されている場合、GIT_SSH_COMMANDを設定する
            if (!start.Environment.ContainsKey("GIT_SSH_COMMAND") && !string.IsNullOrEmpty(SSHKey))
                start.Environment.Add("GIT_SSH_COMMAND", $"ssh -i '{SSHKey}' -F '/dev/null'");

            // Force using en_US.UTF-8 locale
            // Linuxの場合、ロケールをCに強制してgit出力を英語にする
            if (OperatingSystem.IsLinux())
            {
                start.Environment.Add("LANG", "C");
                start.Environment.Add("LC_ALL", "C");
            }

            // gitコマンドの共通オプションを構築する
            var builder = new StringBuilder(2048);
            builder
                .Append("--no-pager -c core.quotepath=off -c credential.helper=")
                .Append(Native.OS.CredentialHelper)
                .Append(' ');

            // エディタ種別に応じてcore.editorを設定する
            switch (Editor)
            {
                case EditorType.CoreEditor:
                    builder.Append($"""-c core.editor="\"{selfExecFile}\" --core-editor" """);
                    break;
                case EditorType.RebaseEditor:
                    builder.Append($"""-c core.editor="\"{selfExecFile}\" --rebase-message-editor" -c sequence.editor="\"{selfExecFile}\" --rebase-todo-editor" -c rebase.abbreviateCommands=true """);
                    break;
                default:
                    builder.Append("-c core.editor=true ");
                    break;
            }

            // コマンド固有の引数を追加する
            builder.Append(Args);
            start.Arguments = builder.ToString();

            // Working directory
            // 作業ディレクトリが指定されている場合に設定する
            if (!string.IsNullOrEmpty(WorkingDirectory))
                start.WorkingDirectory = WorkingDirectory;

            return start;
        }

        /// <summary>
        ///     プロセス出力の各行を処理し、ログへの記録とエラー収集を行う。
        ///     進捗表示や不要な行はフィルタリングする。
        /// </summary>
        /// <param name="line">プロセスから出力された1行。</param>
        /// <param name="errs">エラーメッセージの収集リスト。</param>
        private void HandleOutput(string line, List<string> errs)
        {
            if (line == null)
                return;

            // ログに出力行を記録する
            Log?.AppendLine(line);

            // Lines to hide in error message.
            // エラーメッセージに表示しない行をフィルタリングする
            if (line.Length > 0)
            {
                // リモート操作の進捗行やヒント行は除外する
                if (line.StartsWith("remote: Enumerating objects:", StringComparison.Ordinal) ||
                    line.StartsWith("remote: Counting objects:", StringComparison.Ordinal) ||
                    line.StartsWith("remote: Compressing objects:", StringComparison.Ordinal) ||
                    line.StartsWith("Filtering content:", StringComparison.Ordinal) ||
                    line.StartsWith("hint:", StringComparison.Ordinal))
                    return;

                // パーセンテージ表示の進捗行は除外する
                if (REG_PROGRESS().IsMatch(line))
                    return;
            }

            // フィルタリングされなかった行をエラーリストに追加する
            errs.Add(line);
        }

        /// <summary>
        ///     キャンセル時の安全なプロセス終了のためにプロセス参照を保持するクラス。
        /// </summary>
        private class CapturedProcess
        {
            /// <summary>
            ///     キャプチャされたプロセスの参照。
            /// </summary>
            public Process Process { get; set; } = null;
        }

        /// <summary>
        ///     進捗表示のパーセンテージパターンにマッチする正規表現。
        /// </summary>
        [GeneratedRegex(@"\d+%")]
        private static partial Regex REG_PROGRESS();
    }
}
