using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.Models;

/// <summary>
/// カスタムアクションの適用範囲を定義するenum
/// </summary>
public enum CustomActionScope
{
    /// <summary>リポジトリ全体</summary>
    Repository,
    /// <summary>特定のコミット</summary>
    Commit,
    /// <summary>特定のブランチ</summary>
    Branch,
    /// <summary>特定のタグ</summary>
    Tag,
    /// <summary>特定のリモート</summary>
    Remote,
    /// <summary>特定のファイル</summary>
    File,
}

/// <summary>
/// カスタムアクションのUI入力コントロールの種類
/// </summary>
public enum CustomActionControlType
{
    /// <summary>テキスト入力ボックス</summary>
    TextBox = 0,
    /// <summary>パス選択ダイアログ</summary>
    PathSelector,
    /// <summary>チェックボックス</summary>
    CheckBox,
    /// <summary>ドロップダウン選択</summary>
    ComboBox,
    /// <summary>ローカルブランチ選択（upstream dfe362f2）</summary>
    LocalBranchSelector,
    /// <summary>リモートブランチ選択（upstream dfe362f2）</summary>
    RemoteBranchSelector,
}

/// <summary>
/// カスタムアクションの対象ファイルとリビジョン情報を保持するレコード
/// </summary>
public record CustomActionTargetFile(string File, Commit Revision);

/// <summary>
/// カスタムアクションダイアログ内の個別入力コントロールの定義
/// </summary>
public class CustomActionControl : ObservableObject
{
    /// <summary>
    /// コントロールの種類（テキストボックス、パス選択、チェックボックス、コンボボックス）
    /// </summary>
    public CustomActionControlType Type
    {
        get => _type;
        set => SetProperty(ref _type, value);
    }

    /// <summary>
    /// コントロールのラベル文字列
    /// </summary>
    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    /// <summary>
    /// コントロールの説明文
    /// </summary>
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    /// <summary>
    /// 文字列型の入力値（テキストボックス、パス選択、コンボボックス用）
    /// </summary>
    public string StringValue
    {
        get => _stringValue;
        set => SetProperty(ref _stringValue, value);
    }

    /// <summary>
    /// ブール型の入力値（チェックボックス用）
    /// </summary>
    public bool BoolValue
    {
        get => _boolValue;
        set => SetProperty(ref _boolValue, value);
    }

    private CustomActionControlType _type = CustomActionControlType.TextBox;
    private string _label = string.Empty;
    private string _description = string.Empty;
    private string _stringValue = string.Empty;
    private bool _boolValue = false;
}

/// <summary>
/// ユーザー定義のカスタムアクション。外部コマンドの実行を定義する。
/// </summary>
public class CustomAction : ObservableObject
{
    /// <summary>
    /// カスタムアクションの表示名
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>
    /// アクションの適用範囲（リポジトリ、コミット、ブランチなど）
    /// </summary>
    public CustomActionScope Scope
    {
        get => _scope;
        set => SetProperty(ref _scope, value);
    }

    /// <summary>
    /// 実行する外部コマンドの実行ファイルパス
    /// </summary>
    public string Executable
    {
        get => _executable;
        set => SetProperty(ref _executable, value);
    }

    /// <summary>
    /// 外部コマンドに渡す引数テンプレート
    /// </summary>
    public string Arguments
    {
        get => _arguments;
        set => SetProperty(ref _arguments, value);
    }

    /// <summary>
    /// ダイアログに表示するカスタムコントロールのリスト
    /// </summary>
    public AvaloniaList<CustomActionControl> Controls
    {
        get;
        set;
    } = [];

    /// <summary>
    /// 外部コマンドの終了を待機するかどうか
    /// </summary>
    public bool WaitForExit
    {
        get => _waitForExit;
        set => SetProperty(ref _waitForExit, value);
    }

    private string _name = string.Empty;
    private CustomActionScope _scope = CustomActionScope.Repository;
    private string _executable = string.Empty;
    private string _arguments = string.Empty;
    private bool _waitForExit = true;
}
