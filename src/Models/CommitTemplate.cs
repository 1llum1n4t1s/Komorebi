using System.Collections.Generic;

using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.Models;

/// <summary>
/// コミットメッセージのテンプレートを表すクラス。
/// テンプレートエンジンを使用して変数を展開できる。
/// </summary>
public class CommitTemplate : ObservableObject
{
    /// <summary>
    /// テンプレートの名前。
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>
    /// テンプレートの内容（変数プレースホルダーを含む）。
    /// </summary>
    public string Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }

    /// <summary>
    /// テンプレートを適用し、変数を展開したメッセージを生成する。
    /// </summary>
    /// <param name="branch">現在のブランチ情報。</param>
    /// <param name="changes">現在の変更リスト。</param>
    /// <returns>展開済みのコミットメッセージ。</returns>
    public string Apply(Branch branch, List<Change> changes)
    {
        // テンプレートエンジンで変数を評価・展開
        var te = new TemplateEngine();
        return te.Eval(_content, branch, changes);
    }

    private string _name = string.Empty;
    private string _content = string.Empty;
}
