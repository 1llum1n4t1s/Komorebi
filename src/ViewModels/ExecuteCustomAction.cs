using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>
/// カスタムアクションのコントロールパラメータのインターフェース。
/// </summary>
public interface ICustomActionControlParameter
{
    /// <summary>
    /// パラメータの値を取得する。
    /// </summary>
    string GetValue();
}

/// <summary>
/// テキスト入力コントロールのパラメータ。
/// </summary>
public class CustomActionControlTextBox : ICustomActionControlParameter
{
    /// <summary>ラベルテキスト。</summary>
    public string Label { get; set; }
    /// <summary>プレースホルダーテキスト。</summary>
    public string Placeholder { get; set; }
    /// <summary>入力テキスト。</summary>
    public string Text { get; set; }
    /// <summary>値を整形するテンプレート文字列（upstream fb708065）。<c>${VALUE}</c> が入力テキストに展開される。</summary>
    public string Formatter { get; set; }

    /// <summary>コンストラクタ。ラベル、プレースホルダー、デフォルト値、フォーマッターを指定する。</summary>
    public CustomActionControlTextBox(string label, string placeholder, string defaultValue, string formatter)
    {
        Label = label + ":";
        Placeholder = placeholder;
        Text = defaultValue;
        Formatter = formatter;
    }

    /// <summary>入力テキストを値として返す（空ならそのまま空、Formatter が設定されていれば ${VALUE} に差し込む）。</summary>
    public string GetValue()
    {
        if (string.IsNullOrEmpty(Text))
            return string.Empty;
        return string.IsNullOrEmpty(Formatter) ? Text : Formatter.Replace("${VALUE}", Text);
    }
}

/// <summary>
/// ファイル/フォルダパス選択コントロールのパラメータ。
/// </summary>
public class CustomActionControlPathSelector : ObservableObject, ICustomActionControlParameter
{
    /// <summary>ラベルテキスト。</summary>
    public string Label { get; set; }
    /// <summary>プレースホルダーテキスト。</summary>
    public string Placeholder { get; set; }
    /// <summary>フォルダ選択モードかどうか。</summary>
    public bool IsFolder { get; set; }

    /// <summary>選択されたパス。</summary>
    public string Path
    {
        get => _path;
        set => SetProperty(ref _path, value);
    }

    /// <summary>コンストラクタ。ラベル、プレースホルダー、フォルダモード、デフォルト値を指定する。</summary>
    public CustomActionControlPathSelector(string label, string placeholder, bool isFolder, string defaultValue)
    {
        Label = label + ":";
        Placeholder = placeholder;
        IsFolder = isFolder;
        _path = defaultValue;
    }

    /// <summary>選択パスを値として返す。</summary>
    public string GetValue() => _path;

    private string _path; // 選択されたパス
}

/// <summary>
/// チェックボックスコントロールのパラメータ。チェック時に指定値を返す。
/// </summary>
public class CustomActionControlCheckBox : ICustomActionControlParameter
{
    /// <summary>ラベルテキスト。</summary>
    public string Label { get; set; }
    /// <summary>ツールチップテキスト。</summary>
    public string ToolTip { get; set; }
    /// <summary>チェック時に返す値。</summary>
    public string CheckedValue { get; set; }
    /// <summary>チェック状態。</summary>
    public bool IsChecked { get; set; }

    /// <summary>コンストラクタ。ラベル、ツールチップ、チェック時の値、初期チェック状態を指定する。</summary>
    public CustomActionControlCheckBox(string label, string tooltip, string checkedValue, bool isChecked)
    {
        Label = label;
        ToolTip = string.IsNullOrEmpty(tooltip) ? null : tooltip;
        CheckedValue = checkedValue;
        IsChecked = isChecked;
    }

    /// <summary>チェック時はCheckedValue、未チェック時は空文字を返す。</summary>
    public string GetValue() => IsChecked ? CheckedValue : string.Empty;
}

/// <summary>
/// ドロップダウン選択コントロールのパラメータ。パイプ区切りの選択肢リストを持つ。
/// </summary>
public class CustomActionControlComboBox : ObservableObject, ICustomActionControlParameter
{
    /// <summary>ラベルテキスト。</summary>
    public string Label { get; set; }
    /// <summary>説明テキスト。</summary>
    public string Description { get; set; }
    /// <summary>選択肢リスト。</summary>
    public List<string> Options { get; set; } = [];

    /// <summary>現在選択されている値（upstream dfe362f2 で auto-property 化）。</summary>
    public string Value { get; set; }

    /// <summary>
    /// コンストラクタ。ラベル、説明、パイプ区切りの選択肢文字列を指定する。
    /// </summary>
    public CustomActionControlComboBox(string label, string description, string options)
    {
        Label = label;
        Description = description;

        var parts = options.Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length > 0)
        {
            Options.AddRange(parts);
            Value = parts[0];
        }
    }

    /// <summary>選択中の値を返す。</summary>
    public string GetValue() => Value;
}

/// <summary>
/// ブランチ選択ドロップダウンのコントロールパラメータ（upstream dfe362f2）。
/// ローカル/リモートブランチを絞り込んで選択させ、Friendly Name / Full Name のどちらを値として返すか切り替えられる。
/// </summary>
public class CustomActionControlBranchSelector : ObservableObject, ICustomActionControlParameter
{
    /// <summary>ラベルテキスト。</summary>
    public string Label { get; set; }
    /// <summary>説明テキスト。</summary>
    public string Description { get; set; }
    /// <summary>選択可能なブランチ一覧。</summary>
    public List<Models.Branch> Branches { get; set; } = [];
    /// <summary>現在選択されているブランチ。</summary>
    public Models.Branch SelectedBranch { get; set; }

