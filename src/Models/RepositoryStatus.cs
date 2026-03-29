namespace Komorebi.Models;

/// <summary>
/// リポジトリの現在の状態情報（ブランチ名、先行/後退コミット数、ローカル変更数）を保持するクラス
/// </summary>
public class RepositoryStatus
{
    /// <summary>現在のブランチ名</summary>
    public string CurrentBranch { get; set; } = string.Empty;
    /// <summary>リモートより先行しているコミット数</summary>
    public int Ahead { get; set; } = 0;
    /// <summary>リモートより後退しているコミット数</summary>
    public int Behind { get; set; } = 0;
    /// <summary>ローカルの変更ファイル数</summary>
    public int LocalChanges { get; set; } = 0;

    /// <summary>先行/後退コミットがある場合にtrueを返す</summary>
    public bool IsTrackingStatusVisible
    {
        get
        {
            return Ahead > 0 || Behind > 0;
        }
    }

    /// <summary>先行/後退コミット数の表示文字列（例: "3↑ 1↓"）</summary>
    public string TrackingDescription
    {
        get
        {
            if (Ahead > 0)
                return Behind > 0 ? $"{Ahead}↑ {Behind}↓" : $"{Ahead}↑";

            return Behind > 0 ? $"{Behind}↓" : string.Empty;
        }
    }
}
