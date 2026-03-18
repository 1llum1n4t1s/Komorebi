using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     カスタムアクションのコントロールパラメータのインターフェース。
    /// </summary>
    public interface ICustomActionControlParameter
    {
        /// <summary>
        ///     パラメータの値を取得する。
        /// </summary>
        string GetValue();
    }

    /// <summary>
    ///     テキスト入力コントロールのパラメータ。
    /// </summary>
    public class CustomActionControlTextBox : ICustomActionControlParameter
    {
        public string Label { get; set; }
        public string Placeholder { get; set; }
        public string Text { get; set; }

        public CustomActionControlTextBox(string label, string placeholder, string defaultValue)
        {
            Label = label + ":";
            Placeholder = placeholder;
            Text = defaultValue;
        }

        public string GetValue() => Text;
    }

    /// <summary>
    ///     ファイル/フォルダパス選択コントロールのパラメータ。
    /// </summary>
    public class CustomActionControlPathSelector : ObservableObject, ICustomActionControlParameter
    {
        public string Label { get; set; }
        public string Placeholder { get; set; }
        public bool IsFolder { get; set; }

        public string Path
        {
            get => _path;
            set => SetProperty(ref _path, value);
        }

        public CustomActionControlPathSelector(string label, string placeholder, bool isFolder, string defaultValue)
        {
            Label = label + ":";
            Placeholder = placeholder;
            IsFolder = isFolder;
            _path = defaultValue;
        }

        public string GetValue() => _path;

        private string _path;
    }

    /// <summary>
    ///     チェックボックスコントロールのパラメータ。チェック時に指定値を返す。
    /// </summary>
    public class CustomActionControlCheckBox : ICustomActionControlParameter
    {
        public string Label { get; set; }
        public string ToolTip { get; set; }
        public string CheckedValue { get; set; }
        public bool IsChecked { get; set; }

        public CustomActionControlCheckBox(string label, string tooltip, string checkedValue, bool isChecked)
        {
            Label = label;
            ToolTip = string.IsNullOrEmpty(tooltip) ? null : tooltip;
            CheckedValue = checkedValue;
            IsChecked = isChecked;
        }

        public string GetValue() => IsChecked ? CheckedValue : string.Empty;
    }

    /// <summary>
    ///     ドロップダウン選択コントロールのパラメータ。パイプ区切りの選択肢リストを持つ。
    /// </summary>
    public class CustomActionControlComboBox : ObservableObject, ICustomActionControlParameter
    {
        public string Label { get; set; }
        public string Description { get; set; }
        public List<string> Options { get; set; } = [];

        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        public CustomActionControlComboBox(string label, string description, string options)
        {
            Label = label;
            Description = description;

            var parts = options.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length > 0)
            {
                Options.AddRange(parts);
                _value = parts[0];
            }
        }

        public string GetValue() => _value;

        private string _value = string.Empty;
    }

    /// <summary>
    ///     カスタムアクションを実行するダイアログViewModel。
    ///     コントロールパラメータの値をプレースホルダーに置換してコマンドを実行する。
    /// </summary>
    public class ExecuteCustomAction : Popup
    {
        /// <summary>
        ///     実行するカスタムアクションの定義。
        /// </summary>
        public Models.CustomAction CustomAction
        {
            get;
        }

        /// <summary>
        ///     アクションのスコープ対象（ブランチ、コミット、タグ等）。
        /// </summary>
        public object Target
        {
            get;
        }

        /// <summary>
        ///     UIコントロールパラメータのリスト（テキストボックス、パス選択等）。
        /// </summary>
        public List<ICustomActionControlParameter> ControlParameters
        {
            get;
        } = [];

        /// <summary>
        ///     コンストラクタ。リポジトリ、カスタムアクション定義、スコープ対象を指定する。
        /// </summary>
        public ExecuteCustomAction(Repository repo, Models.CustomAction action, object scopeTarget)
        {
            _repo = repo;
            CustomAction = action;
            Target = scopeTarget ?? new Models.Null();
            PrepareControlParameters();
        }

        /// <summary>
        ///     カスタムアクション実行の確認アクション。
        ///     プレースホルダーをターゲット値とコントロールパラメータ値で置換してコマンドを起動する。
        /// </summary>
        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Run custom action ...";

            // コマンドライン引数のプレースホルダーを実際の値に置換
            var cmdline = PrepareStringByTarget(CustomAction.Arguments);
            for (var i = ControlParameters.Count - 1; i >= 0; i--)
            {
                var param = ControlParameters[i];
                cmdline = cmdline.Replace($"${i + 1}", param.GetValue());
            }

            var log = _repo.CreateLog(CustomAction.Name);
            Use(log);

            log.AppendLine($"$ {CustomAction.Executable} {cmdline}\n");

            if (CustomAction.WaitForExit)
                await RunAsync(cmdline, log);
            else
                _ = Task.Run(() => Run(cmdline));

            log.Complete();
            return true;
        }

        /// <summary>
        ///     カスタムアクション定義のコントロール設定からUIパラメータオブジェクトを生成する。
        /// </summary>
        private void PrepareControlParameters()
        {
            foreach (var ctl in CustomAction.Controls)
            {
                switch (ctl.Type)
                {
                    case Models.CustomActionControlType.TextBox:
                        ControlParameters.Add(new CustomActionControlTextBox(ctl.Label, ctl.Description, PrepareStringByTarget(ctl.StringValue)));
                        break;
                    case Models.CustomActionControlType.PathSelector:
                        ControlParameters.Add(new CustomActionControlPathSelector(ctl.Label, ctl.Description, ctl.BoolValue, PrepareStringByTarget(ctl.StringValue)));
                        break;
                    case Models.CustomActionControlType.CheckBox:
                        ControlParameters.Add(new CustomActionControlCheckBox(ctl.Label, ctl.Description, ctl.StringValue, ctl.BoolValue));
                        break;
                    case Models.CustomActionControlType.ComboBox:
                        ControlParameters.Add(new CustomActionControlComboBox(ctl.Label, ctl.Description, PrepareStringByTarget(ctl.StringValue)));
                        break;
                }
            }
        }

        /// <summary>
        ///     文字列内のターゲット関連プレースホルダー（${REPO}, ${BRANCH}, ${SHA}等）を実際の値に置換する。
        /// </summary>
        private string PrepareStringByTarget(string org)
        {
            org = org.Replace("${REPO}", GetWorkdir());

            return Target switch
            {
                Models.Branch b => org.Replace("${BRANCH_FRIENDLY_NAME}", b.FriendlyName).Replace("${BRANCH}", b.Name).Replace("${REMOTE}", b.Remote),
                Models.Commit c => org.Replace("${SHA}", c.SHA),
                Models.Tag t => org.Replace("${TAG}", t.Name),
                Models.Remote r => org.Replace("${REMOTE}", r.Name),
                Models.CustomActionTargetFile f => org.Replace("${FILE}", f.File).Replace("${SHA}", f.Revision?.SHA ?? string.Empty),
                _ => org
            };
        }

        /// <summary>
        ///     作業ディレクトリのパスをOS形式で取得する。
        /// </summary>
        private string GetWorkdir()
        {
            return OperatingSystem.IsWindows() ? _repo.FullPath.Replace("/", "\\") : _repo.FullPath;
        }

        /// <summary>
        ///     外部プロセスをバックグラウンドで起動する（完了を待たない）。
        /// </summary>
        private void Run(string args)
        {
            var start = new ProcessStartInfo();
            start.FileName = CustomAction.Executable;
            start.Arguments = args;
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.WorkingDirectory = _repo.FullPath;

            try
            {
                Process.Start(start)?.Dispose();
            }
            catch (Exception e)
            {
                App.RaiseException(_repo.FullPath, e.Message);
            }
        }

        /// <summary>
        ///     外部プロセスを非同期で実行し、出力をログに記録する（完了を待つ）。
        /// </summary>
        private async Task RunAsync(string args, Models.ICommandLog log)
        {
            var start = new ProcessStartInfo();
            start.FileName = CustomAction.Executable;
            start.Arguments = args;
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.StandardOutputEncoding = Encoding.UTF8;
            start.StandardErrorEncoding = Encoding.UTF8;
            start.WorkingDirectory = _repo.FullPath;

            using var proc = new Process();
            proc.StartInfo = start;

            proc.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    log?.AppendLine(e.Data);
            };

            var builder = new StringBuilder();
            proc.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    log?.AppendLine(e.Data);
                    builder.AppendLine(e.Data);
                }
            };

            try
            {
                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                await proc.WaitForExitAsync().ConfigureAwait(false);

                var exitCode = proc.ExitCode;
                if (exitCode != 0)
                {
                    var errMsg = builder.ToString().Trim();
                    if (!string.IsNullOrEmpty(errMsg))
                        App.RaiseException(_repo.FullPath, errMsg);
                }
            }
            catch (Exception e)
            {
                App.RaiseException(_repo.FullPath, e.Message);
            }
        }

        private readonly Repository _repo = null;
    }
}