    /// <summary>
    /// コンストラクタ。ラベル、説明、対象リポジトリ、ローカル/リモートフラグ、FriendlyName 使用フラグを指定する。
    /// </summary>
    public CustomActionControlBranchSelector(string label, string description, Repository repo, bool isLocal, bool useFriendlyName)
    {
        Label = label;
        Description = description;
        _useFriendlyName = useFriendlyName;

        // HEAD を切り離した状態のブランチは選択肢から除外する
        foreach (var b in repo.Branches)
        {
            if (b.IsLocal == isLocal && !b.IsDetachedHead)
                Branches.Add(b);
        }

        if (Branches.Count > 0)
            SelectedBranch = Branches[0];
    }

    /// <summary>選択中のブランチ名を返す。useFriendlyName=true の場合は FriendlyName、false の場合は Name。</summary>
    public string GetValue()
    {
        if (SelectedBranch is null)
            return string.Empty;

        return _useFriendlyName ? SelectedBranch.FriendlyName : SelectedBranch.Name;
    }

    private bool _useFriendlyName = false;
}

/// <summary>
/// カスタムアクションを実行するダイアログViewModel。
/// コントロールパラメータの値をプレースホルダーに置換してコマンドを実行する。
/// </summary>
public class ExecuteCustomAction : Popup
{
    /// <summary>
    /// 実行するカスタムアクションの定義。
    /// </summary>
    public Models.CustomAction CustomAction
    {
        get;
    }

    /// <summary>
    /// アクションのスコープ対象（ブランチ、コミット、タグ等）。
    /// </summary>
    public object Target
    {
        get;
    }

    /// <summary>
    /// UIコントロールパラメータのリスト（テキストボックス、パス選択等）。
    /// </summary>
    public List<ICustomActionControlParameter> ControlParameters
    {
        get;
    } = [];

    /// <summary>
    /// コンストラクタ。リポジトリ、カスタムアクション定義、スコープ対象を指定する。
    /// </summary>
    public ExecuteCustomAction(Repository repo, Models.CustomAction action, object scopeTarget)
    {
        _repo = repo;
        CustomAction = action;
        Target = scopeTarget ?? new Models.Null();

        // upstream 770a9184: PrepareControlParameters メソッドを constructor に inline 化
        foreach (var ctl in CustomAction.Controls)
        {
            switch (ctl.Type)
            {
                case Models.CustomActionControlType.TextBox:
                    // upstream fb708065: StringFormatter を渡して ${VALUE} プレースホルダー対応
                    ControlParameters.Add(new CustomActionControlTextBox(ctl.Label, ctl.Description, PrepareStringByTarget(ctl.StringValue), ctl.StringFormatter));
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
                case Models.CustomActionControlType.LocalBranchSelector:
                    // ローカルブランチを絞り込んで選択させる（upstream dfe362f2）
                    ControlParameters.Add(new CustomActionControlBranchSelector(ctl.Label, ctl.Description, _repo, true, false));
                    break;
                case Models.CustomActionControlType.RemoteBranchSelector:
                    // リモートブランチを絞り込んで選択させる。BoolValue=true で FriendlyName を値として使う（upstream dfe362f2）
                    ControlParameters.Add(new CustomActionControlBranchSelector(ctl.Label, ctl.Description, _repo, false, ctl.BoolValue));
                    break;
            }
        }
    }

    /// <summary>
    /// カスタムアクション実行の確認アクション。
    /// プレースホルダーをターゲット値とコントロールパラメータ値で置換してコマンドを起動する。
    /// </summary>
    public override async Task<bool> Sure()
    {
        using var lockWatcher = _repo.LockWatcher();
        ProgressDescription = App.Text("Progress.RunCustomAction");

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
    /// 文字列内のターゲット関連プレースホルダー（${REPO}, ${BRANCH}, ${SHA}等）を実際の値に置換する。
    /// </summary>
    private string PrepareStringByTarget(string org)
    {
        // ${REPO} をリポジトリ作業ディレクトリ（OS形式）に展開する（upstream 9144daeb で GetWorkdir を inline 化）
        var repoPath = OperatingSystem.IsWindows() ? _repo.FullPath.Replace("/", "\\") : _repo.FullPath;
        org = org.Replace("${REPO}", repoPath);

        return Target switch
        {
            Models.Branch b => org.Replace("${BRANCH_FRIENDLY_NAME}", b.FriendlyName).Replace("${BRANCH}", b.Name).Replace("${REMOTE}", b.Remote),
            Models.Commit c => org.Replace("${SHA}", c.SHA),
            Models.Tag t => org.Replace("${TAG}", t.Name),
            Models.Remote r => org.Replace("${REMOTE}", r.Name),
            Models.CustomActionTargetFile f => org.Replace("${FILE}", f.File).Replace("${SHA}", f.Revision?.SHA ?? string.Empty),
            // Repository scope では ${BRANCH} を現在ブランチ名に展開する（upstream 8395efdd）
            _ => org.Replace("${BRANCH}", _repo.CurrentBranch?.Name ?? "HEAD")
        };
    }

    /// <summary>
    /// 外部プロセスをバックグラウンドで起動する（完了を待たない）。
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
    /// 外部プロセスを非同期で実行し、出力をログに記録する（完了を待つ）。
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
            if (e.Data is not null)
                log?.AppendLine(e.Data);
        };

        var builder = new StringBuilder();
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
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

    private readonly Repository _repo = null; // 対象リポジトリ
}