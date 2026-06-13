using System.Threading.Tasks;

namespace Komorebi.ViewModels;

/// <summary>
/// ディスク上に存在しない無効なリポジトリノードを一括削除する確認ダイアログのViewModel。
/// 別 PC と共有した preference.json のように、登録パスがローカルに存在しないエントリをまとめて掃除する用途。
/// 実際の削除ロジックは <see cref="Preferences.AutoRemoveInvalidNode"/> に委譲する。
/// </summary>
public class ClearInvalidRepositoryNodes : Popup
{
    /// <summary>削除対象となる無効なリポジトリの件数。確認文面に表示する。</summary>
    public int Count
    {
        get;
    }

    /// <summary>削除対象件数を埋め込んだローカライズ済みの確認メッセージ。</summary>
    public string Message => App.Text("ClearInvalidRepositoryNodes.Tip", Count);

    /// <summary>コンストラクタ。削除対象件数を受け取る。</summary>
    public ClearInvalidRepositoryNodes(int count)
    {
        Count = count;
    }

    /// <summary>
    /// 無効リポジトリの一括削除を実行する確認アクション。
    /// 設定から無効ノードを再帰的に削除し、保存してウェルカム画面を更新する。
    /// </summary>
    public override Task<bool> Sure()
    {
        Preferences.Instance.AutoRemoveInvalidNode();
        Preferences.Instance.Save();
        Welcome.Instance.Refresh();
        return Task.FromResult(true);
    }
}
